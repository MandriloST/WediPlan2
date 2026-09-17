namespace Wediplan.Api.Auth;

/// <summary>Sastavlja i šalje auth e-mailove (magic link, verifikacija, reset). HR tekst.</summary>
public class AuthEmails
{
    private readonly IEmailSender _sender;
    private readonly string _appUrl;

    public AuthEmails(IEmailSender sender, IConfiguration cfg)
    {
        _sender = sender;
        // Odredište linkova = FRONTEND (ne API). Frontend rutu prosljeđuje API-ju.
        _appUrl = (cfg["App:PublicUrl"] ?? "http://localhost:3000").TrimEnd('/');
    }

    public Task SendMagicLink(string email, string rawToken, CancellationToken ct)
    {
        var url = $"{_appUrl}/prijava/link?token={rawToken}";
        return Send(email, "Vaša Wediplan poveznica za prijavu",
            $"Prijavite se klikom na poveznicu (vrijedi 15 minuta):", url, "Prijava", ct);
    }

    public Task SendVerification(string email, string rawToken, CancellationToken ct)
    {
        var url = $"{_appUrl}/prijava/potvrda?token={rawToken}";
        return Send(email, "Potvrdite svoju Wediplan adresu",
            "Potvrdite e-mail adresu klikom na poveznicu (vrijedi 24 sata):", url, "Potvrdi e-mail", ct);
    }

    public Task SendPasswordReset(string email, string rawToken, CancellationToken ct)
    {
        // Identity reset token može sadržavati znakove koji nisu URL-safe → encode.
        var url = $"{_appUrl}/prijava/reset?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(rawToken)}";
        return Send(email, "Ponovno postavljanje lozinke — Wediplan",
            "Zatražili ste novu lozinku. Postavite je klikom (poveznica vrijedi 1 sat):", url, "Nova lozinka", ct);
    }

    private Task Send(string email, string subject, string intro, string url, string cta, CancellationToken ct)
    {
        var html = $@"<div style=""font-family:system-ui,sans-serif;max-width:480px;margin:0 auto"">
  <h2 style=""color:#c2410c"">Wediplan</h2>
  <p>{intro}</p>
  <p><a href=""{url}"" style=""display:inline-block;background:#c2410c;color:#fff;padding:12px 22px;border-radius:8px;text-decoration:none;font-weight:600"">{cta}</a></p>
  <p style=""color:#666;font-size:13px"">Ako niste vi zatražili ovaj e-mail, slobodno ga zanemarite.</p>
  <p style=""color:#999;font-size:12px;word-break:break-all"">Poveznica: {url}</p>
</div>";
        var text = $"{intro}\n\n{url}\n\nAko niste vi zatražili ovaj e-mail, zanemarite ga.";
        return _sender.SendAsync(email, subject, html, text, ct);
    }
}
