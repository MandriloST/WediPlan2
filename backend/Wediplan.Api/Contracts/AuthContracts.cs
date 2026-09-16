using System.ComponentModel.DataAnnotations;

namespace Wediplan.Api.Contracts;

// Ulazni DTO-ovi (validacija atributima; kontroler dodatno normalizira email na lowercase).

public record RegisterRequest(
    [property: Required, EmailAddress, MaxLength(320)] string Email,
    [property: Required, MinLength(8), MaxLength(128)] string Password,
    [property: MaxLength(80)] string? DisplayName);

public record LoginRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required] string Password);

public record EmailOnlyRequest([property: Required, EmailAddress, MaxLength(320)] string Email);

public record MagicConsumeRequest([property: Required] string Token);

public record VerifyEmailRequest([property: Required] string Token);

public record ResetPasswordRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required] string Token,
    [property: Required, MinLength(8), MaxLength(128)] string Password);

// Izlazni DTO — trenutni korisnik (/api/me i nakon prijave). NIKAD ne vraća hash/tokene.
public record MeDto(
    string Id,
    string Email,
    string? DisplayName,
    bool EmailConfirmed,
    IReadOnlyList<string> Roles);
