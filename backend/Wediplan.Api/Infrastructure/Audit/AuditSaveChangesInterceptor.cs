using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Wediplan.Api.Domain;

namespace Wediplan.Api.Infrastructure.Audit;

/// <summary>
/// Automatski dnevnik promjena (Zadatak 14): pri svakom <c>SaveChanges</c> prođe praćene entitete
/// (<see cref="AuditRules"/>) i u ISTI context doda <see cref="AuditLog"/> zapise — pa idu u istu transakciju kao
/// sama promjena (nema promjene bez zapisa ni zapisa bez promjene). Ne mijenja nijedan kontroler.
/// <para>Singleton (v. <see cref="IAuditContext"/>). Što ulazi u zapis i kako — <see cref="AuditEntryBuilder"/>.</para>
/// <para><b>Ne vidi</b> pisanja mimo ChangeTrackera (<c>ExecuteUpdate/ExecuteDelete</c>, DB cascade) — za njih su
/// eksplicitni zapisi (npr. <c>AccountController.Delete</c>).</para>
/// </summary>
public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IAuditContext _audit;

    public AuditSaveChangesInterceptor(IAuditContext audit) => _audit = audit;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Capture(DbContext? context)
    {
        if (context == null) return;

        // ToList(): ispod se u context dodaju novi entiteti (AuditLog), a kolekciju ne smijemo mijenjati dok je prolazimo.
        // Entries() usput pokreće DetectChanges, pa vidimo i promjene koje kontroler nije izričito označio.
        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();
        if (entries.Count == 0) return;

        var logs = new List<AuditLog>();
        foreach (var e in entries)
        {
            var entityType = AuditRules.EntityTypeOf(e.Entity.GetType());
            if (entityType == null) continue; // netraćen tip (uklj. sam AuditLog → nema rekurzije)

            var key = e.Metadata.FindPrimaryKey();
            var entityId = key == null ? "" : string.Join("|", key.Properties.Select(p => e.Property(p.Name).CurrentValue ?? ""));

            // Podređeni entiteti (slika, recenzije, claim) nose id pružatelja u Note, da ih admin nađe i nakon brisanja.
            string? vendorId = null;
            if (entityType != "vendor" && e.Metadata.FindProperty("VendorId") != null)
                vendorId = e.Property("VendorId").CurrentValue?.ToString();

            AuditLog? log = e.State switch
            {
                EntityState.Added => AuditEntryBuilder.ForAdded(entityType, entityId, vendorId,
                    e.Properties.Select(p => new PropValue(p.Metadata.Name, p.CurrentValue))),
                EntityState.Modified => AuditEntryBuilder.ForModified(entityType, entityId, vendorId,
                    e.Properties.Where(p => p.IsModified)
                        .Select(p => new PropChange(p.Metadata.Name, p.OriginalValue, p.CurrentValue))),
                EntityState.Deleted => AuditEntryBuilder.ForDeleted(entityType, entityId, vendorId,
                    e.Properties.Select(p => new PropValue(p.Metadata.Name, p.CurrentValue))),
                _ => null,
            };
            if (log == null) continue;

            log.OccurredAt = DateTime.UtcNow;
            log.ActorType = _audit.ActorType;
            log.ActorUserId = _audit.ActorUserId;
            log.Source = _audit.Source;
            logs.Add(log);
        }

        if (logs.Count > 0) context.Set<AuditLog>().AddRange(logs);
    }
}
