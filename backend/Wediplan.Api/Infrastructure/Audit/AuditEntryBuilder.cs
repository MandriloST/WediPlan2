using System.Collections;
using System.Text.Encodings.Web;
using System.Text.Json;
using Wediplan.Api.Domain;

namespace Wediplan.Api.Infrastructure.Audit;

/// <summary>Vrijednost svojstva novog entiteta (za <see cref="AuditEntryBuilder.ForAdded"/>).</summary>
public readonly record struct PropValue(string Name, object? Value);

/// <summary>Staro i novo stanje svojstva izmijenjenog entiteta (za <see cref="AuditEntryBuilder.ForModified"/>).</summary>
public readonly record struct PropChange(string Name, object? Original, object? Current);

/// <summary>
/// Gradi <see cref="AuditLog"/> zapise iz već izvučenih vrijednosti svojstava (Zadatak 14). Čista logika bez EF-a:
/// presuđuje što ulazi u dnevnik (ignorirana/maskirana polja, usporedba po sadržaju), bira akciju
/// (<c>create/update/delete/optout/optout_restored</c>) i serijalizira <c>Changes</c> u JSON.
/// Akter, vrijeme i izvor zapisa dopunjuje <see cref="AuditSaveChangesInterceptor"/>.
/// <para><b>Format <c>Changes</c></b> (nazivi su C# imena svojstava, npr. <c>PriceFrom</c>):
/// izmjena <c>{"PriceFrom":{"old":800,"new":950}}</c>; maskirano polje <c>{"Phone":{"changed":true}}</c>;
/// novi entitet <c>{"Name":"…","Phone":{"set":true}}</c>.</para>
/// <para>Za podređene entitete (slika, recenzije, claim) <c>Note = "vendorId:&lt;guid&gt;"</c> — tako admin pregled
/// nađe i zapise o obrisanim entitetima.</para>
/// </summary>
public static class AuditEntryBuilder
{
    /// <summary>Dulji tekstovi (npr. opis) se u dnevniku skraćuju; točan sadržaj je u backupu.</summary>
    public const int MaxStringLength = 1000;

    private static readonly JsonSerializerOptions Json = new()
    {
        // Hrvatski znakovi ostaju čitljivi u bazi/adminu (ne \u010d).
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Vrijednost <c>audit_log.note</c> koja veže podređeni zapis uz pružatelja.</summary>
    public static string VendorNote(Guid vendorId) => $"vendorId:{vendorId}";

    public static AuditLog ForAdded(string entityType, string entityId, string? vendorId, IEnumerable<PropValue> values)
    {
        var d = new Dictionary<string, object?>();
        foreach (var v in values)
        {
            // Ključ je u EntityId, a FK pružatelja u Note — ne ponavljamo ih u Changes.
            if (v.Name == "Id" || (v.Name == "VendorId" && vendorId != null)) continue;
            if (AuditRules.IsIgnored(entityType, v.Name) || v.Value is null) continue;
            if (v.Value is IEnumerable en && v.Value is not string && !en.Cast<object?>().Any()) continue;
            d[v.Name] = AuditRules.IsMasked(entityType, v.Name)
                ? new Dictionary<string, object?> { ["set"] = true }
                : Shorten(v.Value);
        }
        return Make("create", entityType, entityId, vendorId, d.Count == 0 ? null : Serialize(d));
    }

    /// <summary>Vraća <c>null</c> kad nema relevantne promjene (npr. samo <c>UpdatedAt</c> ili lista istog sadržaja).</summary>
    public static AuditLog? ForModified(string entityType, string entityId, string? vendorId, IEnumerable<PropChange> changes)
    {
        var d = new Dictionary<string, object?>();
        var optOutChanged = false;
        object? optOutNew = null;
        foreach (var c in changes)
        {
            if (AuditRules.IsIgnored(entityType, c.Name)) continue;
            if (AuditRules.ValuesEqual(c.Original, c.Current)) continue;
            if (entityType == "vendor" && c.Name == "OptOut") { optOutChanged = true; optOutNew = c.Current; }
            d[c.Name] = AuditRules.IsMasked(entityType, c.Name)
                ? new Dictionary<string, object?> { ["changed"] = true }
                : new Dictionary<string, object?> { ["old"] = Shorten(c.Original), ["new"] = Shorten(c.Current) };
        }
        if (d.Count == 0) return null;

        // GDPR opt-out ima vlastitu, razumljivu akciju. Razlog i kontakt se NE bilježe (ne postoje u modelu Vendor).
        var action = optOutChanged ? (optOutNew is true ? "optout" : "optout_restored") : "update";
        return Make(action, entityType, entityId, vendorId, Serialize(d));
    }

    public static AuditLog ForDeleted(string entityType, string entityId, string? vendorId, IEnumerable<PropValue> values)
    {
        var d = new Dictionary<string, object?>();
        foreach (var v in values)
            if (v.Value is not null && AuditRules.KeepOnDelete(entityType, v.Name))
                d[v.Name] = Shorten(v.Value);
        return Make("delete", entityType, entityId, vendorId, d.Count == 0 ? null : Serialize(d));
    }

    private static AuditLog Make(string action, string entityType, string entityId, string? vendorId, string? changes) => new()
    {
        Action = action,
        EntityType = entityType,
        EntityId = entityId,
        Changes = changes,
        Note = vendorId == null ? null : $"vendorId:{vendorId}",
    };

    private static object? Shorten(object? value) =>
        value is string s && s.Length > MaxStringLength ? s[..MaxStringLength] + "…" : value;

    private static string Serialize(Dictionary<string, object?> d) => JsonSerializer.Serialize(d, Json);
}
