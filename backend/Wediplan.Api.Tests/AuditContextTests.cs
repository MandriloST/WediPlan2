using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Wediplan.Api.Domain;
using Wediplan.Api.Infrastructure.Audit;
using Xunit;
using SecClaim = System.Security.Claims.Claim; // 'Claim' je dvosmislen: Wediplan.Api.Domain.Claim vs System.Security.Claims.Claim

namespace Wediplan.Api.Tests;

/// <summary>§Zadatak 14 — <see cref="AuditContext"/>: tko je akter (iz HTTP zahtjeva ili izričito postavljen).</summary>
public class AuditContextTests
{
    private static AuditContext WithRequest(string method, string path, params SecClaim[]? claims)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = method;
        ctx.Request.Path = path;
        if (claims is { Length: > 0 }) // bez claimova = anoniman zahtjev (prazan params niz NIJE anoniman)
            ctx.User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));
        return new AuditContext(new HttpContextAccessor { HttpContext = ctx });
    }

    [Fact]
    public void NoHttpContext_IsSystem()
    {
        var a = new AuditContext();
        Assert.Equal("system", a.ActorType);
        Assert.Null(a.ActorUserId);
        Assert.Null(a.Source);
    }

    [Fact]
    public void AnonymousRequest_IsPublic_WithSource()
    {
        var a = WithRequest("POST", "/api/optout", claims: null);
        Assert.Equal("public", a.ActorType);
        Assert.Null(a.ActorUserId);
        Assert.Equal("api:POST /api/optout", a.Source);
    }

    [Fact]
    public void AdminRole_IsAdmin_WithUserId()
    {
        var id = Guid.NewGuid();
        var a = WithRequest("POST", "/api/admin/vendors/x/restore-optout",
            new SecClaim(ClaimTypes.NameIdentifier, id.ToString()), new SecClaim(ClaimTypes.Role, Roles.Admin));
        Assert.Equal("admin", a.ActorType);
        Assert.Equal(id, a.ActorUserId);
    }

    [Fact]
    public void ProviderRole_IsPartner()
    {
        var a = WithRequest("PUT", "/api/provider/vendors/x/profile",
            new SecClaim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new SecClaim(ClaimTypes.Role, Roles.Provider));
        Assert.Equal("partner", a.ActorType);
    }

    [Fact]
    public void AdminWinsOverProvider_WhenBothRoles()
    {
        var a = WithRequest("GET", "/api/x", new SecClaim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new SecClaim(ClaimTypes.Role, Roles.Provider), new SecClaim(ClaimTypes.Role, Roles.Admin));
        Assert.Equal("admin", a.ActorType);
    }

    [Fact]
    public void AuthenticatedWithoutSpecialRole_IsUser()
    {
        var a = WithRequest("DELETE", "/api/account",
            new SecClaim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new SecClaim(ClaimTypes.Role, Roles.Couple));
        Assert.Equal("user", a.ActorType);
    }

    [Fact]
    public void Set_OverridesRequest_AndDisposeRestores()
    {
        var a = WithRequest("GET", "/api/x");
        var uid = Guid.NewGuid();
        using (a.Set("import", uid, "import:vendors.xlsx"))
        {
            Assert.Equal("import", a.ActorType);
            Assert.Equal(uid, a.ActorUserId);
            Assert.Equal("import:vendors.xlsx", a.Source);
        }
        Assert.Equal("public", a.ActorType); // vraćeno na stanje iz zahtjeva
        Assert.Equal("api:GET /api/x", a.Source);
    }

    [Fact]
    public void Set_Nested_RestoresPreviousOverride()
    {
        var a = new AuditContext();
        using (a.Set("import", null, "outer"))
        {
            using (a.Set("admin", null, "inner")) Assert.Equal("admin", a.ActorType);
            Assert.Equal("import", a.ActorType);
        }
        Assert.Equal("system", a.ActorType);
    }

    [Fact]
    public void LongSource_IsTrimmedTo200()
    {
        var a = new AuditContext();
        using var _ = a.Set("import", null, new string('x', 500));
        Assert.Equal(200, a.Source!.Length);
    }

    [Fact]
    public void RolesUsed_MatchProjectConstants()
    {
        // čuvar: ako se nazivi uloga promijene, audit akteri moraju ostati usklađeni
        Assert.Equal("admin", Roles.Admin);
        Assert.Equal("provider", Roles.Provider);
    }
}
