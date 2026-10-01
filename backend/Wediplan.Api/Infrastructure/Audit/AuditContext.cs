using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Wediplan.Api.Domain;

namespace Wediplan.Api.Infrastructure.Audit;

/// <inheritdoc cref="IAuditContext"/>
public sealed class AuditContext : IAuditContext
{
    private sealed record Actor(string Type, Guid? UserId, string? Source);

    // Ambijentni "override" (CLI import i sl.). AsyncLocal teče kroz await-ove, a Set vraća scope koji ga
    // vraća na staro — pa ni paralelni testovi ni zahtjevi ne vide tuđeg aktera.
    private static readonly AsyncLocal<Actor?> Override = new();

    private readonly IHttpContextAccessor? _http;

    public AuditContext(IHttpContextAccessor? http = null) => _http = http;

    public string ActorType => Current().Type;
    public Guid? ActorUserId => Current().UserId;
    public string? Source => Current().Source;

    public IDisposable Set(string actorType, Guid? userId, string? source)
    {
        var previous = Override.Value;
        Override.Value = new Actor(actorType, userId, Trim(source));
        return new Restore(previous);
    }

    private Actor Current()
    {
        var o = Override.Value;
        if (o != null) return o;

        var http = _http?.HttpContext;
        if (http == null) return new Actor("system", null, null);

        var source = Trim($"api:{http.Request.Method} {http.Request.Path}");
        var user = http.User;
        if (user?.Identity?.IsAuthenticated != true) return new Actor("public", null, source);

        Guid? id = Guid.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var g) ? g : null;
        var type = user.IsInRole(Roles.Admin) ? "admin"
                 : user.IsInRole(Roles.Provider) ? "partner"
                 : "user";
        return new Actor(type, id, source);
    }

    private static string? Trim(string? s) => s is { Length: > 200 } ? s[..200] : s;

    private sealed class Restore : IDisposable
    {
        private readonly Actor? _previous;
        private bool _done;
        public Restore(Actor? previous) => _previous = previous;
        public void Dispose()
        {
            if (_done) return;
            _done = true;
            Override.Value = _previous;
        }
    }
}
