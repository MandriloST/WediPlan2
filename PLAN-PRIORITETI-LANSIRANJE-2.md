# Plan: 5 stavki drugog vala (SEO, claim-verifikacija, obavijesti, rate-limit, monitoring)

> ✅ **PLAN PROVEDEN U CIJELOSTI (2026-09-23).** Svih 5 zadataka mergeano u `develop`,
> `dotnet build`+`dotnet test` i `npm run build` potvrđeni zeleno na vlasnikovom stroju.
> Detalji po zadatku i sljedeći koraci: v. `STANJE.md`, sesija "Drugi val prioriteta ZATVOREN".

> **Namjena:** ovaj dokument daješ Claude Sonnetu (ili drugom modelu) kao specifikaciju. Svaki zadatak je
> samostalan, ima točne datoteke, provjerene uzorke iz postojećeg koda kojih se treba držati, i kriterij
> „gotovo". Izvor istine je repo `WediPlan2` grana `develop`. **Prije koda pročitati** `STANJE.md`,
> `PLAN-ARHITEKTURA.md`, `API.md` (+ ovaj dokument). Ako se arhitektura mijenja → ažurirati `PLAN-ARHITEKTURA.md`.
>
> Ovaj plan je nastavak `PLAN-PRIORITETI-LANSIRANJE.md` (prvi val: CI+testovi, brisanje računa, recenzije uz
> potvrđen email, opt-out — **sve gotovo i lokalno potvrđeno**, `dotnet test` 4/4). Ove stavke (drugi val)
> **nisu launch-blockeri**; #6 i #5 nose najviše vrijednosti.
>
> **Redoslijed rada (odlučeno s vlasnikom):** Zadatak 6 (SEO) → Zadatak 5 (claim e-mail verifikacija) →
> Zadatak 7 (partner mailovi) → Zadatak 9 (rate-limit) → Zadatak 8 (Sentry).
> Obrazloženje: #6 je najveći „besplatni" akvizicijski dobitak, čisti frontend, nula rizika za shemu — ide prvi
> i može paralelno. #5 je jedina stavka koja dira shemu (migracija). #7 dijeli infrastrukturu obavijesti s #5, pa
> ide odmah poslije. #9 i #8 su neovisni, backend/ops.
>
> **Svaki zadatak = zasebna grana + zaseban PR/merge.** Ne miješati ih. Grane:
> `feat/seo-jsonld-og`, `feat/claim-email-verify`, `feat/partner-emails`, `feat/rate-limit-writes`, `feat/sentry`.

## Status
- [x] **Zadatak 6 — SEO: JSON-LD + OG slike.** (frontend, bez sheme)
- [x] **Zadatak 5 — Claim e-mail verifikacija (auto-approve, prekidač za admin).** (backend+frontend, +migracija)
- [x] **Zadatak 7 — E-mail obavijesti partnerima.** (backend, bez sheme)
- [x] **Zadatak 9 — Očvršćivanje rate-limitinga (writes/auth politike).** (backend, bez sheme)
- [x] **Zadatak 8 — Monitoring: Sentry (backend + frontend).** (backend+frontend+ops, bez sheme)

## Odluke potvrđene s vlasnikom (2026-09-23) — obvezujuće za izvedbu
1. **#5 auto-approve:** na `email_verified` claim se **automatski odobrava**. Zadržati **jednu konfiguracijsku
   sklopku** (`Claims:AutoApproveOnEmailVerify`, default `true`) kojom se ponašanje jednom izmjenom vraća na
   „samo jak dokaz + admin klik", bez daljnjih promjena koda.
2. **#5 privatnost:** korisniku se vraća **maskirani** hint adrese (`t***@domena.hr`), nikad puna `vendor.Email`.
3. **#5 fallback:** pružatelj bez `vendor.Email` na profilu → nema gumba za e-mail verifikaciju; ostaje
   `domain_match`/`""` + admin (kao dosad).
4. #6, #7, #8, #9 — izvedba po prijedlogu u ovom dokumentu (bez otvorenih pitanja).

---

## Kontekst koda (PROVJERENO u repou na `develop`, commit `689be00` — ne nagađati)

### E-mail infrastruktura (postoji, koristi se za #5 i #7)
- `backend/Wediplan.Api/Auth/IEmailSender.cs` — `Task SendAsync(string toEmail, string subject, string htmlBody, string textBody, CancellationToken ct = default)`.
- `backend/Wediplan.Api/Auth/AuthEmails.cs` — klasa, DI `IEmailSender _sender`, `_appUrl` iz `cfg["App:PublicUrl"] ?? "http://localhost:3000"` (odredište linkova = **frontend**). Metode `SendMagicLink`, `SendVerification`, `SendPasswordReset`; privatni `Send(email, subject, intro, url, cta, ct)` (gradi HR HTML+text). Registrirana `AddScoped<AuthEmails>()` u `Program.cs`.
- `backend/Wediplan.Api/Auth/Tokens.cs` — `Tokens.NewRaw()` (32 bajta, base64url, URL-safe), `Tokens.Hash(raw)` (SHA-256 hex). Obrazac: **sirovi token u mail/URL, u bazu ide SAMO hash**, lookup po hashu.
- `Program.cs`: `AddHttpClient()`; ako je `Email:ResendApiKey` postavljen → `ResendEmailSender` (singleton), inače `ConsoleEmailSender` (dev). Postojeći verifikacijski link ide na frontend rutu `/prijava/potvrda?token=` — **isti obrazac slijedi #5**.

### Claim tok (meta #5)
- `backend/Wediplan.Api/Controllers/ClaimsController.cs` — `[ApiController] [Route("api/claims")] [Authorize]`. DI: `AppDbContext _db`, `UserManager<AppUser> _users`. `Uid()` = `Guid.Parse(_users.GetUserId(User)!)`.
  - `POST /api/claims` (`Create([FromBody] ClaimRequest req)`): nađe vendor po slugu; 404 ako null/`!IsPublished`/`OptOut`; 409 `already_claimed` ako `vendor.ClaimStatus == "claimed"`; idempotentno vraća postojeći ne-rejected claim; `Evidence = "domain_match"` ako `ProviderMapper.EmailDomain(user.Email) == ProviderMapper.WebsiteDomain(vendor.Website)`, inače `""`; dodaje `Claim` ili resetira `rejected`; seeda `VendorDraft` (`ProviderMapper.SeedFromVendor`) ako ne postoji; `SaveChanges`; dodaje rolu `Roles.Provider` (idempotentno); vraća `ToDto`.
  - `GET /api/claims/mine`.
  - privatni `ToDto(Claim c, Vendor v)` → `ClaimDto(c.Id.ToString(), v.Slug, v.Name, c.Status, c.Evidence, c.CreatedAt)`.
- `backend/Wediplan.Api/Data/ProviderMapper.cs` — **static** klasa: `EmailDomain(string?)`, `WebsiteDomain(string?)`, `SeedFromVendor(Vendor)`, `ApplyToVendor(VendorDraft, Vendor)`, `ApplyDto`, itd.
- `backend/Wediplan.Api/Domain/ProviderEntities.cs` — `Claim { Guid Id = Guid.NewGuid(); Guid VendorId; Guid UserId; string Message=""; string Evidence=""; string Status="pending"; Guid? DecidedBy; DateTime? DecidedAt; DateTime CreatedAt=UtcNow; }`. (+ `UserReview`, `VendorDraft`, `Subscription`.)
- `backend/Wediplan.Api/Domain/Entities.cs` — `Vendor` ima `string? Website` (61), `string? Phone` (62), `string? Email` (63) — **interni, NIJE u javnom `VendorDto`**; `string ClaimStatus="unclaimed"` (68); `Guid? OwnerUserId` (69).
- `backend/Wediplan.Api/Controllers/AdminController.cs` — `[Authorize(Roles = Roles.Admin)] [Route("api/admin")]`. DI: `AppDbContext`, `UserManager<AppUser>`.
  - `ApproveClaim(Guid id)`: 409 `already_decided` ako `Status != "pending"`; primijeni draft (`ProviderMapper.ApplyToVendor`); `vendor.ClaimStatus="claimed"`; `vendor.OwnerUserId=claim.UserId`; `claim.Status="approved"` + `DecidedBy=Uid()` + `DecidedAt`; **odbije ostale pending claimove za istog vendora**; `SaveChanges`; `Ok(new { status = "approved" })`. **Ovu logiku #5 dijeli — v. refaktor niže.**
  - `RejectClaim`, `ApproveReview`, `RejectReview`, `optouts`, `restore-optout`, `publish`/`unpublish`.

### Ugovori (Contracts) — točna polja
- `backend/Wediplan.Api/Contracts/ProviderContracts.cs`:
  - `ClaimRequest([Required] string VendorSlug, [MaxLength(2000)] string? Message)`.
  - `ClaimDto(string Id, string VendorSlug, string VendorName, string Status, string Evidence, DateTime CreatedAt)` — komentar Evidence: `domain_match | ""` → **dodati `email_verified`**.
  - `AdminClaimDto(string Id, string VendorSlug, string VendorName, string UserEmail, string? UserDisplayName, string Message, string Evidence, string Status, DateTime CreatedAt)` — `Evidence` se prikazuje u admin UI-ju.
- `backend/Wediplan.Api/Contracts/Contracts.cs`:
  - `VendorDto(... string City, double? Lng, double? Lat, PriceDto Price, double Rating, int ReviewCount, bool Verified, ..., SocialDto? Social, string? ClaimStatus, IReadOnlyList<string>? Photos, string? Country)`. **BEZ** `Website`/`Phone`/`Email` (javni DTO nema kontakte). `SocialDto(string? Instagram, string? Facebook)`.
  - `VendorProfileDto(VendorDto Vendor, string About, IReadOnlyList<string> Services, IReadOnlyList<ImportedReviewDto> ImportedReviews, IReadOnlyList<UserReviewDto>? UserReviews)`.

### Frontend (meta #6, FE dio #5/#7)
- `app/pruzatelj/[slug]/page.tsx` — server komponenta. `revalidate = 300`, `dynamicParams = true`, `generateStaticParams()` → `[]`. `load = cache(slug => getProfile → withProfileDefaults)`. `generateMetadata` postoji (title, description, `alternates.canonical`) — **nema `openGraph`/`twitter`, nema JSON-LD**. Import: `getProfile`, `getSimilar` (`@/lib/api/server`), `withProfileDefaults` (`@/lib/profile`), `CATEGORY_BY_SLUG`, `homeLabel` (`@/lib/data`), `formatPrice` (`@/lib/format`).
- `app/layout.tsx` — `metadata` s `metadataBase: new URL(SITE_URL)`, title, description, manifest, icons. **Nema `openGraph`/`twitter`.**
- `lib/site.ts` — `SITE_URL = NEXT_PUBLIC_SITE_URL ?? (VERCEL_URL ? https://… ) ?? http://localhost:3000`.
- `lib/api/server.ts` — `getProfile(slug): Promise<VendorProfileData | null>` (`/api/vendors/{slug}`; mock fallback). Tip profila u `lib/types.ts`.
- `lib/types.ts` — tipovi `Vendor`, `UserReview`, `Claim`, `AdminClaim`, `VendorDraft` itd. **Točne nazive polja (npr. `photos`, `social`, `lat`/`lng`, `price`) provjeriti ovdje prije JSON-LD-a.**
- `lib/api/provider.ts` — `claimApi`/`providerApi`/`reviewApi`/`adminApi` + `providerMessage` (HR poruke za error kodove).
- `components/VendorProfile.tsx` — sadrži `ClaimPanel` + `ReviewForm`. `components/AdminPanel.tsx` (`/admin`). `components/ProviderDashboard.tsx` (`/partner`).
- `next.config.mjs` — ima uploads rewrite + CDN `remotePattern`. **Kod #8 wrap-ati, NE prepisivati.**

### Rate limiting / infrastruktura (meta #9, #8)
- `Program.cs` `AddRateLimiter`: `RejectionStatusCode = 429`; helper `static string Ip(HttpContext c) => c.Connection.RemoteIpAddress?.ToString() ?? "unknown"`. `GlobalLimiter` = `PartitionedRateLimiter` **fixed window 300/min po IP-u**. Politika `"lists"` = **60/min po IP-u**, primijenjena `[EnableRateLimiting("lists")]` na liste (vendors-list/pins/suggest). **Pisanja (reviews/claims/optout) idu samo pod global.**
- Middleware redoslijed: `if(trustProxy) UseForwardedHeaders();` → `UseCors` → `UseStaticFiles` → `UseRateLimiter` → `UseAuthentication` → `UseAuthorization` → `MapControllers`.
- `Proxy:TrustForwardedFor` (bool) upravlja `UseForwardedHeaders` (X-Forwarded-For/Proto; `KnownNetworks/Proxies` očišćeni). U produkciji iza Cloudflarea = `true`.
- `/api/health` (`MapGet`, `AllowAnonymous`) → 200 `{status:"ok"}` / 503 `{status:"db_down"}`.
- `public partial class Program {}` na dnu (za `WebApplicationFactory<Program>` u testovima). **Ne dirati.**

### Testovi / CI (mora ostati zeleno)
- `backend/Wediplan.Api.Tests/` — `HealthEndpointTests` (`WebApplicationFactory<Program>` diže cijeli `Program.cs`, EF **InMemory**, `GET /api/health` → 200), `ReviewsControllerTests` (EF InMemory, `UserManager` preko `AddIdentityCore`).
- `backend/Wediplan.Api/Data/AppDbContext.cs` — `OnModelCreating` ima granu `if (Database.IsNpgsql()) { … Postgres-only: HasPostgresExtension, GIN, generirani tsvector … }`, a pod ne-Npgsql providerom `e.Ignore(v => v.Search)`. Imena stupaca snake_case centralno. **Svaka nova Postgres-specifična konfiguracija (indeksi) MORA biti unutar `IsNpgsql()` grane** da InMemory testovi prođu.
- `.github/workflows/ci.yml` — backend (`restore`/`build -c Release`/`test`) + frontend (`npm ci`/`tsc --noEmit`/`npm run build`) na push/PR u `develop`.
- Zadnja migracija: `backend/Wediplan.Api/Migrations/20260918132812_Faza4`. Nova: `dotnet ef migrations add <Ime>` iz `backend/Wediplan.Api/`.

---

## Zadatak 6 — SEO: JSON-LD strukturirani podaci + OG slike

**Cilj:** za direktorij je Google glavni kanal akvizicije. Dodati (a) JSON-LD `LocalBusiness`/`Service` +
`BreadcrumbList` na profil, (b) `openGraph`/`twitter` metapodatke, (c) dinamičku per-profil OG sliku i branded
default za cijeli site. **Čisti frontend, bez backenda/migracije, nula rizika za shemu — zato ide prvi.**

**Datoteke:**
- `app/pruzatelj/[slug]/page.tsx` (izmjena) — u `generateMetadata` dodati `openGraph` (`title`, `description`,
  `url` = canonical, `type: "website"`, `locale: "hr_HR"`, `images`) i `twitter` (`card: "summary_large_image"`).
  U tijelu stranice renderirati `<script type="application/ld+json" dangerouslySetInnerHTML={{__html: JSON.stringify(jsonLd)}} />`
  (server komponenta — podatak je već dohvaćen preko `load`).
- `lib/jsonld.ts` (novo) — čiste funkcije `vendorJsonLd(profile, siteUrl)` i `breadcrumbJsonLd(vendor, siteUrl)`.
- `app/pruzatelj/[slug]/opengraph-image.tsx` (novo) — dinamička OG slika (`ImageResponse`): ime pružatelja,
  kategorija, grad, cijena; prava fotografija (`vendor.photos[0]`) kao pozadina kad postoji, inače branded
  gradijent. `size = { width: 1200, height: 630 }`, `contentType = "image/png"`.
- `app/opengraph-image.tsx` (novo) ILI statični `public/og.jpg` — branded default za cijeli site; referencirati u
  `app/layout.tsx` `metadata.openGraph.images` + `twitter`.
- `app/layout.tsx` (izmjena) — dodati `openGraph` (`siteName: "Wediplan"`, `locale: "hr_HR"`, `type: "website"`,
  default `images`) i `twitter`.

**Koraci:**
1. Provjeriti točne nazive polja u `lib/types.ts` (`Vendor`: `photos`, `social`, `lat`/`lng`, `price`, `city`,
   `region`, `rating`, `reviewCount`, `slug`, `name`, `category`).
2. `lib/jsonld.ts`:
   - `LocalBusiness` (dvorane/restorani imaju adresu+geo) + `Service`. Polja: `name`, `image` (`photos` →
     apsolutni URL preko `SITE_URL`/CDN baze), `url` (`{SITE_URL}/pruzatelj/{slug}`), `geo`
     (`{"@type":"GeoCoordinates", latitude, longitude}` samo ako `lat && lng`), `address`
     (`{"@type":"PostalAddress", addressLocality: city, addressCountry: "HR"}`), `priceRange` (mapiraj `price`
     → `€`/`€€`/`€€€` ili raspon), `sameAs` (`social.instagram`/`facebook` ako postoje).
   - **`aggregateRating` uključi SAMO kad `reviewCount > 0`** (Google ne dopušta lažni/prazni rating).
     `{"@type":"AggregateRating", ratingValue: rating, reviewCount}`.
   - `BreadcrumbList`: Naslovnica → kategorija (`/kategorije` ili `/[region]/[category]`) → pružatelj.
3. Ubaciti oba JSON-LD bloka na stranicu (jedan `<script>` s poljem `"@graph": [...]` ili dva zasebna).
4. OG slike: `ImageResponse` iz `next/og`. Apsolutni URL fotografije (remotePattern za CDN već postoji).
5. `openGraph`/`twitter` u `generateMetadata` i `layout.tsx`.

**Kriterij gotovo:** `npx tsc --noEmit` + `npm run build` čisti; `/pruzatelj/<slug>` u HTML-u sadrži valjani
`application/ld+json` (provjeriti u [Google Rich Results Test] i [Schema Markup Validator]); OG slika se generira
na `/pruzatelj/<slug>/opengraph-image`; `<head>` ima `og:*` i `twitter:*`; profil bez recenzija **nema**
`aggregateRating`. Ažuriran `STANJE.md`.

---

## Zadatak 5 — Claim e-mail verifikacija (jak dokaz vlasništva, auto-approve)

**Cilj:** zamijeniti „admin odlučuje na oko" jakim dokazom vlasništva. Pošalje se jednokratni token na
**`vendor.Email`** (službena adresa profila iz importa, javno se ne izlaže). Tko klikne link iz tog inboxa —
dokazao je kontrolu → `Evidence = "email_verified"` → **auto-approve** (uz sklopku za povratak na admin klik).

**Odluke (v. gore):** auto-approve default `true` (`Claims:AutoApproveOnEmailVerify`); maskirani hint adrese;
bez `vendor.Email` → nema gumba (fallback na dosadašnje). **Ovo je jedina stavka drugog vala koja dira shemu.**

### Backend

**Nova shema — `backend/Wediplan.Api/Domain/ProviderEntities.cs`:**
```csharp
/// <summary>Jednokratni token za dokaz vlasništva claima (§6, drugi val). Sirovi token ide u mail
/// na vendor.Email; u bazu SAMO hash (isti obrazac kao EmailVerificationToken).</summary>
public class ClaimVerificationToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClaimId { get; set; }                 // FK → Claim (cascade)
    public string TokenHash { get; set; } = "";       // SHA-256 hex (Tokens.Hash)
    public DateTime ExpiresAt { get; set; }           // UtcNow + 24h
    public DateTime? ConsumedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

**`backend/Wediplan.Api/Data/AppDbContext.cs`:** `DbSet<ClaimVerificationToken>`; mapiranje snake_case; FK na
`Claim` s `OnDelete(Cascade)` (claim se briše → token nestaje; korisnikovo brisanje računa već kaskadno briše
claim). **Indeks na `token_hash` staviti unutar `if (Database.IsNpgsql())` grane** (da InMemory testovi prođu;
običan `HasIndex` je OK i pod InMemory, ali drži se postojećeg obrasca — Postgres-specifično ide u guard).

**Refaktor zajedničke logike odobrenja — novi `backend/Wediplan.Api/Services/ClaimApprovalService.cs` (scoped):**
- Izvući logiku iz `AdminController.ApproveClaim` u `Task ApproveAsync(Claim claim, Vendor vendor, Guid? decidedBy, CancellationToken ct)`:
  primijeni draft (`ProviderMapper.ApplyToVendor`), `vendor.ClaimStatus="claimed"`, `vendor.OwnerUserId=claim.UserId`,
  `vendor.UpdatedAt`, `claim.Status="approved"`, `DecidedBy=decidedBy`, `DecidedAt`, odbij ostale pending claimove,
  `SaveChanges`. `decidedBy = null` označava sustavno (auto) odobrenje.
- **Ovdje se poziva i #7 obavijest** (v. Zadatak 7) — jedan izvor istine za nuspojave odobrenja.
- `AdminController.ApproveClaim` → tanki wrapper koji zove servis s `decidedBy = Uid()`.
- Registrirati `builder.Services.AddScoped<ClaimApprovalService>()` u `Program.cs`.

**`backend/Wediplan.Api/Controllers/ClaimsController.cs` — dva nova endpointa** (DI dodati `AuthEmails`,
`ClaimApprovalService`, `IConfiguration`):
1. `POST /api/claims/{id:guid}/send-verification` (`[Authorize]`, **`[EnableRateLimiting("writes")]`** — v. #9):
   - Nađi claim korisnika (`c.Id == id && c.UserId == Uid()`); 404 ako nema. 409 ako `claim.Status != "pending"`.
   - Nađi vendora; ako `string.IsNullOrWhiteSpace(vendor.Email)` → `400 { error = "no_email_on_file" }`.
   - Anti-zloupotreba: ograniči broj slanja po claimu (npr. max 3 nepotrošena tokena / 24 h) i cooldown (npr.
     zadnji token mlađi od 2 min → `429 { error = "too_many_requests" }`).
   - `raw = Tokens.NewRaw()`; spremi `ClaimVerificationToken { ClaimId, TokenHash = Tokens.Hash(raw), ExpiresAt = UtcNow.AddHours(24) }`.
   - `await _emails.SendClaimVerification(vendor.Email!, vendor.Name, raw, ct)`.
   - Vrati `200 { sentTo = Mask(vendor.Email!) }` gdje `Mask("test@domena.hr") => "t***@domena.hr"`
     (helper u kontroleru ili `ProviderMapper`).
2. `POST /api/claims/verify` (`[Authorize]`, `[EnableRateLimiting("writes")]`, tijelo `{ token }`):
   - `hash = Tokens.Hash(token)`; nađi token po hashu; 400 `invalid_token` ako nema / `ConsumedAt != null` /
     `ExpiresAt < UtcNow`.
   - Nađi claim; provjeri `claim.UserId == Uid()` (verificira prijavljeni korisnik koji je i vlasnik claima).
   - `token.ConsumedAt = UtcNow`; `claim.Evidence = "email_verified"`.
   - Ako `_cfg.GetValue("Claims:AutoApproveOnEmailVerify", true)` i `claim.Status == "pending"`:
     `await _approval.ApproveAsync(claim, vendor, decidedBy: null, ct)` → vrati `200 { status = "approved" }`.
     Inače `SaveChanges` i vrati `200 { status = "verified" }` (jak dokaz, admin i dalje klikne).
- **Link u mailu ide na frontend rutu** `/{App:PublicUrl}/partner/potvrda-vlasnistva?token={raw}` (isti obrazac
  kao postojeći `/prijava/potvrda`). Frontend na klik gumba zove `POST /api/claims/verify` — **GET link ne
  troši token** (izbjegava auto-consume mail-skenera).

**`backend/Wediplan.Api/Auth/AuthEmails.cs`:** dodati
```csharp
public Task SendClaimVerification(string vendorEmail, string vendorName, string rawToken, CancellationToken ct)
{
    var url = $"{_appUrl}/partner/potvrda-vlasnistva?token={rawToken}";
    return Send(vendorEmail, "Potvrda vlasništva profila — Wediplan",
        $"Netko je zatražio preuzimanje profila „{vendorName}” na Wediplanu. Ako ste to vi, potvrdite " +
        $"vlasništvo klikom (poveznica vrijedi 24 sata):", url, "Potvrdi vlasništvo", ct);
}
```

**`backend/Wediplan.Api/Contracts/ProviderContracts.cs`:** novi `record VerifyClaimRequest(string Token)`;
u komentaru `ClaimDto.Evidence` dodati `email_verified`.

**Migracija:** `dotnet ef migrations add ClaimVerification` iz `backend/Wediplan.Api/`. Spomenuti je u PR opisu.

### Frontend
- `lib/api/provider.ts` — `claimApi.sendVerification(claimId)` (`POST /api/claims/{id}/send-verification`) i
  `claimApi.verify(token)` (`POST /api/claims/verify`, `{token}`); dodati HR poruke u `providerMessage` za
  `no_email_on_file` („Za ovaj profil nemamo e-mail adresu — preuzimanje odobrava administrator."),
  `too_many_requests`, `invalid_token`.
- `components/VendorProfile.tsx` (`ClaimPanel`) — nakon kreiranog pending claima prikazati gumb
  „Potvrdi vlasništvo e-mailom"; na uspjeh prikazati maskirani `sentTo` hint („Poslali smo poveznicu na
  t***@domena.hr"). Ako `no_email_on_file` → objašnjenje da odobrava admin.
- `app/partner/potvrda-vlasnistva/page.tsx` (novo) — čita `?token=`, gumb „Potvrdi", zove `claimApi.verify`;
  na `approved` → poruka o uspjehu + link na `/partner`; na `verified` → „dokaz zabilježen, čeka odobrenje";
  na grešku → HR poruka.
- `components/AdminPanel.tsx` — u listi claimova prikazati bedž po `evidence`: `email_verified` → „✓ e-mail
  potvrđen" (istaknuto), `domain_match` → „✓ domena", `""` → bez bedža. (Kad je auto-approve uključen, takvi
  claimovi ionako neće biti u pending listi — bedž pokriva slučaj isključene sklopke.)

### Testovi (`backend/Wediplan.Api.Tests/`)
- Novi `ClaimVerificationTests` (EF InMemory, obrazac kao `ReviewsControllerTests`): sretni put (valjan token →
  `email_verified` + auto-approve kad je sklopka `true`); istekao token → 400; već potrošen → 400; sklopka
  `false` → status `verified`, vendor ostaje `pending`.

**Kriterij gotovo:** `dotnet build backend/Wediplan.sln -c Release` čist; `dotnet test` zelen (postojeći + novi);
u dev-u (ConsoleEmailSender) `send-verification` ispiše link u konzolu, klik na frontend rutu → `verify` →
vendor `claimed` + owner postavljen; `Claims:AutoApproveOnEmailVerify=false` → status `verified`, admin i dalje
može odobriti; bez `vendor.Email` → gumb se ne nudi / 400. `tsc` + `npm run build` čisti. Ažurirani `API.md`
(novi endpointi, `email_verified`, `no_email_on_file`) i `STANJE.md`.

---

## Zadatak 7 — E-mail obavijesti partnerima

**Cilj:** partner ne mora osvježavati dashboard. Slati HR mail na: claim odobren, claim odbijen, recenzija
objavljena. **Reuse `IEmailSender`; slanje je best-effort i NIKAD ne obara admin akciju.**

**Datoteke:**
- `backend/Wediplan.Api/Auth/PartnerEmails.cs` (novo) — po uzoru na `AuthEmails` (DI `IEmailSender`, `_appUrl`
  iz `App:PublicUrl`). Metode:
  - `SendClaimApproved(email, vendorName, ct)` → link na `/partner`.
  - `SendClaimRejected(email, vendorName, ct)` → neutralan tekst + kontakt za upit.
  - `SendReviewPublished(email, vendorName, ct)` → link na profil/`/partner`.
- `Program.cs` — `builder.Services.AddScoped<PartnerEmails>()`.
- `backend/Wediplan.Api/Services/ClaimApprovalService.cs` — nakon `SaveChanges`, dohvatiti e-mail vlasnika
  (`_users.FindByIdAsync(claim.UserId)`) i pozvati `SendClaimApproved` u **try/catch** (`_log.LogError` na pad,
  bez rušenja). Time i auto-approve (#5) i admin approve šalju istu obavijest — jedan izvor istine.
- `backend/Wediplan.Api/Controllers/AdminController.cs`:
  - `RejectClaim` → nakon `SaveChanges`, best-effort `SendClaimRejected` (e-mail iz `claim.UserId`).
  - `ApproveReview` → best-effort `SendReviewPublished`: vlasnik = `vendor.OwnerUserId`; ako je profil
    **neclaiman (`OwnerUserId == null`)** → preskoči. (Injektirati `PartnerEmails` u `AdminController`.)

**Napomena za budućnost (ne sad):** za volumen prijeći na outbox/red čekanja; sad je inline slanje dovoljno.

**Kriterij gotovo:** `dotnet build -c Release` čist; u dev-u (Console sender) approve/reject claima i objava
recenzije ispišu mail u konzolu; pad slanja (npr. neispravan Resend ključ) **ne** obara admin akciju (akcija
vrati 200, greška samo u logu). `dotnet test` zelen. Ažurirani `API.md` (napomena o nuspojavi-mailu) i `STANJE.md`.

---

## Zadatak 9 — Očvršćivanje rate-limitinga (writes/auth politike)

**Cilj:** pisanja (recenzije, claim, send-verification, opt-out) i auth rute dobiju strožu politiku; particija po
**korisniku kad je prijavljen** (rješava CGNAT lažne pozitive), inače po IP-u; sliding-window umjesto fixed;
`Retry-After` na 429. **Backend-only, bez sheme.**

**Datoteke:** `Program.cs` (samo `AddRateLimiter` blok + `OnRejected`) + atributi na kontrolerima.

**Koraci (u `Program.cs`, `AddRateLimiter`):**
1. Helper particije:
   ```csharp
   static string PartitionKey(HttpContext c)
   {
       var uid = c.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
       return !string.IsNullOrEmpty(uid) ? $"u:{uid}" : $"ip:{c.Connection.RemoteIpAddress}";
       // Iza Cloudflarea RemoteIpAddress je stvarni IP jer UseForwardedHeaders ide prvi (Proxy:TrustForwardedFor).
   }
   ```
2. Politika `"writes"` — `GetSlidingWindowLimiter(PartitionKey(ctx), _ => new SlidingWindowRateLimiterOptions
   { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 6, QueueLimit = 0 })`.
3. Politika `"auth"` — stroža, npr. `PermitLimit = 10, Window = 1 min` (komplementarno Identity lockoutu
   8/15 min); particija po IP-u (auth rute su često pre-login pa nema uid-a) — koristi `Ip(ctx)`.
4. `o.OnRejected = (ctx, _) => { ctx.HttpContext.Response.Headers.RetryAfter = "60"; return ValueTask.CompletedTask; };`
5. Zadržati postojeće `GlobalLimiter` (300/min) i `"lists"` (60/min).

**Atributi:**
- `ReviewsController.Create`, `ClaimsController.Create` + `send-verification` + `verify` → `[EnableRateLimiting("writes")]`.
- `OptOutController` (POST) → `[EnableRateLimiting("writes")]`.
- `AuthController` login/register/magic/reset rute → `[EnableRateLimiting("auth")]`.

**Napomena (ne sad):** limiter je in-memory po instanci — dovoljno za jednu instancu. Za više instanci →
distribuirani (Redis). Zapisati kao budući korak.

**Kriterij gotovo:** `dotnet build -c Release` čist; `dotnet test` zelen (health smoke i dalje diže app s novim
politikama); ručno: brzo ponavljanje POST recenzije/claima s istim korisnikom → 429 + `Retry-After`; liste i dalje
rade pod „lists". Ažurirani `API.md` (spomen 429 na writes/auth) i `STANJE.md`.

---

## Zadatak 8 — Monitoring: Sentry (backend + frontend)

**Cilj:** greške na backendu i frontendu vidljive u Sentryju. **Uzor: Resend** — aktivno SAMO ako je DSN
postavljen (dev/CI ostaju netaknuti). `/api/health` ostaje za uptime (komplementarno).

### Backend
- `backend/Wediplan.Api/Wediplan.Api.csproj` — dodati paket `Sentry.AspNetCore` (uskladiti verziju s .NET
  targetom projekta).
- `Program.cs` — **samo ako DSN postoji**:
  ```csharp
  var sentryDsn = builder.Configuration["Sentry:Dsn"];
  if (!string.IsNullOrWhiteSpace(sentryDsn))
      builder.WebHost.UseSentry(o =>
      {
          o.Dsn = sentryDsn;
          o.Environment = builder.Environment.EnvironmentName;
          o.Release = Environment.GetEnvironmentVariable("SENTRY_RELEASE"); // git sha iz CI-ja
          o.TracesSampleRate = 0.1;
          o.SendDefaultPii = false; // bez e-maila/IP-a (minimizacija — etos projekta)
      });
  ```
  (Ako DSN nema → nula promjena ponašanja; `HealthEndpointTests` prolazi jer CI nema DSN.)
- `backend/Wediplan.Api/appsettings.json` / `.env.example` — dokumentirati `Sentry:Dsn` (env
  `Sentry__Dsn`, NIKAD u git).

### Frontend
- `@sentry/nextjs` (`npm i @sentry/nextjs`); config: `sentry.client.config.ts`, `sentry.server.config.ts`,
  `sentry.edge.config.ts` (DSN `NEXT_PUBLIC_SENTRY_DSN`, `enabled: process.env.NODE_ENV === "production"`,
  `tracesSampleRate: 0.1`).
- `next.config.mjs` — **wrap-ati** postojeći config `withSentryConfig(existingConfig, {...})` (zadržati uploads
  rewrite + CDN remotePattern — NE prepisivati). Opcionalno `tunnelRoute` da ad-block ne guši evente.
- `.env.local.example` — dodati `NEXT_PUBLIC_SENTRY_DSN`.

### Ops
- `DEPLOY.md` — odjeljak Monitoring: Sentry DSN-ovi (env na Vercelu i backendu), `/api/health` za uptime
  (UptimeRobot/BetterStack), napomena da su komplementarni.

**Kriterij gotovo:** bez DSN-a build/test **identičan** (CI zelen, `dotnet test` 4/4+). S DSN-om (ručna provjera
vlasnika): namjerno bačena iznimka na backendu i klijentska greška stižu u Sentry. `tsc` + `npm run build` čisti.
Ažurirani `DEPLOY.md` i `STANJE.md`. (Sourcemap/release upload u `ci.yml` je razuman budući dodatak — ne blokira.)

---

## Zajednička pravila za sve zadatke (vrijede svaku sesiju)
- Rad isključivo na `develop` (grana po zadatku, gore navedena). `main` se ne dira.
- Nakon backend zadatka: `dotnet build backend/Wediplan.sln -c Release` **mora proći**; `dotnet test` zelen.
  Ako zadatak mijenja shemu → nova EF migracija (samo #5) + spomen u opisu PR-a. #6/#7/#8/#9 **ne** mijenjaju shemu.
- Nakon frontend promjena: `npx tsc --noEmit` i `npm run build` čisti.
- Nova Postgres-specifična EF konfiguracija ide **unutar `Database.IsNpgsql()` grane** (InMemory testovi).
- Ne mijenjati postojeće slugove, ugovore `API.md` ruta koje frontend već koristi, ni postojeće migracije.
- Ažurirati `API.md` (#5, #7, #9) i `STANJE.md` (svi) u istom commitu — kratko, činjenično, novije na vrh.
- Predaja po pravilima iz `STANJE.md` („Stalna pravila predaje"): upute za pokretanje/testiranje + kod
  (ZIP pune strukture i/ili patch/bundle) + točne git naredbe za merge.
- Zadatak 6 neka bude prvi mergean (nezavisan, nula rizika); CI (`ci.yml`) vrti build+test na svaki push u `develop`.
