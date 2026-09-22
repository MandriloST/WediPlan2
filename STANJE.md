# STANJE.md — dnevnik rada i trenutno stanje projekta

> 📍 **KANONSKI REPO: https://github.com/MandriloST/WediPlan2.git** (grana `develop`). Ovo je JEDINI ispravan repo — NE `WediPlan`/`wediplan` bez 2.


> **Namjena:** model koji nastavlja rad čita OVO + `PLAN-ARHITEKTURA.md` + `API.md` prije koda.
> Ažurira se na kraju SVAKE radne sesije (kratko, činjenično). Novije sesije na vrhu.
> Uvijek provjeriti i stvarni `git log` — repo je izvor istine, ovo je sažetak.

## Repo: **WediPlan2** (novi, čist — GDPR #18 riješen). Javan dok razvoj traje; na kraju → private.
## Trenutna faza: **Faza 6 (lansiranje) — KOD IMPLEMENTIRAN ⏳ (2026-09-17)**. Frontend build/tsc čisti; backend kod predan (bez nove migracije). Preostaju OPS koraci vlasnika: domena+`NEXT_PUBLIC_SITE_URL`, popuna+pravna provjera pravnih stranica, Google Search Console, finalna regresija, **merge `develop`→`main`**. Sve odluke #1–#19 ODOBRENE.
## Analiza slabosti pred lansiranje (2026-09-21) — **sva 4 zadatka implementirana I POTVRĐENA** (2026-09-22, v. `PLAN-PRIORITETI-LANSIRANJE.md`): CI+testovi, brisanje računa (GDPR), recenzije uz potvrđen email, opt-out odluka. `dotnet build` + `dotnet test` prolaze čisto (4/4 testa). Pushano na `develop`. Preostaje: vlasnik provjeri zeleni GitHub Actions run, zatim merge u `main`.

## Sesija 2026-09-22 (e) — Potvrđeno: dotnet build + dotnet test prolaze (4/4)
Nakon dva popravka iz prošle bilješke, vlasnik ponovno pokrenuo `dotnet test backend/Wediplan.sln`:
```
Wediplan.Api net8.0 succeeded with 1 warning(s)   ← CS8603, bezazleno, pred-postojeće, ne blokira
Wediplan.Api.Tests net8.0 succeeded (0 errors)
Test summary: total: 4; failed: 0; succeeded: 4; skipped: 0
```
`HealthEndpointTests` je stvarno digao cijelu aplikaciju (Program.cs — DI, middleware, Identity, rate limiter)
i dobio pravi `200` na `/api/health` (vidljivo u logu: `Executed endpoint 'HTTP: GET /api/health'` →
`200 - application/json`). Sva tri `ReviewsControllerTests` isto zelena.

**Backend je sada stvarno kompajliran i testiran — prvi put otkad je pisan** (ranije sesije su radile bez
dotnet SDK-a u okruženju pisanja i mogle su samo ručno pregledati kod). Oba popravka i ovaj rezultat su
commitani i pushani na `develop` (`13f8aac claim`, `34dc9eb korekcija testa`).

**Jedino što ostaje neprovjereno s ove strane:** pravi CI run na GitHub Actionsu za ovaj push (`ci.yml`,
Zadatak 1) — vlasnik treba provjeriti Actions tab. Kad to bude zeleno, sva četiri zadatka iz analize slabosti
(2026-09-21) su gotova i potvrđena, i ništa iz Faze 6 plana više ne čeka na kod — preostaju samo OPS koraci
(domena, pravne stranice, Search Console, finalna ručna regresija, merge `develop`→`main`).

## Sesija 2026-09-22 (c/d) — Dva popravka nakon prvog dotnet build/test
Vlasnik pokrenuo `dotnet build`/`dotnet test` (prvi put da je backend stvarno kompajliran i testiran). Dvije
greške, oba popravljena:
1. **Build error CS0104** — `ReviewsControllerTests.cs` ima i `using System.Security.Claims;` i
   `using Wediplan.Api.Domain;`, a `Wediplan.Api.Domain` ima svoj `Claim` (zahtjev za preuzimanje profila) →
   ambiguity. Popravak: puno kvalificirano ime `System.Security.Claims.Claim` na jedinom mjestu korištenja.
2. **Runtime error kod dotnet test** — sva 4 testa pucala s "No suitable constructor for entity type
   'NpgsqlTsVector'". Uzrok: `Vendor.Search` (`NpgsqlTsVector?`) je i dalje bio dio EF modela pod InMemory
   providerom — isključivanje FLUENT konfiguracije (`isNpgsql` guard iz prošle sesije) nije bilo dovoljno,
   trebalo je isključiti i sâm CLR tip. Popravak: `e.Ignore(v => v.Search);` unutar `if (!isNpgsql)` grane u
   `AppDbContext.OnModelCreating`. Provjereno (grep) da je `Search` jedino mjesto u modelu s Npgsql-specifičnim
   CLR tipom — popravak je potpun.
   
## Sesija 2026-09-22 (b) — Zadatak 1 (+1b) (Plan prioriteti): CI + minimalni testovi
Implementiran **Zadatak 1** (v. `PLAN-PRIORITETI-LANSIRANJE.md`), grana `feat/ci-tests`. **Sva četiri zadatka
iz analize slabosti (2026-09-21) su sada implementirana.**

- **`.github/workflows/ci.yml`** (novo) — dva joba na push/PR u `develop`: `backend` (`dotnet restore` →
  `build -c Release` → `test -c Release --no-build`) i `frontend` (`npm ci` → `tsc --noEmit` → `npm run build`,
  bez `API_URL` — mock rute). Bez Postgresa u CI-ju (testovi su namjerno hermetični, EF InMemory).
- **`backend/Wediplan.Api.Tests/`** (novi projekt, dodan u `Wediplan.sln`):
  - `HealthEndpointTests.cs` — `WebApplicationFactory<Program>` diže CIJELI `Program.cs` (DI, middleware,
    rate limiter, Identity/cookie, ForwardedHeaders grana, seed rola), zamijeni `AppDbContext` s EF InMemory,
    `GET /api/health` → 200. Ovo je smoke test koji hvata točno onu klasu grešaka (npr. loš
    `ForwardedHeadersOptions`) zbog koje je CI tražen.
  - `ReviewsControllerTests.cs` — 3 testa nad `ReviewsController.Create` (EF InMemory, `UserManager` preko
    `AddIdentityCore`, ne ručno): `email_not_confirmed` (403, Zadatak 3), `already_reviewed` (409), sretni put.
- **`backend/Wediplan.Api/Program.cs`** — dodan `public partial class Program {}` na dnu (potrebno da
  `WebApplicationFactory<Program>` vidi klasu; top-level statements je inače generiraju kao `internal`).
- **`backend/Wediplan.Api/Data/AppDbContext.cs`** — **otkriveno tijekom pisanja testova:** `OnModelCreating` ima
  Postgres-only konfiguraciju (`HasPostgresExtension`, GIN indeksi, generirani tsvector stupac na `Vendor`) koja
  je pod EF InMemory bila neprovjereno ponašanje (oba nova testa dijele taj model čim se `AppDbContext`
  ikako upotrijebi). Dodan standardni EF Core idiom: taj blok se primjenjuje samo kad `Database.IsNpgsql()`;
  pod bilo kojim drugim providerom (testovi) se preskače. Produkcija (uvijek Npgsql) — bez promjene ponašanja.
- **`backend/Wediplan.sln`** — dodan `Wediplan.Api.Tests` (Debug/Release, Any CPU).
- **Namjerno izostavljeno u ovoj iteraciji** (v. plan, razlozi ondje): Postgres servis-container + `dotnet ef
  database update` u CI-ju (validacija pravih migracija) i testovi čistih helper-funkcija — razumni budući
  dodaci, ne blokiraju lansiranje.
- **Verifikacija:** frontend `tsc` + `next build` čisti (nepromijenjen ovom sesijom, samo potvrđeno da backend
  izmjene ne diraju frontend). **Backend NIJE build-an ni testiran u sandboxu** (nema dotnet SDK, ni pristup
  GitHubu za pravi CI run) — sav C# kod je pažljivo ručno pregledan, uključujući namjerno de-riziranje
  Npgsql/InMemory sukoba prije nego što je postao problem, ali **vlasnik mora**: (1) pokrenuti
  `dotnet build backend/Wediplan.sln -c Release` i `dotnet test backend/Wediplan.sln`, (2) nakon push-a na
  GitHub, provjeriti da `ci.yml` prođe zeleno, i javiti ako nešto padne (najvjerojatnije mjesto: točne verzije
  NuGet paketa u `Wediplan.Api.Tests.csproj`, ili nešto specifično za Npgsql/InMemory interakciju koje nisam
  mogao izvršiti da provjerim).

**Time su sve stavke iz analize slabosti (2026-09-21) pokrivene.** Preostaje: vlasnik potvrđuje da backend
stvarno kompajlira, da testovi prolaze i da CI radi na pravom GitHubu, zatim `develop` → `main`.

## Sesija 2026-09-22 — Zadatak 2 (Plan prioriteti): brisanje računa
Implementiran **Zadatak 2** (v. `PLAN-PRIORITETI-LANSIRANJE.md`), grana `feat/account-deletion`.

**Otkriće koje je pojednostavilo plan:** `Favorite`, `BudgetPlan`, `Claim`, `UserReview` već imaju pravi FK
`ON DELETE CASCADE` prema korisniku (`AppDbContext.OnModelCreating`, potvrđeno i u migracijama
`Faza3Couple`/`Faza4`) — nije ih trebalo ručno brisati. `UserManager.DeleteAsync(user)` ih briše sam na razini
baze, zajedno s Identity role/login/token/claim (default `IdentityDbContext` ponašanje).

- **`backend/Wediplan.Api/Controllers/AccountController.cs`** (novi) — `DELETE /api/account`, `[Authorize]`.
  Traži `{confirm:"OBRISI"}` (inače 400 `confirmation_required`). U transakciji: `Vendor.OwnerUserId → null`
  za sve profile korisnika (`ExecuteUpdateAsync`); eksplicitno briše `EmailVerificationToken` (po `UserId`) i
  `MagicLink` (po emailu) — jedina dva zapisa BEZ cascade FK; zatim `_users.DeleteAsync(user)` (DB cascade za
  ostalo); `_signIn.SignOutAsync()`; `LogWarning` bez emaila (minimizacija).
- **`backend/Wediplan.Api/Contracts/AuthContracts.cs`** — novi `DeleteAccountRequest(string Confirm)`.
- **`lib/api/auth.ts`** — `authApi.deleteAccount(confirm)`. **`stores/auth.ts`** — poruka za `confirmation_required`.
- **`components/ProfileShell.tsx`** — sekcija "Opasna zona" (samo za prijavljenog korisnika): klik → inline
  potvrda s poljem za upis "OBRISI" (gumb onemogućen dok se ne poklapa) → na uspjeh `useAuth().logout()` +
  redirect na `/`. **Usput ispravljeno** (zatečeno, izvan opsega): vrh `/profil` je imao zastarjeli tekst
  "sinkronizacija — uskoro" i onemogućene gumbove za prijavu, iako `AccountSync.tsx` sinkronizaciju već stvarno
  radi u pozadini — sad ispravno vodi na `/prijava`, prikazuje se samo gostu.
- **`app/globals.css`** — `.btn-danger`, `.danger-zone`, `.danger-confirm`, `.danger-input` (isti `--danger`
  token kao `.auth-error`).
- **`API.md`** — dodan `DELETE /api/account` u novi odjeljak "Brisanje računa".
- **Verifikacija:** `tsc` + `next build` čisti. UI tok vizualno potvrđen — Playwright s mock-anim `/api/me`
  (pravi backend nije dostupan u sandboxu): prijavljeno stanje → "Opasna zona" → prazno polje (gumb
  onemogućen) → upisano "OBRISI" (gumb aktivan). Logged-out `/profil` bez regresije.
  **Backend build NIJE proveden u sandboxu (nema dotnet SDK — isto upozorenje kao Zadatak 3) — vlasnik mora
  pokrenuti `dotnet build backend/Wediplan.sln -c Release` prije mergea.**

**Sljedeći korak: Zadatak 1 (+1b) — CI i testovi**, iz istog plan-dokumenta. Kreni od odjeljka "Zadatak 1" u
`PLAN-PRIORITETI-LANSIRANJE.md`. Nakon ovoga, sva četiri zadatka iz analize slabosti su gotova.

## Sesija 2026-09-21 (c) — Zadatak 3 (Plan prioriteti): recenzije samo uz potvrđen email
Odluke potvrđene s vlasnikom: `UserReview` se pri brisanju računa **briše** (ne anonimizira); redoslijed
zadataka **3 → 2 → 1**. Implementiran **Zadatak 3** (v. `PLAN-PRIORITETI-LANSIRANJE.md`), grana
`feat/review-verified-email`:
- **`backend/Wediplan.Api/Controllers/ReviewsController.cs`** — `Create` sad prvo dohvati korisnika
  (`_users.FindByIdAsync`) i vrati **403 `email_not_confirmed`** ako `!EmailConfirmed`, prije provjere postoji
  li vendor. Bez promjene sheme/migracije.
- **`lib/api/provider.ts`** (`providerMessage`) — hrvatska poruka za `email_not_confirmed`.
- **`components/ReviewForm.tsx`** — proaktivna provjera: prijavljen korisnik s nepotvrđenim emailom vidi objašnjenje
  (s adresom na koju je poslana potvrda) **umjesto** forme, ne tek nakon pokušaja slanja.
- **`API.md`** — dodan `403 email_not_confirmed` u opis `POST /api/reviews`.
- Verifikacija: `tsc` + `next build` čisti. **Backend NIJE build-an u sandboxu (nema dotnet SDK ovdje — baš to
  je Zadatak 1) — vlasnik mora pokrenuti `dotnet build backend/Wediplan.sln -c Release` prije mergea.**

**Sljedeći korak: Zadatak 2 (brisanje računa)**, iz istog plan-dokumenta. Kreni od odjeljka "Zadatak 2" u
`PLAN-PRIORITETI-LANSIRANJE.md` — tablica entiteta, novi `AccountController`, FE "Opasna zona" u `ProfileShell`.

## Sesija 2026-09-21 (b) — Analiza slabosti pred lansiranje + plan (PLAN-PRIORITETI-LANSIRANJE.md)
Napravljena analiza slabosti; **kod nije mijenjan**, samo je dodan plan `PLAN-PRIORITETI-LANSIRANJE.md` (spec za
izvedbu kroz model). Četiri prioriteta prije/uz launch:
1. **CI + testovi** — nema `.github/` ni test projekta. Plan: `ci.yml` (backend build+test, frontend tsc+build na
   push/PR u develop) + minimalni xUnit projekt `Wediplan.Api.Tests` (health smoke test hvata baš greške tipa
   ForwardedHeaders u `Program.cs`).
2. **Brisanje računa (GDPR)** — obećано u Pravilima privatnosti, endpoint ne postoji. Plan: novi
   `AccountController` `DELETE /api/account` (potvrda `{confirm:"OBRISI"}`), briše korisnikove osobne podatke
   (Favorite, BudgetPlan, EmailVerificationToken, Claim, UserReview, MagicLink po emailu) + `UserManager.DeleteAsync`
   (kaskada Identity), `Vendor.OwnerUserId → null` (profil pružatelja ostaje), odjava. Bez promjene sheme. FE:
   „Opasna zona” u `ProfileShell` + `deleteAccount()` u `lib/api/auth.ts`. **Otvoreno pitanje za vlasnika:**
   UserReview brisati (preporuka) ili anonimizirati (traži nullable UserId + migraciju).
3. **Recenzije bez potvrđenog emaila** — `ReviewsController.Create` ne provjerava `EmailConfirmed`. Plan: dodati
   provjeru (403 `email_not_confirmed`), ~3 retka + FE poruka. Bez promjene sheme.
4. **Anonimni trenutni opt-out** — vektor zloupotrebe konkurenata. **Odluka: ostaje kako jest** (GDPR-first,
   reverzibilno preko admina). Plan za prelazak na „admin odobrava” zapisan za slučaj zloupotrebe. **Prag:**
   ako se pojavi zloupotreba (skidanje tuđih profila), prebaciti opt-out na pending+admin-approve. Do tada pratiti
   `LogWarning("GDPR opt-out…")` u logovima.

Preporučeni redoslijed izvedbe: 3 → 2 → 1 (+1b), a Zadatak 4 je zasad samo ova bilješka.

## Sesija 2026-09-21 — Treći sloj default slika: kartica ≠ profil
- **`lib/images.ts`**: `vendorDefaultImage(category, context)` i `vendorImages(vendor, context)` sad primaju
  `context: "card" | "profile"` (zadano `"card"`, pa se `coverImage()` — rezultati/karta/usporedba — ne mijenja).
  `"profile"` čita iz **novog** foldera `public/images/defaults-profile/` umjesto `public/images/defaults/`.
  Isti `VENDOR_DEFAULT_MODE` ("per-category"/"single") vrijedi za oba sloja, datoteke su zasebne.
- **`components/VendorProfile.tsx`** (`/pruzatelj/[slug]`) sad zove `vendorImages(vendor, "profile")`.
  Kartica, karta i usporedba (`VendorCard`, `CroatiaMap`, `/usporedba`) i dalje idu preko `coverImage()` → `"card"`.
- **`public/images/defaults-profile/`**: zasad kopija dosadašnjih `defaults/*.jpg` (isti prikaz kao prije za
  pružatelje bez vlastitih fotografija) — zamijeni pojedinačne datoteke kad budeš imao prikladnije "veće" kadrove.
- **`scripts/sync-images.mjs`**: `npm run sync:images` sad upozorava odvojeno na nedostajuću default sliku
  kartice i profila.
- Kad vendor ima svoje fotografije (`photos`), `context` nema utjecaja — posvuda su iste, stvarne slike.
- Verifikacija: `tsc` + `next build` čisti; ručno potvrđeno da su kartica i profil vizualno neovisni (privremeno
  tonirana jedna profil-slika radi provjere, pa vraćena).

## Sesija 2026-09-19 (c) — Paginacija 12 po stranici + ravni grid kategorija
- **Rezultati:** `PAGE_SIZE = 12` (`lib/paths.ts`) — klijent i SSR uvijek traže 12, više se ne može prikazati.
  „Učitaj još” (infinite query) zamijenjen paginacijom (`components/Pagination.tsx`): `?page=N` pravi linkovi s
  rel=prev/next; desktop = brojevi + Prethodna/Sljedeća + velike strelice uz mrežu (≥ 1270 px) + tipkovnica ←/→;
  mobitel = kompaktno „‹ 3 / 9 ›” (mete 48 px), bez swipea (sukobio bi se s horizontalnim chipovima i kartom).
  Promjena stranice skrola na vrh liste, ne cijele stranice. Stranica iza kraja i dalje 404.
- **/kategorije i /regija:** bez naslova skupina — jedna mreža 29 pločica (6 u redu, responzivno 3/2), redoslijed po omotnicama.
- API nepromijenjen (`pageSize` je već u ugovoru). `tsc` + `next build` čisti; ručno testirano (desktop, mobitel, ←/→).

## Sesija 2026-09-19 (b) — Foto-pločice na /kategorije + odvojene slike kategorija i pružatelja
- **`components/CategoryTile.tsx`** — zajednička foto-pločica (naslovnica i `/kategorije` + `/regija` su sad identične).
  `CategoryGrid` više nema emoji ikone; prazne kategorije su sive (grayscale), i dalje klikabilne.
- **Slike razdvojene** (`lib/images.ts`): `public/images/categories/<slug>.jpg` = slika KATEGORIJE (pločice);
  `public/images/defaults/…` = default slika PRUŽATELJA bez vlastitih fotografija. `VENDOR_DEFAULT_MODE`:
  `"per-category"` (defaults/<kategorija>.jpg, kao dosad) ili `"single"` (jedna defaults/pruzatelj.jpg za sve).
  `sync:images` čita način iz `lib/images.ts`. Dodani nedostajući `simbolicni-maticar.jpg` (placeholder) i `pruzatelj.jpg`.
- **Tekstovi naslovnice** premješteni u `lib/landing.ts` (`LANDING_TEXT`); hero slika: `HERO_IMAGE` u istoj datoteci,
  fotografija u `public/images/hero/`.
- Verifikacija: `tsc` + `next build` čisti; ručno pregledano `/`, `/kategorije` (desktop + mobitel).

## Sesija 2026-09-19 — Redizajn naslovnice prema wireframeu 3a (grana `claude/landing-3a` → develop)
- **Naslovnica `/`** = `components/LandingShell.tsx` (3a bez sekcija: stil, brojke, inspiracija, newsletter — dolaze nakon MVP-a):
  hero (postojeći `SearchBar`, poveznice na usporedbu i budžet-drawer) → 6 foto-pločica kategorija s brojačima →
  „Najbolje ocijenjeni” (prvi po rangu iz dvorana/foto/bendova, postojeći `VendorCard`) → karta HR + popis regija s brojačima.
  Konfiguracija u `lib/landing.ts` (pločice, kategorije za „najbolje ocijenjene”, `HERO_IMAGE` — zasad `null` → jednobojni hero).
- **`/kategorije`** = bivši landing (grid 29 kategorija + regije + karta, `ExploreShell`), ISR 60 s. `/regija` ostaje isti grid sužen na regiju.
  „Cijela Hrvatska” i „← Sve kategorije” sad vode na `/kategorije` (`browsePath()` u `lib/paths.ts`). Sitemap: + `/kategorije`.
- **Header:** logo + Kategorije (`/kategorije`) · Lokacije (`/karta`) · Inspiracija (`/inspiracija`, rezervirano mjesto „Uskoro”, noindex) ·
  Za pružatelje (`/partner`); uklonjen „Sve ▾” izbornik i zasebna poveznica „Za partnere”. Footer/metadata: „Za pružatelje”.
- **Bez promjena** API-ja, backenda, slugova i migracija. Verifikacija: `tsc` + `next build` čisti; ručno pregledano (desktop 1280, mobitel 390).
- **Za vlasnika:** kad stigne prava hero fotografija → `public/images/hero/naslovnica.jpg` + `HERO_IMAGE` u `lib/landing.ts`.
  Kad dođe plaćeni „Izlog na naslovnici” (§M.2) → mijenja se samo izvor podataka za „Najbolje ocijenjeni” (+ naslov „Izdvojeno”).

## Sesija 2026-09-17 (c) — Faza 6: lansiranje (pravne stranice + GDPR opt-out)
- **GDPR opt-out (§9):** `OptOutController` (`POST /api/optout`, javno+rate-limited) — postavlja `Vendor.OptOut=true` odmah (koristi postojeće polje → **bez migracije**); razlog/kontakt samo u log (minimizacija). Admin: `GET /api/admin/optouts` + `POST /api/admin/vendors/{slug}/restore-optout` (reverzibilno zbog moguće zloupotrebe anonimne forme). Frontend: `OptOutLink` na neclaimanom profilu (aside), admin sekcija "Skriveni profili".
- **Pravne stranice (§9):** `/pravila-privatnosti`, `/uvjeti-koristenja` (s anti-scraping klauzulom), `/impressum` — HR predlošci s placeholderima za podatke tvrtke + oznaka „dati na pravnu provjeru". `Footer` (poveznice) + `CookieNotice` (minimalna, samo nužni kolačići, dismiss u localStorage), oboje u `layout.tsx`.
- **Već postojalo (provjereno, ništa mijenjano):** `app/robots.ts` (disallow /api/, sitemap), `app/sitemap.ts` (kategorije+regije+profili preko `/api/sitemap`), `lib/site.ts` (`NEXT_PUBLIC_SITE_URL`), `.Published()` = `IsPublished && !OptOut` na SVIM javnim upitima.
- **Verifikacija:** `tsc` + `next build` čisti (28 ruta; /pravila-privatnosti, /uvjeti-koristenja, /impressum statične). Backend brace-balans OK; **NIJE kompajliran** (sandbox nema .NET SDK). **Faza 6 NE traži novu migraciju.**
- **Preostalo vlasniku (OPS, ne kod):** kupiti domenu → `NEXT_PUBLIC_SITE_URL` u Vercel env; popuniti [NAZIV/OIB/ADRESA/EMAIL] u pravnim stranicama + pravna provjera; Google Search Console (dodati verifikacijski meta/DNS); finalna regresija; **merge `develop`→`main`** (tek nakon što `dotnet build` + regresija prođu). Napomena: opt-out forma je anonimna i skida odmah — ako se pojavi zloupotreba, prijeći na model s admin-odobrenjem.


## Sesija 2026-09-17 (b) — Faza 5: slike + očvršćivanje
- **Odobrenja:** sve odluke #1–#19 označene ✅ ODOBRENO u PLAN §11 (banner + retci). Kanonski repo banner dodan na vrh PLAN/STANJE/README; ispravljen krivi URL u `PLAN-ARHITEKTURA.md` (redak s "Repo:" pokazivao je na stari `wediplan.git` — GLAVNI uzrok zašto su nove sesije išle na krivi repo).
- **Slike (odluka #3):** `Media/` — `IPhotoStorage` (+ `LocalPhotoStorage` za dev, `R2PhotoStorage` za produkciju), `ImagePipeline` (ImageSharp: auto-orient, resize, WebP, opcionalni žig, thumbnail). `PhotosController` (owner-only upload/delete/order). csproj: +SixLabors.ImageSharp.Drawing, +AWSSDK.S3. `ProviderController.MyVendors` sada vraća `photos`.
- **Očvršćivanje:** rate limiting (global 300/min, liste 60/min) + `[EnableRateLimiting("lists")]` na vendors-list/pins/suggest; ForwardedHeaders iza `Proxy:TrustForwardedFor` (#17); `GET /api/health`; `UseStaticFiles` za `/uploads`.
- **Frontend:** `lib/images.ts` (apsolutni URL-ovi), `next.config.mjs` (uploads rewrite + CDN remotePattern), `providerApi` (uploadPhoto/deletePhoto/reorderPhotos), `ProviderDashboard` PhotoManager + CSS.
- **Ops/docs:** `ops/backup.sh` (pg_dump rotacija); DEPLOY.md (R2/Cloudflare/backup/monitoring/ImageSharp licenca), API.md (photo + health), PLAN §7 Faza 5 → implementirano.
- **Verifikacija:** frontend `tsc` + `next build` čisti (/partner, /admin u ruti). Backend NIJE kompajliran (sandbox nema .NET SDK/NuGet); brace-balans OK. Faza 5 **NE traži novu migraciju** (`vendor_photos` postoji od Faze 1).
- **Za vlasnika:** `git fetch`+merge bundle → `dotnet restore` (novi paketi) → `dotnet build`. Ako Faza 4 nije primijenjena: `dotnet ef migrations add Faza4 && dotnet ef database update` + `dotnet run -- --make-admin <email>`. Test: claim→odobri→recenzija→objava; upload fotografije u /partner (owner). Produkcija: postaviti R2 env + `Proxy:TrustForwardedFor=true` tek iza Cloudflarea.


## Odluka #18 — GDPR: ✅ RIJEŠENO (2026-09-16, novi repo WediPlan2)
Rad prebačen na novi repo **WediPlan2** s čistom poviješću (jedan initial commit).
`data/vendors-live.xlsx` NIJE u gitu — `.gitignore` pravilo `/data/vendors-live.xlsx` ga
blokira (`git check-ignore -v` potvrđuje), a anonimni klon ga ne sadrži. Stari repo
`MandriloST/wediplan` (sa zaraženom poviješću) ide na **private**. Novi repo ostaje **javan
dok razvoj traje** (Claude ga mora klonirati radi provjere paketa), na kraju projekta → private.
Pravilo: Claude nikad ne commita niti briše taj Excel (`.gitignore` ga drži izvan gita).

## Stalna pravila predaje (vrijede svaku sesiju)
- Rad isključivo na `develop` (ili `claude/*` → develop). `main` se ne dira.
- Na kraju svake sesije predati: **(1)** upute za pokretanje i testiranje, **(2)** kod kao
  **ZIP s punom strukturom foldera** (copy-paste preko root projekta radi ispravno) i/ili
  **patch/bundle** + točne git naredbe za merge.
- Build mora proći prije predaje: `npm run build` (frontend), `dotnet build` (backend).
- Ažurirati ovaj dokument (i PLAN-ARHITEKTURA.md ako se arhitektura mijenja) u istom commitu.

---

## ODLUKE POTVRĐENE 2026-09-14 (vlasnik)
1. **Import podataka:** opcija (b) — .NET import u Fazi 1 (ne Node sada). Frontend do Faze 2
   ostaje na mock JSON-u. Geokodiranje radi import (vlasnik NE popunjava koordinate ručno).
2. **Import alat:** .NET konzolna komanda.
3. **Baza/pretraga:** Postgres + pg_trgm (potvrđeno).
4. **GDPR kontakti:** uvezi u bazu, NE izlaži javno u `/api/vendors` (na klik kasnije).
5. **NOVO — Category-first (§L, odluka #12):** landing ne prikazuje sve pružatelje;
   pregledavanje po JEDNOJ kategoriji; paginacija pageSize 24 + "Učitaj još"; karta samo
   odabrane kategorije + jitter oko centroida. Defaulti a-d prihvaćeni. Novi endpoint
   `GET /api/categories`. Zapisano u PLAN §L, API.md, §7 Faza 1, §11 #12-#13.

## PREOSTALE OTVORENE ODLUKE (nisu blokeri za Fazu 1)
Hosting (#1), slike/R2 (#3), email/Resend (#5), pragovi oznaka (#9), potvrda §4.1 modela (#7).

---

## Sesija 2026-09-17 — Faza 4: claim + admin + korisničke recenzije ✅ (kod predan; .NET build/migracija na vlasniku)

**Cilj (DoD §7 Faza 4):** put registracija pružatelja → claim → (uređivanje drafta) → admin
odobri (draft objavljen, `claimed`) → korisnik napiše recenziju → admin objavi → vidljiva na profilu.

**Backend (`backend/Wediplan.Api/`):**
- `Domain/ProviderEntities.cs` — `Claim` (pending|approved|rejected, `evidence=domain_match`),
  `UserReview` (pending|published|rejected, 1 po korisnik+pružatelj), `VendorDraft` (1:1; about/
  usluge/cijena/stil — objava odvojena od žive verzije, §6.3), `Subscription` (§M.4, prazna).
- `Data/AppDbContext.cs` — DbSetovi + mapiranja (unique (user,vendor) za claim i recenziju;
  FK cascade na users/vendors; draft PK=vendor_id). `Data/ProviderMapper.cs` — draft seed/apply/
  objava + `domain_match` (email domena == web domena profila).
- Kontroleri: `ClaimsController` (`POST /api/claims`, `GET /api/claims/mine` — dodjeljuje rolu
  provider, seeda draft), `ProviderController` (`GET /provider/vendors` s draftom+statistikom 30 dana
  iz `daily_stats`; `PUT …/draft`; `POST …/publish` samo vlasnik), `ReviewsController`
  (`POST /api/reviews` → moderacija), `AdminController` [Authorize admin] (moderacija claimova/
  recenzija, publish/unpublish). `VendorsController.Get` sada vraća `userReviews` (published).
- `Contracts/ProviderContracts.cs` (+ `VendorProfileDto.UserReviews`). `Program.cs` — CLI
  `--make-admin <email>`. `db/schema.sql` dopunjen (referenca; izvor istine = EF migracija).
- **Počišćeno:** uklonjena dva greškom commitana prazna filea (`------`, `Data/AppDbContext.cs(49`).

**Frontend:**
- `lib/types.ts` (+UserReview/Claim/ProviderVendor/VendorDraft/AdminClaim/AdminReview; profil +userReviews).
  `lib/api/provider.ts` (claimApi/providerApi/reviewApi/adminApi + `providerMessage`).
- `components/VendorProfile.tsx` — Wediplan recenzije (lista) + `ReviewForm` (zvjezdice+tekst→moderacija)
  + `ClaimPanel` (preuzmi profil). `components/ProviderDashboard.tsx` + `/partner` (uredi draft,
  statistika, objava). `components/AdminPanel.tsx` + `/admin` (moderacija). Header "Za partnere" → `/partner`.
- CSS u `globals.css` (claim/review/prov/admin). **Bez mock ruta** za nove endpointe: u mock načinu
  (bez backenda) korisnik nikad nije prijavljen pa se auth-ovisne akcije ne pozivaju (isti obrazac
  kao Faza 3 — puni tok testira se protiv .NET-a).

**Odluka [ZA ODOBRENJE #19]:** objavljene korisničke recenzije zasad NE mijenjaju `vendor.rating`/
`reviewCount` (ostaju iz importa); prikazuju se zasebno kao "Wediplan recenzije". Stapanje kad bude podataka.

**Verificirano:** `npx tsc` čist; `npm run build` prolazi (mock način; `/partner` i `/admin` u ruti).
**NIJE u sandboxu (NuGet blokiran):** `dotnet build`, `dotnet ef migrations add Faza4`, `database update`,
`--make-admin` — na vlasniku (koraci u DEPLOY.md). EF fluent mapiranja slijede isti obrazac kao Faza 3.

**Optimizacija dokumentacije (na zahtjev):** dnevnik podijeljen — `STANJE.md` sad drži samo aktualno
stanje + pravila + zadnje sesije; starije sesije preseljene u **`STANJE-ARHIVA.md`** (sadržaj očuvan,
ništa obrisano). Obavezno štivo prije koda je manje → manji token-trošak budućih sesija.

---

## Sesija 2026-09-16 (6) — Faza 3 ZAVRŠENA: auth + favoriti/plan sync ✅ (sažetak)

DoD §5 ispunjen: prijava (lozinka/magic/Google-uvjetno), verifikacija/reset, favoriti+plan sync
(gost localStorage → account merge pri prijavi, mirror na server). Backend Identity nad Guid,
cookie sesija; `FavoritesController` (`/api/favorites` + `merge`); frontend `useAuth`, `/prijava/*`,
`AccountSync`. **Puni detalji Faze 3 (backend/UI/sync) i svih ranijih faza: `STANJE-ARHIVA.md`.**

---

> **Starije sesije (Faza 0–3 detaljno, Zadaci A–D, geokod, importi):** `STANJE-ARHIVA.md`.
