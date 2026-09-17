# STANJE.md — dnevnik rada i trenutno stanje projekta

> 📍 **KANONSKI REPO: https://github.com/MandriloST/WediPlan2.git** (grana `develop`). Ovo je JEDINI ispravan repo — NE `WediPlan`/`wediplan` bez 2.


> **Namjena:** model koji nastavlja rad čita OVO + `PLAN-ARHITEKTURA.md` + `API.md` prije koda.
> Ažurira se na kraju SVAKE radne sesije (kratko, činjenično). Novije sesije na vrhu.
> Uvijek provjeriti i stvarni `git log` — repo je izvor istine, ovo je sažetak.

## Repo: **WediPlan2** (novi, čist — GDPR #18 riješen). Javan dok razvoj traje; na kraju → private.
## Trenutna faza: **Faza 5 (slike + očvršćivanje) — IMPLEMENTIRANA ✅ (2026-09-17)**. Frontend build/tsc čisti; backend kod predan (traži `dotnet restore`+`build`; NEMA nove migracije). Sve odluke #1–#19 ODOBRENE. Sljedeće: **Faza 6** (lansiranje: domena, pravne stranice §9, Search Console, merge u `main`).

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
