using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Wediplan.Api.Auth;

/// <summary>
/// Email preko Resend API-ja (§5, odobreno #5). Konfiguracija:
///   Email:Provider = "resend"
///   Email:ResendApiKey = "re_..."   (env: Email__ResendApiKey — NIKAD u git)
///   Email:From = "Wediplan <noreloy@wediplan.hr>"
/// Ako ključ nije postavljen, DI bira ConsoleEmailSender (dev) — v. Program.cs.
/// </summary>
public class ResendEmailSender : IEmailSender
{
    private readonly HttpClient _http;
    private readonly string _from;
    private readonly ILogger<ResendEmailSender> _log;

    public ResendEmailSender(IHttpClientFactory factory, IConfiguration cfg, ILogger<ResendEmailSender> log)
    {
        _http = factory.CreateClient("resend");
        _http.BaseAddress = new Uri("https://api.resend.com/");
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", cfg["Email:ResendApiKey"]);
        _from = cfg["Email:From"] ?? "Wediplan <onboarding@resend.dev>";
        _log = log;
    }

    public async Task SendAsync(string to, string subject, string html, string text, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new { from = _from, to = new[] { to }, subject, html, text });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync("emails", content, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            // Ne otkrivamo korisniku detalje; logiramo za ops. Auth tok tretira grešku slanja
            // kao "nismo mogli poslati" (v. AuthController).
            _log.LogError("Resend slanje nije uspjelo ({Status}): {Body}", (int)resp.StatusCode, body);
            throw new InvalidOperationException("email_send_failed");
        }
    }
}
