namespace Wediplan.Api.Auth;

/// <summary>
/// Obavijesti partnerima (§Zadatak 7, PLAN-PRIORITETI-LANSIRANJE-2.md) — da ne moraju sami
/// osvježavati nadzornu ploču da vide je li claim odlučen ili je recenzija objavljena. Isti
/// obrazac kao <see cref="AuthEmails"/> (DI <see cref="IEmailSender"/>, link na FRONTEND).
/// <para>
/// Slanje je uvijek BEST-EFFORT sa strane pozivatelja — ove metode same ne gutaju iznimke
/// (ako mail padne, <see cref="IEmailSender.SendAsync"/> baca), nego pozivatelj (npr.
/// <c>ClaimApprovalService</c>, <c>AdminController</c>) omata poziv u try/catch NAKON
/// <c>SaveChanges</c> i nikad ne obara samu admin akciju zbog pada slanja.
/// </para>
/// </summary>
public class PartnerEmails
{
    private readonly IEmailSender _sender;
    private readonly string _appUrl;

    public PartnerEmails(IEmailSender sender, IConfiguration cfg)
    {
        _sender = sender;
        _appUrl = (cfg["App:PublicUrl"] ?? "http://localhost:3000").TrimEnd('/');
    }

    public Task SendClaimApproved(string email, string vendorName, CancellationToken ct)
    {
        var url = $"{_appUrl}/partner";
        return Send(email, "Profil je preuzet — Wediplan",
            $"Vaš zahtjev za preuzimanje profila „{vendorName}” je odobren. Profil je sada vaš — " +
            "uredite podatke, cijene i fotografije u nadzornoj ploči:", url, "Otvori nadzornu ploču", ct);
    }

    public Task SendClaimRejected(string email, string vendorName, CancellationToken ct)
    {
        var url = $"{_appUrl}/partner";
        return Send(email, "Zahtjev za preuzimanje nije odobren — Wediplan",
            $"Vaš zahtjev za preuzimanje profila „{vendorName}” nažalost nije odobren. Ako mislite " +
            "da je ovo greška ili imate dodatni dokaz vlasništva, javite nam se na support@wediplan.hr:",
            url, "Nadzorna ploča", ct);
    }

    public Task SendReviewPublished(string email, string vendorName, CancellationToken ct)
    {
        var url = $"{_appUrl}/partner";
        return Send(email, "Nova recenzija objavljena — Wediplan",
            $"Nova korisnička recenzija za „{vendorName}” je pregledana i objavljena na profilu:",
            url, "Pogledaj nadzornu ploču", ct);
    }

    /// <summary>
    /// Obavijest vlasniku da je jedna njegova fotografija skrivena s javnog profila (§Zadatak 15). Razlog je tekst koji je upisao
    /// admin — u HTML se ESCAPE-a (HtmlEncode), a u tekstualnoj varijanti ide kakav jest.
    /// </summary>
    public Task SendPhotoFlagged(string email, string vendorName, string reason, CancellationToken ct)
    {
        var url = $"{_appUrl}/partner";
        var plain = $"Jedna fotografija na profilu „{vendorName}” skrivena je iz javnog prikaza nakon pregleda. Razlog: {reason}";
        var html = $"Jedna fotografija na profilu „{System.Net.WebUtility.HtmlEncode(vendorName)}” skrivena je iz javnog prikaza nakon pregleda. " +
                   $"Razlog: {System.Net.WebUtility.HtmlEncode(reason)}";
        return Send(email, "Fotografija je skrivena — Wediplan", html, url, "Pogledaj nadzornu ploču", ct, plain);
    }

    private Task Send(string email, string subject, string intro, string url, string cta, CancellationToken ct, string? textIntro = null)
    {
        var html = $@"<div style=""font-family:system-ui,sans-serif;max-width:480px;margin:0 auto"">
  <h2 style=""color:#c2410c"">Wediplan</h2>
  <p>{intro}</p>
  <p><a href=""{url}"" style=""display:inline-block;background:#c2410c;color:#fff;padding:12px 22px;border-radius:8px;text-decoration:none;font-weight:600"">{cta}</a></p>
  <p style=""color:#999;font-size:12px;word-break:break-all"">Poveznica: {url}</p>
</div>";
        var text = $"{textIntro ?? intro}\n\n{url}";
        return _sender.SendAsync(email, subject, html, text, ct);
    }
}
