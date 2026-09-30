# Plan: 4 prioriteta prije/uz lansiranje

> Namjena: ovaj dokument daješ Claude Sonnetu (ili drugom modelu) kao specifikaciju. Svaki zadatak je
> samostalan, ima točne datoteke, uzorke iz postojećeg koda kojih se treba držati, i kriterij "gotovo".
> Izvor istine je repo `WediPlan2` grana `develop`. Prije koda pročitati `STANJE.md`, `PLAN-ARHITEKTURA.md`, `API.md`.
>
> **Redoslijed rada (odlučeno):** Zadatak 3 (recenzije) → Zadatak 2 (brisanje računa) → Zadatak 1 (CI) →
> Zadatak 4 (samo dokumentiranje/praćenje, već gotovo — v. STANJE.md). Zadatak 1 (CI) neka bude **zadnji koji
> se mergea** jer tek kad testovi postoje ima ih smisla vrtjeti u CI-ju.
>
> **Svaki zadatak = zasebna grana + zaseban PR/merge.** Ne miješati ih.
>
> ## Status
> - [x] **Zadatak 3 — recenzije samo uz potvrđen email.** Gotovo i **potvrđeno** (`dotnet build` + `dotnet test`
>   prošli 2026-09-22, v. STANJE.md).
> - [x] **Zadatak 2 — brisanje računa.** Gotovo i **potvrđeno**. **Otkriveno tijekom rada:**
>   `Favorite`/`BudgetPlan`/`Claim`/`UserReview` već imaju pravi FK ON DELETE CASCADE prema korisniku
>   (potvrđeno i u migracijama) — `UserManager.DeleteAsync` ih briše sam; ručno se brišu samo
>   `EmailVerificationToken` i `MagicLink` (nemaju taj FK).
> - [x] **Zadatak 1 (+1b) — CI i testovi.** Gotovo i **potvrđeno**: `dotnet build backend/Wediplan.sln -c
>   Release` čist (1 bezazleno pred-postojeće upozorenje CS8603, ne blokira), `dotnet test` 4/4 zeleno —
>   uključujući `HealthEndpointTests`, koji je stvarno digao cijelu app i dobio 200 s `/api/health`.
>   Usput ispravljene dvije greške nađene tim prvim pravim buildom/testom (v. STANJE.md 2026-09-22 (c)+(d)):
>   ambiguity `Claim` (`System.Security.Claims` vs `Wediplan.Api.Domain`) i `Vendor.Search`
>   (`NpgsqlTsVector`) koji je trebalo eksplicitno `Ignore()`-ati pod ne-Npgsql providerom (EF InMemory).
>   Kod je pushan na `develop` (2026-09-22). **Preostaje samo:** vlasnik provjeri da `ci.yml` prođe zeleno
>   na GitHub Actions tabu za ovaj push (nisam u mogućnosti to vidjeti odavde).
> - [x] Zadatak 4 — odluka zapisana (ostaje kako jest), nema koda.
>
> **Sva četiri zadatka iz analize slabosti su implementirana I lokalno potvrđena** (build zelen, testovi
> zeleni). Preostaje: vlasnik provjeri zeleni GitHub Actions run na `develop`, zatim merge `develop`→`main`.

---

## Kontekst koda (provjereno u repou — ne nagađati)

- **Backend:** `backend/Wediplan.Api/` (ASP.NET Core, .NET, EF Core, ASP.NET Identity nad `Guid`). Solucija:
  `backend/Wediplan.sln`, jedini projekt `Wediplan.Api.csproj`. **Nema test projekta. Nema `.github/`.**
- **Identity:** `AddIdentityCore<AppUser>()… .AddSignInManager()` u `Program.cs`. Sesija = httpOnly cookie
  `wediplan.session`. `SignInManager<AppUser>` je dostupan (koristi ga `AuthController`).
- **Što korisnik "posjeduje"** (za brisanje računa) — provjereno u `Domain/`:
  - `Favorite` (`UserId`), `BudgetPlan` (PK = `UserId`), `UserReview` (`UserId`), `Claim` (`UserId`),
    `EmailVerificationToken` (`UserId`).
  - `Vendor.OwnerUserId` (nullable) — korisnik može biti vlasnik profila pružatelja.
  - Identity tablice: `AspNetUserRoles`, `AspNetUserLogins` (Google), `AspNetUserTokens`, `AspNetUserClaims`.
  - `MagicLink` je vezan na **email** (string), ne na `UserId`.
- **Uzorci kojih se držati:**
  - Kontroler stil, DI, `CancellationToken ct`, `Ok(new { … })`, hrvatski komentari: vidi `AuthController`,
    `MeController`, `OptOutController`, `AdminController`.
  - Odjava sesije: `await _signIn.SignOutAsync();` (vidi `AuthController.Logout`).
  - Migracije: `backend/Wediplan.Api/Migrations/` (zadnja `20260918132812_Faza4`). Nova migracija se radi
    `dotnet ef migrations add <Ime>` iz `backend/Wediplan.Api/`.
- **Frontend auth pozivi:** `lib/api/auth.ts` (sve na relativni `/api/*`, `credentials:"include"`).
  Profil korisnika: `app/profil/page.tsx` → `components/ProfileShell.tsx`.

---

## Zadatak 1 — GitHub Actions CI (build + test na svaki push u `develop`)

**Cilj:** svaki push/PR na `develop` automatski builda backend i frontend i vrti testove. Ovo trajno hvata greške
tipa `ForwardedHeadersOptions` koje se lokalno lako promaše.

**Datoteke (nove):**
- `.github/workflows/ci.yml`

**Sadržaj workflowa — dva job-a (implementirano točno ovako):**

1. **backend** — `dotnet restore` → `dotnet build -c Release --no-restore` → `dotnet test -c Release --no-build`.
   Bez Postgresa u CI-ju (namjerno — v. Zadatak 1b, testovi su hermetični/InMemory).
2. **frontend** — `npm ci` → `npx tsc --noEmit` → `npm run build` (bez `API_URL` — mock rute).

**Trigger:** push/PR na `develop`.

**Kriterij gotovo:** workflow prolazi zeleno na test-push u granu; namjerno ubačena sintaktička greška u backendu
obori CI. **Nije pokrenuto ni na pravom GitHubu ni u sandboxu** (nema mrežnog pristupa/dotneta u okruženju u
kojem je pisano) — vlasnik prati prvi pravi run nakon mergea.

### Zadatak 1b — minimalni test projekt — implementirano

**Datoteke (nove):** `backend/Wediplan.Api.Tests/` (`Wediplan.Api.Tests.csproj`, `HealthEndpointTests.cs`,
`ReviewsControllerTests.cs`), dodan u `backend/Wediplan.sln`.

- **Smoke (`HealthEndpointTests`):** `WebApplicationFactory<Program>` diže cijeli `Program.cs` (DI, middleware,
  rate limiter, Identity/cookie, ForwardedHeaders grana, seed rola), `GET /api/health` → 200. `Program.cs` dobio
  `public partial class Program {}` na dnu (WebApplicationFactory treba javno vidljivu klasu).
- **`ReviewsControllerTests`** (EF InMemory, veže se uz Zadatak 3): triput poziva `ReviewsController.Create`
  izravno (ne preko HTTP-a) — `email_not_confirmed` (403), `already_reviewed` (409), i sretni put (200 + red u
  bazi). `UserManager<AppUser>` se gradi preko `AddIdentityCore` (isti obrazac kao Program.cs), ne ručno.
- **Otkriveno/riješeno tijekom rada:** oba testa dijele `AppDbContext`, čiji `OnModelCreating` ima Postgres-only
  konfiguraciju (`HasPostgresExtension`, GIN indeksi na `Vendor`, generirani tsvector stupac) — pod EF InMemory
  to je neprovjereno ponašanje. Dodan standardni EF Core idiom: `AppDbContext.OnModelCreating` sad provjerava
  `Database.IsNpgsql()` i taj blok primjenjuje SAMO pod pravim Postgresom; pod bilo kojim drugim providerom
  (testovi) se preskače. Produkcija (uvijek Npgsql) — bez promjene ponašanja.
- **Namjerno izostavljeno (za sada):** testovi čistih helpera (`Tokens.Hash`, `ClientIp`) — nisu bili nužni za
  dvije stvarne, vrijedne provjere gore; dodati po potrebi. Postgres servis-container u CI-ju (za testiranje
  pravih migracija) — svjesno izostavljen radi jednostavnosti i brzine prvog CI-ja; razuman budući dodatak.

**Kriterij gotovo:** `dotnet test` prolazi (3 testa u `ReviewsControllerTests` + 1 u `HealthEndpointTests`);
health smoke test stvarno diže app. **Nije pokrenuto u sandboxu — vlasnik pokreće prvi `dotnet test` lokalno
prije oslanjanja na CI.**

**Kriterij gotovo:** `dotnet test` lokalno i u CI-ju prolazi; health smoke test stvarno diže app.

---

## Zadatak 2 — Brisanje računa (GDPR "pravo na zaborav")

**Problem:** Pravila privatnosti (`app/pravila-privatnosti/page.tsx`, t. 6 i 8) obećavaju brisanje računa, a
endpoint ne postoji. Must prije javnog lansiranja.

**Odluka o modelu (potvrđeno s vlasnikom, 2026-09-21):** *hard delete korisnikovih osobnih podataka* uz
*anonimizaciju sadržaja koji mora ostati*. Konkretno:

| Podatak | Postupak pri brisanju | Zašto |
|---|---|---|
| `AppUser` (+ Identity: roles, logins, tokens, claims) | **obriši** | osobni podaci |
| `Favorite`, `BudgetPlan` | **obriši** | privatni korisnikovi podaci |
| `EmailVerificationToken`, `MagicLink` (po emailu) | **obriši** | vezani uz osobu |
| `Claim` (zahtjevi za preuzimanje) | **obriši** | osobni zahtjev |
| `UserReview` | **obriši** (odlučeno) | i objavljene recenzije korisnika nestaju s brisanjem računa — jednostavnije od anonimizacije, nema migracije |
| `Vendor.OwnerUserId` | **postavi na `null`** (profil ostaje, gubi vlasnika) | pružatelj je poslovni podatak, ne osobni; profil ne smije nestati jer je korisnik obrisao svoj račun |

> Napomena: `UserReview` trenutno ima `UserId` kao ne-nullable. **Odlučeno: brišu se** (ne anonimiziraju) —
> jednostavnije, nema šeme za mijenjati, a recenzija bez autora ionako gubi vrijednost za povjerenje. Ovaj plan
> dalje **pretpostavlja brisanje** `UserReview`; tablica u koraku "Backend — datoteke" dolje je već ažurirana.

**Backend — datoteke:**
- **Novi:** `backend/Wediplan.Api/Controllers/AccountController.cs`
  - `[ApiController] [Route("api/account")] [Authorize]`
  - `DELETE /api/account` :
    1. `uid = Uid()` (kao u drugim kontrolerima).
    2. U jednoj transakciji (`_db.Database.BeginTransactionAsync`):
       - `Vendor.OwnerUserId → null` za sve profile tog korisnika (`ExecuteUpdateAsync`).
       - **Eksplicitno obriši samo `EmailVerificationToken` (po `UserId`) i `MagicLink` (po emailu)**
         — ta dva NEMAJU cascade FK prema korisniku (`EmailVerificationToken` ima samo indeks;
         `MagicLink` je vezan uz email, ne uz `UserId`, nema tu kolonu uopće).
       - `Favorite`, `BudgetPlan`, `Claim`, `UserReview` **NE brišu se ručno** — svi već imaju pravi FK
         `ON DELETE CASCADE` prema `users` (potvrđeno u `AppDbContext.OnModelCreating` i u migracijama
         `Faza3Couple`/`Faza4`), pa ih Postgres sam obriše kad nestane red u `users`.
    3. `await _users.DeleteAsync(user)` (briše AppUser red → DB cascade obriše gornje četiri tablice +
       Identity role/login/token/claim, isto po defaultnom cascade ponašanju `IdentityDbContext`).
    4. `await _signIn.SignOutAsync()` (poništi cookie).
    5. `Ok(new { ok = true })`.
  - **Potvrda namjere:** tijelo zahtjeva traži `{ confirm: "OBRISI" }` ili sličan sentinel; ako ne odgovara,
    `BadRequest(new { error = "confirmation_required" })`. (Sprječava slučajno brisanje.)
  - DI: `UserManager<AppUser>`, `SignInManager<AppUser>`, `AppDbContext`, `ILogger`. Uzor: `AuthController`.
  - Audit: `_log.LogWarning("account deleted: {UserId}", uid)` — **bez emaila u logu** (minimizacija).

**API.md:** dodati redak u Fazu 6 / novi odjeljak "Račun":
`DELETE /api/account` | sesija | `{ confirm:"OBRISI" }` | `200 {ok:true}` / `400 confirmation_required`.

**Frontend — implementirano:**
- `lib/api/auth.ts`: `authApi.deleteAccount(confirm)` — koristi postojeći `call()` helper (`DELETE /api/account`,
  `{confirm}`). `stores/auth.ts` (`authMessage`): poruka za `confirmation_required`.
- `components/ProfileShell.tsx`: sekcija "Opasna zona" na dnu, samo za prijavljenog korisnika (`useAuth().user`):
  gumb "Obriši račun" → inline potvrda s poljem za upis točno "OBRISI" (gumb onemogućen dok se ne poklapa) →
  na uspjeh poziva postojeći `useAuth().logout()` (čisti store + best-effort `/api/auth/logout`, cookie je već
  obrisan na serveru) i `router.push("/")`. Tekst objašnjava što se briše i da profil pružatelja ostaje javan.
  **Usput ispravljeno** (zatečeno pri radu, izvan opsega zadatka): vrh `/profil` imao je zastarjeli tekst
  "sinkronizacija — uskoro" i onemogućene gumbove za prijavu, iako `AccountSync.tsx` sinkronizaciju već stvarno
  radi — tekst i gumbovi sad ispravno vode na `/prijava` i prikazuju se samo gostu.
- `app/globals.css`: `.btn-danger`, `.danger-zone`, `.danger-confirm`, `.danger-input` (koriste postojeći
  `--danger` token, isti kao `.auth-error`).

**Kriterij gotovo:** prijavljen korisnik obriše račun; ponovna prijava s istim emailom nije moguća (nema računa);
njegovi favoriti/plan/claimovi/recenzije nestali (DB cascade); `Vendor.OwnerUserId` mu je `null` (profil
pružatelja i dalje javan); build + `tsc` čisti — **potvrđeno**; UI tok vizualno potvrđen (route-mock `/api/me`
u sandboxu, v. STANJE.md); (ako postoji test iz 1b) dodati test koji obriše korisnika na EF-InMemory i provjeri
da su zavisni zapisi počišćeni.

---

## Zadatak 3 — Recenzije samo uz potvrđen email

**Problem:** `ReviewsController.Create` ne provjerava `EmailConfirmed`. Throwaway račun može slati recenzije.
Za platformu kojoj je povjerenje USP — dodati uvjet.

**Napomena:** `Program.cs` ima `o.SignIn.RequireConfirmedEmail = true`, a login blokira nepotvrđeni email.
**Ali** magic-link i Google put stvore korisnika s `EmailConfirmed = true` odmah — provjera na recenziji je
svejedno prava zaštita protiv scenarija gdje bi netko došao do sesije s nepotvrđenim emailom, i čini pravilo
eksplicitnim/otpornim na buduće promjene auth toka. Trošak: par redaka.

**Backend — datoteka:** `backend/Wediplan.Api/Controllers/ReviewsController.cs`
- U `Create`, odmah nakon `var uid = Uid();`:
  ```csharp
  var user = await _users.FindByIdAsync(uid.ToString());
  if (user == null || !user.EmailConfirmed)
      return StatusCode(403, new { error = "email_not_confirmed" });
  ```
  (`UserManager<AppUser> _users` već je injektiran u kontroler.)

**API.md:** kod "Korisničke recenzije" dodati mogući odgovor `403 email_not_confirmed`.

**Frontend:** gdje se šalje recenzija (naći komponentu koja zove review create — vjerojatno na
`components/VendorProfile.tsx` ili zasebna forma), uhvatiti `403 email_not_confirmed` i prikazati poruku:
"Za pisanje recenzije potvrdite svoju e-mail adresu." (+ link na ponovno slanje potvrde ako postoji).

**Kriterij gotovo:** korisnik s `EmailConfirmed=false` dobije 403 i jasnu poruku; potvrđen korisnik normalno
recenzira; (ako postoji test iz 1b) test na EF-InMemory: nepotvrđen → 403.

---

## Zadatak 4 — Opt-out zloupotreba: praćenje (bez koda za sada)

**Stanje:** `OptOutController` skida profil **odmah** i anonimno je (GDPR-first), ali je reverzibilno — admin vidi
`GET /api/admin/optouts` i može vratiti (`POST /api/admin/vendors/{slug}/restore-optout`). Ovo je svjesna odluka.
Rizik: konkurent skida tuđi profil.

**Za sada NIJE kod, nego odluka + priprema:**
- **Ostaje kako jest** dok se ne pojavi zloupotreba (dokumentirano u samom `OptOutController` komentaru).
- **Priprema plan za prebacivanje na "admin odobrava"** ako zatreba (opisano ovdje da bude spremno):
  1. Umjesto `Vendor.OptOut = true` odmah, spremati zahtjev kao `pending` (novo stanje ili nova mala tablica
     `OptOutRequest{ VendorId, Reason?, Contact?, CreatedAt, Status }`).
  2. Admin ruta za odobri/odbij (uzor: `AdminController` claim approve/reject).
  3. Tek na "odobri" postaviti `Vendor.OptOut = true`.
  - Napomena: ovo je u tenziji s "trenutnim uklanjanjem" iz GDPR-a — zato ga ne radimo preventivno; radimo ga
    samo ako stvarna zloupotreba to opravda, uz kratki rok obrade zahtjeva.
- **Minimalni "monitoring" koji je vrijedan odmah (opcionalno, malen):** brojati opt-out događaje (već se loga
  `LogWarning`). Ako se želi vidljivost bez novog koda — dovoljno je pratiti te logove. Ako se želi malo više:
  dodati opt-out u `DailyStat`/`Event` tok (postoji `EventsController`/`DailyStat`) — **samo ako vlasnik želi**,
  inače preskočiti.

**Kriterij gotovo:** odluka zapisana u `STANJE.md` (praćenje, prag za prelazak na odobrenje). Bez promjene koda
osim po izričitoj želji vlasnika.

---

## Zajednička pravila za sve zadatke

- Grananje: `feat/ci`, `feat/account-deletion`, `feat/review-verified-email` (Zadatak 4 nema granu ako je samo
  dokumentacija — ide u sklopu `STANJE.md` update-a).
- Nakon svakog backend zadatka: `dotnet build backend/Wediplan.sln -c Release` mora proći. Ako mijenja shemu →
  nova EF migracija + spomenuti je u opisu. (Zadatci 2 i 3 u ovoj verziji **ne** mijenjaju shemu — provjeri.)
- Nakon frontend promjena: `npx tsc --noEmit` i `npm run build` čisti.
- Ažurirati `API.md` (Zadatci 2, 3) i `STANJE.md` (svi) — kratko, činjenično, novije na vrh.
- Ne mijenjati `slug`-ove, ugovor `API.md` ruta koje frontend već koristi, ni postojeće migracije.
