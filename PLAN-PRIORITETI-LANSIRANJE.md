# Plan: 4 prioriteta prije/uz lansiranje

> Namjena: ovaj dokument daješ Claude Sonnetu (ili drugom modelu) kao specifikaciju. Svaki zadatak je
> samostalan, ima točne datoteke, uzorke iz postojećeg koda kojih se treba držati, i kriterij "gotovo".
> Izvor istine je repo `WediPlan2` grana `develop`. Prije koda pročitati `STANJE.md`, `PLAN-ARHITEKTURA.md`, `API.md`.
>
> **Redoslijed rada (preporuka):** Zadatak 3 (recenzije) → Zadatak 2 (brisanje računa) → Zadatak 1 (CI) →
> Zadatak 4 (samo dokumentiranje/praćenje). Zadatak 1 (CI) neka bude **zadnji koji se mergea** jer tek kad
> testovi postoje ima ih smisla vrtjeti u CI-ju; ali kostur CI-ja može nastati bilo kad.
>
> **Svaki zadatak = zasebna grana + zaseban PR/merge.** Ne miješati ih.

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

**Sadržaj workflowa — dva job-a:**

1. **backend**
   - `runs-on: ubuntu-latest`
   - `actions/setup-dotnet@v4` s verzijom iz `global.json` ako postoji, inače `8.0.x` (provjeri TargetFramework
     u `Wediplan.Api.csproj` i uskladi).
   - Koraci: `dotnet restore backend/Wediplan.sln` → `dotnet build backend/Wediplan.sln -c Release --no-restore`
     → `dotnet test backend/Wediplan.sln -c Release --no-build` (dok test projekt ne postoji, test korak
     preskoči ili neka bude uvjetovan — vidi Zadatak 1b).
2. **frontend**
   - `actions/setup-node@v4`, Node 20, `cache: npm`.
   - `npm ci` → `npx tsc --noEmit` → `npm run build`.
   - Build backenda i frontenda ne smiju ovisiti jedan o drugom (frontend build radi s mock rutama kad
     `API_URL` nije postavljen — vidi `.env.example`; u CI-ju **ne** postavljati `API_URL`).

**Trigger:** `on: { push: { branches: [develop] }, pull_request: { branches: [develop] } }`.

**Kriterij gotovo:** workflow prolazi zeleno na test-push u granu; namjerno ubačena sintaktička greška u backendu
obori CI.

### Zadatak 1b — minimalni test projekt (da `dotnet test` ima što vrtjeti)

Bez ijednog testa CI ne donosi puno. Napravi mali projekt s nekoliko smislenih testova.

**Datoteke (nove):**
- `backend/Wediplan.Api.Tests/Wediplan.Api.Tests.csproj` (xUnit; referencira `Wediplan.Api`)
- `backend/Wediplan.Api.Tests/…` test datoteke
- dodati projekt u `backend/Wediplan.sln` (`dotnet sln backend/Wediplan.sln add …`)

**Što testirati prvo (jeftino, a vrijedno) — bez baze:**
- **Smoke:** aplikacija se digne s `WebApplicationFactory<Program>` i `GET /api/health` vrati 200
  (postoji `HealthController`). Ovo hvata greške konfiguracije u `Program.cs` (npr. forwarded headers) — točno onu
  klasu bugova zbog koje je CI potreban. Napomena: možda treba `public partial class Program {}` na dnu
  `Program.cs` da bi `WebApplicationFactory` vidio `Program`.
- **Čiste funkcije:** ako ima helpera bez ovisnosti (npr. `Tokens.Hash` determinističan, `ClientIp`,
  normalizacija emaila) — po jedan test.
- **EF InMemory** (`Microsoft.EntityFrameworkCore.InMemory`): jedan test da `ReviewsController.Create` odbije
  duplu recenziju (`already_reviewed`) — vezuje se uz Zadatak 3.

**Kriterij gotovo:** `dotnet test` lokalno i u CI-ju prolazi; health smoke test stvarno diže app.

---

## Zadatak 2 — Brisanje računa (GDPR "pravo na zaborav")

**Problem:** Pravila privatnosti (`app/pravila-privatnosti/page.tsx`, t. 6 i 8) obećavaju brisanje računa, a
endpoint ne postoji. Must prije javnog lansiranja.

**Odluka o modelu (potvrdi s vlasnikom, default niže):** *hard delete korisnikovih osobnih podataka* uz
*anonimizaciju sadržaja koji mora ostati*. Konkretno:

| Podatak | Postupak pri brisanju | Zašto |
|---|---|---|
| `AppUser` (+ Identity: roles, logins, tokens, claims) | **obriši** | osobni podaci |
| `Favorite`, `BudgetPlan` | **obriši** | privatni korisnikovi podaci |
| `EmailVerificationToken`, `MagicLink` (po emailu) | **obriši** | vezani uz osobu |
| `Claim` (zahtjevi za preuzimanje) | **obriši** | osobni zahtjev |
| `UserReview` | **anonimiziraj**: `UserId = null`-ekvivalent *ILI* zadrži tekst bez veze na korisnika | objavljene recenzije su dio integriteta platforme; ali veza na osobu se miče. **Ako se odabere brisanje umjesto anonimizacije — dogovoriti s vlasnikom.** |
| `Vendor.OwnerUserId` | **postavi na `null`** (profil ostaje, gubi vlasnika) | pružatelj je poslovni podatak, ne osobni; profil ne smije nestati jer je korisnik obrisao svoj račun |

> Napomena: `UserReview` trenutno ima `UserId` kao ne-nullable. Za anonimizaciju treba ili učiniti stupac
> nullable (migracija) ili obrisati recenzije. **Preporuka: obrisati `UserReview` korisnika** (jednostavnije,
> nema šeme za mijenjati, a recenzija bez autora ionako gubi vrijednost za povjerenje). Time se izbjegava
> migracija sheme. Potvrdi s vlasnikom; ovaj plan dalje pretpostavlja **brisanje** `UserReview`.

**Backend — datoteke:**
- **Novi:** `backend/Wediplan.Api/Controllers/AccountController.cs`
  - `[ApiController] [Route("api/account")] [Authorize]`
  - `DELETE /api/account` :
    1. `uid = Uid()` (kao u drugim kontrolerima).
    2. U jednoj transakciji (`_db.Database.BeginTransactionAsync`): obriši `Favorites`, `BudgetPlans`,
       `EmailVerificationTokens`, `Claims`, `UserReviews` gdje `UserId == uid`; obriši `MagicLinks` gdje
       `Email == user.Email`; `Vendors.Where(v => v.OwnerUserId == uid)` → `OwnerUserId = null`.
    3. `await _users.DeleteAsync(user)` (briše AppUser + kaskadno Identity zavisne tablice).
    4. `await _signIn.SignOutAsync()` (poništi cookie).
    5. `Ok(new { ok = true })`.
  - **Potvrda namjere:** tijelo zahtjeva traži `{ confirm: "OBRISI" }` ili sličan sentinel; ako ne odgovara,
    `BadRequest(new { error = "confirmation_required" })`. (Sprječava slučajno brisanje.)
  - DI: `UserManager<AppUser>`, `SignInManager<AppUser>`, `AppDbContext`, `ILogger`. Uzor: `AuthController`.
  - Audit: `_log.LogWarning("account deleted: {UserId}", uid)` — **bez emaila u logu** (minimizacija).
- **Provjeri kaskade:** u `AppDbContext.OnModelCreating` vidjeti brišu li se Identity ovisne tablice kaskadno
  (kod `IdentityDbContext` default je kaskada). Ako `Favorite`/`BudgetPlan`/… nemaju FK na usera (nemaju — bez FK
  po dizajnu), zato ih gore brišemo ručno.

**API.md:** dodati redak u Fazu 6 / novi odjeljak "Račun":
`DELETE /api/account` | sesija | `{ confirm:"OBRISI" }` | `200 {ok:true}` / `400 confirmation_required`.

**Frontend — datoteke:**
- `lib/api/auth.ts`: dodati `deleteAccount()` → `fetch("/api/account", { method:"DELETE", credentials:"include",
  headers:{'content-type':'application/json'}, body: JSON.stringify({confirm:"OBRISI"}) })`.
- `components/ProfileShell.tsx`: u dnu, sekcija "Opasna zona":
  - gumb "Obriši račun" → otvara potvrdu (modal ili `window.confirm` + upis riječi `OBRISI`).
  - na uspjeh: očisti lokalni auth store, preusmjeri na `/` s porukom.
  - tekst uz gumb: što se briše, što ostaje (anonimizirano/odvezano), da je nepovratno.
- Ako postoji auth store (provjeri `stores/`), pozvati njegov `logout()`/`clear()` nakon brisanja.

**Kriterij gotovo:** prijavljen korisnik obriše račun; ponovna prijava s istim emailom nije moguća (nema računa);
njegovi favoriti/plan/claimovi nestali; `Vendor.OwnerUserId` mu je `null` (profil pružatelja i dalje javan);
build + `tsc` čisti; (ako postoji test iz 1b) dodati test koji obriše korisnika na EF-InMemory i provjeri da su
zavisni zapisi počišćeni.

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
