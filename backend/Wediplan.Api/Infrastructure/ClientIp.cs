namespace Wediplan.Api.Infrastructure;

/// <summary>
/// IP klijenta za rate limiting (Faza 2).
///
/// Frontend zove API kroz Next.js rewrite na Vercelu, pa je RemoteIpAddress uvijek
/// Vercelov server → bez ovoga bi SVI posjetitelji dijelili isti limit.
/// Vercel postavlja (i prepisuje, ne nadovezuje) X-Forwarded-For stvarnim IP-om klijenta.
///
/// Uključuje se konfiguracijom <c>Proxy:TrustForwardedFor=true</c> (env
/// <c>Proxy__TrustForwardedFor=true</c>). Default je false jer je zaglavlje lažljivo ako je
/// API izravno dostupan s interneta — u produkciji (Faza 5) API mora primati promet samo
/// preko Cloudflarea/Vercela. IP se koristi samo u memoriji i NIKAD se ne pohranjuje (§A).
/// </summary>
public static class ClientIp
{
    public static string Get(HttpContext ctx)
    {
        var cfg = ctx.RequestServices.GetService<IConfiguration>();
        if (cfg?.GetValue<bool>("Proxy:TrustForwardedFor") == true)
        {
            var xff = ctx.Request.Headers["X-Forwarded-For"].ToString();
            if (!string.IsNullOrWhiteSpace(xff))
            {
                var first = xff.Split(',')[0].Trim();
                if (first.Length is > 0 and <= 64) return first;
            }
        }
        return ctx.Connection.RemoteIpAddress?.ToString() ?? "?";
    }
}
