namespace Wediplan.Api.Auth;

/// <summary>
/// DEV fallback kad Resend ključ nije postavljen: ispiše email (uklj. magic/verify link) u
/// konzolu servera umjesto slanja. Omogućuje potpuni auth tok lokalno bez vanjskog servisa.
/// NIKAD se ne bira u produkciji ako je ključ postavljen.
/// </summary>
public class ConsoleEmailSender : IEmailSender
{
    private readonly ILogger<ConsoleEmailSender> _log;
    public ConsoleEmailSender(ILogger<ConsoleEmailSender> log) => _log = log;

    public Task SendAsync(string to, string subject, string html, string text, CancellationToken ct = default)
    {
        _log.LogWarning("─── DEV EMAIL (Resend ključ nije postavljen) ───\nTo: {To}\nSubject: {Subject}\n{Text}\n────────────────────────────────",
            to, subject, text);
        return Task.CompletedTask;
    }
}
