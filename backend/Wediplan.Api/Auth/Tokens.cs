using System.Security.Cryptography;

namespace Wediplan.Api.Auth;

/// <summary>
/// Sigurni jednokratni tokeni za magic-link i email-verifikaciju.
/// Sirovi token ide u URL/email; u bazu ide SAMO SHA-256 hash (procuri li baza, tokeni su
/// beskorisni). Usporedba je po hashu, pa je i lookup indeksiran.
/// </summary>
public static class Tokens
{
    /// <summary>Sirovi token: 32 slučajna bajta, base64url (bez paddinga) — URL-safe.</summary>
    public static string NewRaw()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static string Hash(string raw)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes); // stabilan, case-insensitive hex
    }
}
