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

// ---------------------------------------------------------------- couple podaci (Faza 3 kraj)

/// <summary>Budžetski plan (ulazi; caps se izvode). null total/guests = nema plana.</summary>
public record BudgetPlanDto(int Guests, string Region, int Total);

/// <summary>Merge localStorage → account nakon prijave. Sve opcionalno (može biti samo jedno).</summary>
public record MergeRequest(
    IReadOnlyList<string>? FavoriteIds,
    BudgetPlanDto? Plan);

/// <summary>Odgovor /api/favorites i /api/me couple dijela: ID-jevi favorita + plan.</summary>
public record CoupleDataDto(
    IReadOnlyList<string> FavoriteIds,
    BudgetPlanDto? Plan);
