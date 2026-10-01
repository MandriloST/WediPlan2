namespace Wediplan.Api.Infrastructure.Audit;

/// <summary>
/// TKO i ODAKLE piše u bazu — podatak koji interceptor stavlja na svaki zapis dnevnika (Zadatak 14).
/// <para>Vrijednosti se izvode iz trenutnog HTTP zahtjeva (nema ga → <c>system</c>; anoniman → <c>public</c>;
/// admin → <c>admin</c>; pružatelj → <c>partner</c>; ostali prijavljeni → <c>user</c>), osim ako ih netko izričito
/// postavi preko <see cref="Set"/> (CLI import, pozadinski poslovi).</para>
/// <para>Registrirano kao SINGLETON namjerno: interceptor mora biti jedna ista instanca, inače EF za svaki zahtjev
/// gradi novi interni servisni provider (<c>ManyServiceProvidersCreatedWarning</c>). Zato stanje po zahtjevu
/// ne drži instanca nego <c>HttpContext</c> / <c>AsyncLocal</c>.</para>
/// </summary>
public interface IAuditContext
{
    string ActorType { get; }
    Guid? ActorUserId { get; }
    string? Source { get; }

    /// <summary>
    /// Izričito postavi aktera za TIJEK IZVRŠAVANJA koji slijedi (async-safe). Vraća scope koji pri
    /// <c>Dispose</c> vraća prethodno stanje — uvijek koristi <c>using var _ = ctx.Set(…)</c>.
    /// </summary>
    IDisposable Set(string actorType, Guid? userId, string? source);
}
