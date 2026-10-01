using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Wediplan.Api.Data;
using Wediplan.Api.Domain;
using Wediplan.Api.Infrastructure.Audit;
using Xunit;

namespace Wediplan.Api.Tests;

/// <summary>
/// §Zadatak 14 — <see cref="AuditSaveChangesInterceptor"/> nad EF InMemory bazom (isti pristup kao ostali testovi,
/// bez Postgresa). Dva DbContexta dijele istu InMemory bazu preko zajedničkog <see cref="InMemoryDatabaseRoot"/>:
/// "plain" (bez interceptora — za pripremu podataka, da priprema ne ulazi u dnevnik) i "audited" (s interceptorom).
/// </summary>
public class AuditInterceptorTests
{
    private sealed class Env
    {
        public required DbContextOptions<AppDbContext> Plain { get; init; }
        public required DbContextOptions<AppDbContext> Audited { get; init; }
        public required AuditContext Actor { get; init; }
    }

    private static Env Build()
    {
        var name = Guid.NewGuid().ToString();
        var root = new InMemoryDatabaseRoot(); // bez zajedničkog roota dva konteksta NE bi dijelila bazu
        var actor = new AuditContext();        // bez HttpContexta → "system", osim ako test pozove Set(...)
        var interceptor = new AuditSaveChangesInterceptor(actor);
        return new Env
        {
            Actor = actor,
            Plain = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name, root).Options,
            Audited = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name, root)
                .AddInterceptors(interceptor).Options,
        };
    }

    private static Vendor SeedVendor(Env env, Action<Vendor>? customize = null)
    {
        var v = new Vendor { Slug = "foto-anic", Name = "Foto Anić", CategorySlug = "foto-i-video", RegionSlug = "dalmacija" };
        customize?.Invoke(v);
        using var db = new AppDbContext(env.Plain);
        db.Vendors.Add(v);
        db.SaveChanges();
        return v;
    }

    private static List<AuditLog> Logs(Env env)
    {
        using var db = new AppDbContext(env.Plain);
        return db.AuditLogs.AsNoTracking().OrderBy(a => a.Id).ToList();
    }

    [Fact]
    public void VendorUpdate_WritesOneRow_WithOldAndNewValue()
    {
        var env = Build();
        var seeded = SeedVendor(env, v => v.PriceFrom = 800);

        using (var db = new AppDbContext(env.Audited))
        {
            var v = db.Vendors.Single(x => x.Id == seeded.Id);
            v.PriceFrom = 950;
            db.SaveChanges();
        }

        var logs = Logs(env);
        var log = Assert.Single(logs);
        Assert.Equal("update", log.Action);
        Assert.Equal("vendor", log.EntityType);
        Assert.Equal(seeded.Id.ToString(), log.EntityId);
        var price = JsonDocument.Parse(log.Changes!).RootElement.GetProperty("PriceFrom");
        Assert.Equal(800, price.GetProperty("old").GetInt32());
        Assert.Equal(950, price.GetProperty("new").GetInt32());
        Assert.Equal("system", log.ActorType); // nema HTTP zahtjeva ni Set(...)
    }

    [Fact]
    public void ActorSetExplicitly_IsStampedOnTheRow()
    {
        var env = Build();
        var seeded = SeedVendor(env, v => v.PriceFrom = 800);
        var adminId = Guid.NewGuid();

        using (env.Actor.Set("admin", adminId, "test:admin"))
        using (var db = new AppDbContext(env.Audited))
        {
            db.Vendors.Single(x => x.Id == seeded.Id).PriceFrom = 900;
            db.SaveChanges();
        }

        var log = Assert.Single(Logs(env));
        Assert.Equal("admin", log.ActorType);
        Assert.Equal(adminId, log.ActorUserId);
        Assert.Equal("test:admin", log.Source);
        Assert.True((DateTime.UtcNow - log.OccurredAt).TotalMinutes < 5);
    }

    [Fact]
    public void ContactFieldChange_IsMasked()
    {
        var env = Build();
        var seeded = SeedVendor(env, v => v.Phone = "021111222");

        using (var db = new AppDbContext(env.Audited))
        {
            db.Vendors.Single(x => x.Id == seeded.Id).Phone = "021999888";
            db.SaveChanges();
        }

        var log = Assert.Single(Logs(env));
        Assert.True(JsonDocument.Parse(log.Changes!).RootElement.GetProperty("Phone").GetProperty("changed").GetBoolean());
        Assert.DoesNotContain("021111222", log.Changes!);
        Assert.DoesNotContain("021999888", log.Changes!);
    }

    [Fact]
    public void UntrackedEntity_WritesNothing()
    {
        var env = Build();

        using (var db = new AppDbContext(env.Audited))
        {
            db.Favorites.Add(new Favorite { UserId = Guid.NewGuid(), VendorId = Guid.NewGuid() });
            db.SaveChanges();
        }

        Assert.Empty(Logs(env));
    }

    [Fact]
    public void UpdatedAtOnlyChange_WritesNothing()
    {
        var env = Build();
        var seeded = SeedVendor(env);

        using (var db = new AppDbContext(env.Audited))
        {
            db.Vendors.Single(x => x.Id == seeded.Id).UpdatedAt = DateTime.UtcNow.AddMinutes(10);
            db.SaveChanges();
        }

        Assert.Empty(Logs(env));
    }

    [Fact]
    public void ListReplacedWithSameContent_WritesNothing()
    {
        var env = Build();
        var seeded = SeedVendor(env, v => v.Services = new List<string> { "Vjenčanja", "Zaruke" });

        using (var db = new AppDbContext(env.Audited))
        {
            var v = db.Vendors.Single(x => x.Id == seeded.Id);
            v.Services = new List<string> { "Vjenčanja", "Zaruke" }; // nova lista, isti sadržaj (kao ApplyToVendor)
            db.SaveChanges();
        }

        Assert.Empty(Logs(env));
    }

    [Fact]
    public void OptOutChange_UsesOptoutAction()
    {
        var env = Build();
        var seeded = SeedVendor(env);

        using (var db = new AppDbContext(env.Audited))
        {
            db.Vendors.Single(x => x.Id == seeded.Id).OptOut = true;
            db.SaveChanges();
        }
        using (env.Actor.Set("admin", Guid.NewGuid(), "test"))
        using (var db = new AppDbContext(env.Audited))
        {
            db.Vendors.Single(x => x.Id == seeded.Id).OptOut = false;
            db.SaveChanges();
        }

        var logs = Logs(env);
        Assert.Equal(2, logs.Count);
        Assert.Equal("optout", logs[0].Action);
        Assert.Equal("optout_restored", logs[1].Action);
        Assert.Equal("admin", logs[1].ActorType);
    }

    [Fact]
    public void NewVendor_WritesCreateRow_WithMaskedContacts()
    {
        var env = Build();

        Guid id;
        using (var db = new AppDbContext(env.Audited))
        {
            var v = new Vendor { Slug = "novi", Name = "Novi", CategorySlug = "foto-i-video", RegionSlug = "istra", Phone = "052123456" };
            db.Vendors.Add(v);
            db.SaveChanges();
            id = v.Id;
        }

        var log = Assert.Single(Logs(env));
        Assert.Equal("create", log.Action);
        Assert.Equal(id.ToString(), log.EntityId);
        Assert.True(JsonDocument.Parse(log.Changes!).RootElement.GetProperty("Phone").GetProperty("set").GetBoolean());
        Assert.DoesNotContain("052123456", log.Changes!);
    }

    [Fact]
    public void DeletedPhoto_IsLogged_WithVendorNote()
    {
        var env = Build();
        var vendor = SeedVendor(env);
        Guid photoId;
        using (var db = new AppDbContext(env.Plain))
        {
            var p = new VendorPhoto { VendorId = vendor.Id, StorageKey = "/uploads/x.webp" };
            db.VendorPhotos.Add(p);
            db.SaveChanges();
            photoId = p.Id;
        }

        using (var db = new AppDbContext(env.Audited))
        {
            db.VendorPhotos.Remove(db.VendorPhotos.Single(p => p.Id == photoId));
            db.SaveChanges();
        }

        var log = Assert.Single(Logs(env));
        Assert.Equal("delete", log.Action);
        Assert.Equal("vendor_photo", log.EntityType);
        Assert.Equal(photoId.ToString(), log.EntityId);
        Assert.Equal(AuditEntryBuilder.VendorNote(vendor.Id), log.Note);
        Assert.Equal("/uploads/x.webp", JsonDocument.Parse(log.Changes!).RootElement.GetProperty("StorageKey").GetString());
    }

    [Fact]
    public void AuditLogRows_AreNotAuditedThemselves()
    {
        // Dnevnik koji bilježi vlastite zapise bi se beskonačno množio.
        var env = Build();

        using (var db = new AppDbContext(env.Audited))
        {
            db.AuditLogs.Add(new AuditLog { ActorType = "system", EntityType = "user", EntityId = "x", Action = "account_deleted" });
            db.SaveChanges();
        }

        var log = Assert.Single(Logs(env)); // samo onaj koji smo izričito dodali
        Assert.Equal("account_deleted", log.Action);
    }

    [Fact]
    public void TwoChangesInOneSave_WriteTwoRows()
    {
        var env = Build();
        var a = SeedVendor(env, v => { v.Slug = "a"; v.PriceFrom = 100; });
        var b = SeedVendor(env, v => { v.Slug = "b"; v.PriceFrom = 200; });

        using (var db = new AppDbContext(env.Audited))
        {
            db.Vendors.Single(x => x.Id == a.Id).PriceFrom = 110;
            db.Vendors.Single(x => x.Id == b.Id).PriceFrom = 220;
            db.SaveChanges();
        }

        var logs = Logs(env);
        Assert.Equal(2, logs.Count);
        Assert.Equal(new[] { a.Id.ToString(), b.Id.ToString() }.OrderBy(x => x), logs.Select(l => l.EntityId).OrderBy(x => x));
    }
}
