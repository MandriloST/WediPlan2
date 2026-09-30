# Plan: treći val — točne lokacije na karti, audit (GDPR), backup, moderacija slika i recenzija

> **Namjena:** ovaj dokument daješ Claude Sonnetu (ili drugom modelu) kao specifikaciju. Svaki zadatak je
> samostalan, ima točne datoteke, provjerene uzorke iz postojećeg koda kojih se treba držati, i kriterij
> „gotovo". Izvor istine je repo `WediPlan2` grana `develop`. **Prije koda pročitati** `STANJE.md`,
> `PLAN-ARHITEKTURA.md`, `API.md` (+ ovaj dokument). Ako se arhitektura mijenja → ažurirati `PLAN-ARHITEKTURA.md`.
>
> Nastavak `PLAN-PRIORITETI-LANSIRANJE.md` (zadaci 1–4) i `PLAN-PRIORITETI-LANSIRANJE-2.md` (zadaci 5–9), oba
> provedena. Numeracija se nastavlja: **zadaci 10–18**.
>
> **Svaki zadatak = zasebna grana + zaseban PR/merge u `develop`.** Ne miješati ih. Na kraju svakog zadatka
> ažurirati `STANJE.md` (nova sesija na vrhu, kratko i činjenično), a `API.md` ako se mijenja API ugovor.

## Redoslijed rada i ovisnosti

```
Faza A (paralelno, bez sheme):   10 importer ─┐   11 karta ─┐   18 JSON-LD ─┐
Faza B (ops, vlasnik):           12 backup ───┤ (obavezno PRIJE primjene migracije iz 13 na bilo koju stvarnu bazu)
Faza C (shema, redom):           13 migracija → 14 audit → { 15 slike | 16 recenzije | 17 privola } (međusobno neovisni)
```

Obrazloženje: 10 popravlja stvarni gubitak podataka (ide prvi). 11 i 18 su čisti frontend, nula rizika. 12 mora
postojati prije bilo kakve migracije nad stvarnim podacima. 13 uvodi SVE nove stupce u **jednoj** migraciji da
15–17 ne trebaju svoje. 14 ide prije 15–17 da njihove admin akcije budu automatski auditirane.

**Grane:** `fix/import-protect-claimed` (10), `feat/map-exact-pins` (11), `fix/jsonld-own-ratings` (18),
`ops/backup-hardening` (12), `feat/schema-audit-moderation` (13), `feat/audit-log` (14),
`feat/photo-moderation` (15), `feat/review-verification` (16), `feat/data-provenance` (17).

**Tko radi što (preporuka vlasniku):** 10, 11, 12, 15, 16, 17, 18 su dobro specificirani i prikladni za Sonnet.
13 i 14 diraju shemu i sve putove pisanja — dati ih Opusu ili Sonnetu uz obavezan pregled diffa prije mergea.

## Status
- [x] **Zadatak 10 — Importer ne gazi izmjene partnera na preuzetim profilima.** (backend, bez sheme) — **kod gotov na grani `fix/import-protect-claimed` (2026-09-30); čeka vlasnikov `dotnet build` + `dotnet test` i merge.**
- [x] **Zadatak 11 — Zeleni pin za točnu lokaciju + legenda karte.** (frontend, bez sheme) — **gotovo na grani `feat/map-exact-pins` (2026-09-30); `tsc` + `npm run build` čisti, provjereno u headless Chromiumu.**
- [x] **Zadatak 18 — JSON-LD `aggregateRating` samo iz vlastitih recenzija.** (frontend, bez sheme) — **gotovo na grani `fix/jsonld-own-ratings` (2026-09-30); `tsc` + `npm run build` čisti, ponašanje provjereno izvršavanjem.**
- [~] **Zadatak 12 — Backup: custom format, enkripcija, off-site, testirani restore, slike.** (ops) — **skripte i dokumentacija gotove na grani `ops/backup-hardening` (2026-09-30), provjerene nad Postgresom 16; čeka vlasnikov lokalni backup + `restore-test.sh` (kriterij „gotovo“) i merge.**
- [ ] **Zadatak 13 — Migracija `AuditIModeracija` (sva nova shema odjednom).** (backend, +migracija)
- [ ] **Zadatak 14 — Audit log (interceptor) + admin pregled povijesti.** (backend+frontend, bez nove sheme)
- [ ] **Zadatak 15 — Moderacija slika (post-moderacija, evidencija admina).** (backend+frontend)
- [ ] **Zadatak 16 — Recenzije: evidencija odluka + verifikacija uvezenih.** (backend+frontend)
- [ ] **Zadatak 17 — Porijeklo podataka i privola pružatelja (Excel + import + admin).** (backend+skripta)

## Odluke potvrđene s vlasnikom (2026-09-30) — obvezujuće za izvedbu

Vlasnik je prihvatio sve preporuke iz analize („sve po tvojim preporukama"):

1. **Zeleni pin = `LocationPrecision == "exact"`.** Zelena ide na obrub + točku u pinu (ne puna pozadina), tako da
   se kombinira s postojećim prigušenim stilom „na upit" (`.pin.upit`). Klasteri ostaju plavi. Boja nikad nije
   jedini nositelj značenja (aria-label + točka).
2. **Legenda** ide samo tamo gdje ima pinova: `ExploreShell` i `MapPageShell` (landing karta ima `vendors={[]}`).
   Donji lijevi kut; na mobitelu sklopiva.
3. **Nema nove baze od nule.** Backup → testirani restore → EF migracija nad postojećim podacima. Za postojeće
   retke **ne izmišljati datume**: novi vremenski stupci za staru povijest su `nullable` i ostaju `null`.
4. **Audit:** jedna append-only tablica `audit_log`, puni se automatski (EF `SaveChangesInterceptor`).
   Kontakt polja (telefon, e-mail, IG, FB) bilježe se **bez vrijednosti** (`{"changed":true}`). Draftovi se ne
   auditiraju (javna promjena se vidi na `Vendor` pri objavi). Rok čuvanja **24 mjeseca**. Kod brisanja računa
   `ActorUserId` ostaje (pseudonimni GUID koji nakon brisanja korisnika više ne pokazuje ni na koga).
5. **Opt-out se bilježi i u bazu**, minimalno (koji profil, kad; **bez** razloga i kontakta) — mijenja raniju
   odluku „samo log" radi dokaza poštivanja zahtjeva (GDPR čl. 5(2)). Postojeći log redak se ne dira.
6. **Slike: post-moderacija.** Upload odmah ide javno sa statusom `unreviewed`; admin ga označi `approved` ili
   `flagged`. `flagged` = skriveno iz javnog prikaza (vlasnik ga i dalje vidi s napomenom). Potvrda prava na
   fotografiju je **obavezna** pri uploadu.
7. **Uvezene recenzije:** javno se prikazuju sve osim `rejected`; `verified` dobiva bedž. Importer ih više ne briše
   i ponovno stvara, nego radi upsert po stabilnom ključu (status provjere se čuva).
8. **Korisničke recenzije:** dodaje se `DecidedBy` + opcionalni `RejectReason`.
9. **Importer i preuzeti profili:** za `ClaimStatus == "claimed"` importer **ne dira polja kojima upravlja partner**
   (`About`, `Services`, `PriceKind/From/To`, `StyleTags` — točno polja `VendorDraft`/`ApplyToVendor`). Vrijedi i
   kad je vlasnik obrisao račun (tada `OwnerUserId == null`, ali `ClaimStatus` ostaje `claimed`).
10. **Privola:** `refused` → importer postavlja `OptOut = true`. Importer **nikad** ne skida `OptOut`. Odobren
    claim → `ConsentStatus = "granted"`, kanal `claim`.
11. **Google Places (budućnost):** sada se dodaje samo stupac `GooglePlaceId`. Google rating i broj recenzija se
    **nikad ne spremaju u bazu i nikad ne ulaze u JSON-LD**. V. sekciju „Budući rad" na dnu.
12. **JSON-LD `aggregateRating`** računa se samo iz vlastitih objavljenih korisničkih recenzija (Zadatak 18).

---

## Kontekst koda (PROVJERENO u repou na `develop`, commit `fb1d79e` — ne nagađati)

### Stack i ograničenja okruženja
- Backend: .NET 8, EF Core 8.0.11 + Npgsql, ASP.NET Identity (Guid ključevi), xUnit + EF InMemory u testovima.
- Frontend: Next.js 14 App Router, TanStack Query, Zustand, MapLibre GL.
- **Sandbox modela nema pristup `api.nuget.org`** (isto kao Zadaci 5/7/9): `dotnet build`, `dotnet test` i
  `dotnet ef migrations add` **pokreće vlasnik**. Model u tom slučaju piše kod, ručno ga pregleda i u `STANJE.md`
  jasno napiše „backend build/test NEPOTVRĐEN" + točne naredbe koje vlasnik mora pokrenuti.
- **Model NE piše ručno datoteke migracija.** Mijenja entitete + `AppDbContext`, a migraciju generira vlasnik.
- `npm` radi u sandboxu → frontend zadaci se provjeravaju `npx tsc --noEmit` + `npm run build`.

### Lokacija i karta (meta #11)
- `backend/Wediplan.Api/Domain/Entities.cs` — `Vendor.Lat/Lng` (`double?`), `Vendor.LocationPrecision`
  (`"exact" | "city" | "region"`, default `"region"`).
- `backend/Wediplan.Api/Import/ExcelImporter.cs:116` — `precision = coords != null ? "exact" : (city.Length > 0 ? "city" : "region")`.
- `PinsController` vraća `LocationPrecision`; `lib/types.ts` → `PinVendor` (Pick, uključuje `locationPrecision`).
- `components/CroatiaMap.tsx`:
  - `toFC(vendors)` gradi GeoJSON; `properties` već ima `approx: v.locationPrecision === "city" ? "1" : ""`.
    Pinovi postoje samo za pružatelje s koordinatama (`spreadPositions`, `lib/jitter.ts`) — `region` nema pin.
  - U `sync()` ne-klaster pin: `el.className = props.upit ? "pin upit" : "pin";`,
    `el.setAttribute("aria-label", \`${props.name}, ${props.priceFull}\`)`.
  - Popup (`popupContent`) već prikazuje „lokacija približna (grad)" za `approx`.
- `app/globals.css`: `.pin` (~206), `.pin:hover`, `.pin.selected`, `.pin-cluster`, `.pin.upit` (~588),
  `.map-hint` (gore lijevo), `.map-fab` (dolje desno). Varijable: `--success: #1f9d5b`, `--success-tint: #e7f6ee`.
- Karta se koristi u: `components/ExploreShell.tsx` (ima `map-hint`, `map-fab`), `components/MapPageShell.tsx`
  (fullscreen `/karta`, ima `map-catpick` gore i `map-fab`), `components/LandingShell.tsx` (`vendors={[]}` — bez pinova).

### Import (meta #10, #16, #17)
- CLI: `dotnet run -- --import <xlsx> [--dry-run] [--no-geocode] [--geocode-retry]` → `Program.cs RunImportAsync`
  → `new ExcelImporter(db, geocoder, dryRun)` (ručno konstruiran, `db` iz `CreateScope()`).
- `ExcelImporter.UpsertAsync(parsed, reviews, ct)` (~251): za postojeći slug **prepisuje sva skalarna polja**
  (uklj. `About`, `Services`, `Price*`, `StyleTags`, kontakte, koordinate), zatim
  `_db.ImportedReviews.RemoveRange(existing.ImportedReviews)` i dodaje nove. **Ne provjerava `ClaimStatus`.**
  `OptOut` i `ClaimStatus` ne dira (dobro).
- Izvještaj: `_warnings` (`Warn(rowNo, name, msg)`), `_skipped`, `WriteReport()` → `import-report.txt`.
- Recenzije: list „Recenzije", stupci `naziv_pruzatelja, autor, ocjena, tekst, izvor, godina`.
- Stupci lista „Pružatelji": `naziv*, kategorija*, dodatne_kategorije, regija*, grad, koordinate, pokriva_regije,
  pokrivanje_napomena, nacin_cijene*, cijena_od*, cijena_do, ocjena, broj_recenzija, izvor_ocjene, provjereno,
  kalendar_uzivo, stil, o_pruzatelju, usluge, instagram, facebook, web, telefon, email, status, napomena`.
- Predložak generira `scripts/make-template.py`; frontend mock import je `scripts/import-vendors.mjs`
  (čita stupce po imenu zaglavlja — nepoznate stupce ignorira; provjeriti pri Zadatku 17).
- `ProviderMapper.ApplyToVendor(VendorDraft d, Vendor v)` kopira točno: `About, Services, PriceKind, PriceFrom,
  PriceTo, StyleTags` (+ `UpdatedAt`). **To su polja kojima upravlja partner.**

### Entiteti koji se mijenjaju (meta #13)
- `Vendor` — v. gore; ima `CreatedAt`, `UpdatedAt`; kontakti `Website, Phone, Email, SocialInstagram, SocialFacebook`
  (interni, **nikad** u javnom `VendorDto`); `ClaimStatus`, `OwnerUserId`, `IsPublished`, `OptOut`; `Search`
  (tsvector, ignoriran izvan Npgsql).
- `VendorPhoto { Id, VendorId, StorageKey, SortOrder, IsCover }` — **bez ikakvih vremena i autora**.
- `ImportedReview { Id, VendorId, Author, Rating, Text, Source, Year }` — bez vremena, bez statusa.
- `UserReview { Id, VendorId, UserId, Rating, Text, Status (pending|published|rejected), CreatedAt, DecidedAt }` — **nema `DecidedBy`**.
- `Claim` — kompletan (`DecidedBy`, `DecidedAt`), ne dira se.
- `AppDbContext.OnModelCreating`: `var isNpgsql = Database.IsNpgsql();` — Postgres-specifično (ekstenzije, GIN,
  tsvector) **samo unutar `isNpgsql`**. `HasColumnType("text[]")` i `"jsonb"` se već koriste izvan grane i rade
  pod InMemory. snake_case imena stupaca konfigurirana centralno.
- Zadnja migracija: `20260923115834_ClaimVerification`.

### Pisanja koja zaobilaze ChangeTracker (važno za #14)
- `AccountController.Delete`: `_db.Vendors.Where(v => v.OwnerUserId == uid).ExecuteUpdateAsync(...)` (odvezivanje
  vlasništva), `ExecuteDeleteAsync` na tokenima i magic linkovima, pa `_users.DeleteAsync(user)`; sve u transakciji;
  log `account deleted: user={UserId}`.
- `FavoritesController` — `ExecuteDeleteAsync` na favoritima/budžetu (osobni podaci korisnika, **ne auditira se**).
- `OptOutController.Submit` (anoniman, `[EnableRateLimiting("writes")]`): `vendor.OptOut = true; SaveChanges;`
  + `_log.LogWarning("GDPR opt-out: vendor=… razlog=… kontakt=…")`.

### Admin i partner API (meta #14–#16)
- `AdminController` — `[Route("api/admin")] [Authorize(Roles = Roles.Admin)]`, DI: `AppDbContext`,
  `UserManager<AppUser>`, `PartnerEmails`, `ILogger`, `ClaimApprovalService`. `Uid()` helper.
  Rute: `claims` (GET, approve, reject), `reviews` (GET `?status=`, approve, reject — postavljaju `Status` +
  `DecidedAt`, 409 `already_decided`), `optouts`, `vendors/{slug}/restore-optout|publish|unpublish`.
- `PhotosController` — `[Route("api/provider/vendors/{slug}/photos")] [Authorize]`; `OwnedAsync(slug)` (samo
  odobreni vlasnik); `Upload(string slug, IFormFile? file)` (multipart polje `file`, `RequestSizeLimit` 15 MB,
  obrada `ImagePipeline` → `{base}.webp` + `{base}_thumb.webp`, prva slika = cover); `Delete`; `Reorder`.
- `ProviderPhotoDto(string Id, string Url, string ThumbUrl, bool IsCover, int SortOrder)` u `ProviderContracts.cs`;
  `ProviderMapper.PhotoDto(photo)`, `ProviderMapper.ThumbUrl(url)`, `ProviderMapper.MaskEmail(email)`.
- Javni prikaz slika: `VendorMapper` (~59, `v.Photos.OrderBy(SortOrder).Select(StorageKey)`), `PinsController` (~53,
  cover = prva po `SortOrder`), `VendorsController` (`Include(v => v.Photos)` na tri mjesta).
- Javni profil: `VendorsController` (~85–110) — uvezene recenzije `OrderByDescending(Year)` →
  `ImportedReviewDto(Author, Rating, Text, Source, Year)`; korisničke samo `Status == "published"`.
- Frontend: `lib/api/provider.ts` — `providerApi.uploadPhoto(slug, file)` (FormData, `fetch` s `credentials`),
  `adminApi` (claims/reviews/optouts/publish…). `components/AdminPanel.tsx` (`/admin`, učitava
  `Promise.all([claims, reviews, optouts])`), `components/ProviderDashboard.tsx` (`/partner`, upload ~184).
  Tipovi u `lib/types.ts` (`AdminClaim`, `AdminReview`, `AdminOptOut`, `ProviderPhoto`, `ImportedReview`…).

### Testovi / CI (mora ostati zeleno)
- `backend/Wediplan.Api.Tests/`: `HealthEndpointTests`, `RateLimitingTests` (puni app preko
  `WebApplicationFactory<Program>`; uklanjaju descriptor `DbContextOptions<AppDbContext>` i dodaju
  `AddDbContext(o => o.UseInMemoryDatabase(...))`), `ReviewsControllerTests`, `ClaimVerificationTests`
  (`BuildServices()` s `AddIdentityCore` + InMemory, `ControllerAs(...)` s `ClaimsPrincipal`).
- `.github/workflows/ci.yml` — backend restore/build/test + frontend `npm ci`/`tsc --noEmit`/`npm run build`.
- **Ne dirati** `public partial class Program {}` na dnu `Program.cs`.

---

## Zadatak 10 — Importer ne gazi izmjene partnera na preuzetim profilima

**Cilj:** ponovni Excel import trenutno tiho prepisuje opis, usluge, cijene i stil koje je partner uredio i objavio.
Za preuzete profile (`ClaimStatus == "claimed"`) ta polja preskočiti i zabilježiti upozorenje. **Bez migracije.**

**Datoteke:**
- `backend/Wediplan.Api/Import/ImportMerge.cs` (novo) — čista statička logika, da je testabilna bez Excela.
- `backend/Wediplan.Api/Import/ExcelImporter.cs` (izmjena) — `UpsertAsync` koristi `ImportMerge`.
- `backend/Wediplan.Api.Tests/ImportMergeTests.cs` (novo).

**Koraci:**
1. `ImportMerge.Apply(Vendor existing, Vendor incoming) → IReadOnlyList<string> skippedFields`:
   - Uvijek kopira polja kojima upravlja Wediplan: `Name, CategorySlug, RegionSlug, Country, City, Lat, Lng,
     LocationPrecision, CoverageAll, CoverageRegions, CoverageNote, Rating, ReviewCount, RatingSource, Verified,
     LiveCalendar, Website, Phone, Email, SocialInstagram, SocialFacebook` (isti skup kao danas).
   - Polja partnera (`About, Services, PriceKind, PriceFrom, PriceTo, StyleTags`): ako
     `existing.ClaimStatus == "claimed"` → **ne kopira**, a u `skippedFields` dodaje ime polja **samo ako se
     vrijednost razlikuje** (liste uspoređivati `SequenceEqual`). Inače kopira kao danas.
   - Postavlja `existing.UpdatedAt = DateTime.UtcNow`.
   - Konstante polja držati u jednom `static readonly string[] PartnerManagedFields` s komentarom da mora pratiti
     `ProviderMapper.ApplyToVendor`.
2. U `UpsertAsync` zamijeniti blok „ažuriraj skalarna polja" pozivom `ImportMerge.Apply`. Ako je
   `skipped.Count > 0` → `Warn(rowNo, name, $"profil preuzet od partnera — polja nisu prepisana: {string.Join(", ", skipped)}")`.
   `UpsertAsync` danas nema `rowNo` — proslijediti ga (npr. mapa slug → rowNo iz parsiranja) ili koristiti
   `Warn(0, name, …)` i u poruku dodati slug. Kategorije i uvezene recenzije ostaju kako jesu (recenzije mijenja Zadatak 16).
3. Testovi (`ImportMergeTests`, čiste jedinice bez baze):
   - `Unclaimed_CopiesPartnerFields`
   - `Claimed_KeepsPartnerFields_AndReportsOnlyChangedOnes`
   - `Claimed_StillUpdatesWediplanFields` (npr. `Phone`, `Lat`)
   - `Claimed_WithDeletedOwner_StillProtected` (`OwnerUserId = null`, `ClaimStatus = "claimed"`)

**Ne raditi:** ne mijenjati `VendorDraft`, ne dirati `OptOut`/`ClaimStatus`/`OwnerUserId`.

**Kriterij gotovo:** `dotnet build` + `dotnet test` zeleno (vlasnik); `--dry-run` import i dalje radi;
`import-report.txt` prikazuje upozorenje za preuzeti profil s promijenjenim poljima. `STANJE.md` ažuriran.

---

## Zadatak 11 — Zeleni pin za točnu lokaciju + legenda karte

**Cilj:** pinovi s `locationPrecision === "exact"` zeleni, ostali zadani; legenda objašnjava razliku. **Čisti frontend.**

**Datoteke:**
- `components/CroatiaMap.tsx` (izmjena)
- `components/MapLegend.tsx` (novo)
- `components/ExploreShell.tsx`, `components/MapPageShell.tsx` (izmjena — ubaciti legendu)
- `app/globals.css` (izmjena)

**Koraci:**
1. `toFC`: u `properties` dodati `exact: v.locationPrecision === "exact" ? "1" : ""` (uz postojeći `approx`).
2. `sync()` (ne-klaster grana): klase složiti iz dijelova, npr.
   `el.className = ["pin", props.upit && "upit", props.exact && "exact"].filter(Boolean).join(" ");`
   aria-label: `${props.name}, ${props.priceFull}${props.exact ? ", točna lokacija" : ", približna lokacija"}`.
   Za točku dodati `<span class="pin-dot" aria-hidden="true"></span>` ispred teksta cijene (tekst postaviti
   preko `textContent` na zasebnom `<span>`, ne preko `innerHTML` s podacima).
3. `globals.css`:
   - `--exact: var(--success);` u `:root`.
   - `.pin.exact { border-color: var(--exact); color: var(--exact); box-shadow: 0 2px 8px rgba(31,157,91,.25); }`
   - `.pin.exact.selected { background: var(--exact); color: #fff; }`
   - `.pin.upit.exact { border-color: var(--exact); }` (kurziv i prigušen tekst iz `.pin.upit` ostaju)
   - `.pin .pin-dot` — mala točka (6px, `border-radius: 50%`), vidljiva samo za `.pin.exact .pin-dot`
     (`background: var(--exact)`); za ostale `display: none`.
   - Klasteri se **ne mijenjaju**.
4. `MapLegend.tsx`: mala komponenta, props `className?`. Sadržaj (HR):
   - „● točna lokacija" (zelena točka + primjer mini-pina)
   - „○ približno — centar grada"
   - „*kurziv* = cijena na upit"
   Desktop: uvijek vidljivo. Mobitel (`max-width: 640px`): `<details>` sa `<summary>Legenda</summary>`, zatvoreno.
   Stil `.map-legend`: `position:absolute; left:14px; bottom:14px; z-index:5;` isti vizualni jezik kao `.map-hint`
   (poluprozirna bijela pozadina, `--r-card`, `font-size: 12.5px`). Ne smije se preklapati s `.map-fab` (dolje
   desno) ni s MapLibre atribucijom (`attributionControl: { compact: true }` je dolje desno — provjeriti).
5. Ubaciti `<MapLegend />` u `ExploreShell` (unutar `map-wrap`, samo kad **nije** `browsing`, jer tada nema pinova)
   i u `MapPageShell` (samo kad je `category` odabran). **Ne** u `LandingShell`.

**Kriterij gotovo:** `npx tsc --noEmit` + `npm run build` čisti; ručno: `/kategorije` s odabranom kategorijom
prikazuje zelene i zadane pinove, „na upit" pin s točnom lokacijom je zelen i kurziv; legenda vidljiva na
desktopu, sklopiva na mobitelu, ne prekriva gumb „Budžet"; screen reader čita „točna/približna lokacija".
`STANJE.md` ažuriran (s napomenom vlasniku da `exact` znači „koordinate upisane u Excel", ne „provjereno").

---

## Zadatak 18 — JSON-LD `aggregateRating` samo iz vlastitih recenzija

**Cilj:** `lib/jsonld.ts` danas šalje `vendor.rating`/`vendor.reviewCount` kao `aggregateRating`. Te vrijednosti
dolaze iz Excela (`ocjena`, `broj_recenzija`, `izvor_ocjene`) — ocjene s drugih stranica (npr. Google). Googleove
smjernice za review snippete ne dopuštaju agregiranje ocjena s drugih stranica, a to je i temelj pravila iz
Odluke 11 (Google rating nikad u JSON-LD). **Čisti frontend.**

**Datoteke:** `lib/jsonld.ts` (izmjena).

**Koraci:**
1. `aggregateRating` računati iz `data.userReviews` (objavljene recenzije registriranih korisnika; tip
   `UserReview { rating }` u `lib/types.ts`): `count = userReviews.length`, `avg` zaokružen na 1 decimalu.
2. Uključiti samo kad je `count > 0`. Inače izostaviti (i kad `vendor.reviewCount > 0`).
3. Ažurirati komentar u datoteci: zašto se ne koristi `vendor.rating` (vanjski izvor, `ratingSource`).
4. Vizualni prikaz ocjene na profilu (`VendorProfile.tsx`, `ratingSource`) se **ne mijenja** u ovom zadatku.

**Kriterij gotovo:** `tsc` + `build` čisti; profil bez objavljenih korisničkih recenzija nema `aggregateRating` u
HTML-u; profil s njima ima točan prosjek. `STANJE.md` ažuriran.

---

## Zadatak 12 — Backup: custom format, enkripcija, off-site, testirani restore, slike

**Cilj:** postojeći `ops/backup.sh` (plain SQL + gzip, lokalno, rotacija 14 dana) nadograditi tako da backup
preživi gubitak servera, da je enkriptiran (sadrži osobne podatke) i da je restore dokazano ispravan.
**Obavezno prije primjene migracije iz Zadatka 13 na bilo koju stvarnu bazu.**

**Datoteke:**
- `ops/backup.sh` (izmjena)
- `ops/restore-test.sh` (novo)
- `ops/backup-photos.sh` (novo)
- `DEPLOY.md` §4 „Backup baze" (prepisati)

**Koraci:**
1. `backup.sh`:
   - `pg_dump -Fc` (custom format; `.dump`), ime `wediplan-YYYYMMDD-HHMMSS.dump`.
   - Ako je postavljen `BACKUP_AGE_RECIPIENT` (javni ključ za [age](https://github.com/FiloSottile/age)):
     `pg_dump -Fc … | age -r "$BACKUP_AGE_RECIPIENT" > …dump.age`; inače upozorenje u log i nekriptirani `.dump`.
   - Ako je postavljen `BACKUP_RCLONE_REMOTE` (npr. `r2:wediplan-backups/db`): `rclone copy` nakon uspjeha;
     pad uploada = exit ≠ 0 (cron ga mora vidjeti).
   - Rotacija lokalno `KEEP_DAYS` (default 14) — isti rok dokumentirati za remote (lifecycle pravilo na bucketu).
   - Zadržati podršku za `WEDIPLAN_DB` i `PG*` varijable. `set -euo pipefail`, `pipefail` mora uhvatiti pad `pg_dump`-a.
2. `restore-test.sh <datoteka>`: dekriptira ako `.age` (`age -d -i "$BACKUP_AGE_IDENTITY"`), kreira privremenu bazu
   `wediplan_restore_test_<stamp>`, `pg_restore --no-owner --no-privileges`, provjeri `select count(*)` za
   `vendors`, `users`, `user_reviews`, `vendor_photos`, `__EFMigrationsHistory` (ispiše zadnju migraciju), zatim
   `dropdb`. Exit ≠ 0 ako bilo što padne.
3. `backup-photos.sh`: ako je storage R2 → `rclone sync` bucketa na odvojeni remote/prefix; ako lokalni
   (`LocalPhotoStorage`) → `rclone sync` direktorija uploads. Putanje/imena iz env varijabli, ne hardkodirati.
4. `DEPLOY.md` §4: cron primjeri (DB dnevno 03:00, slike dnevno 03:30, `restore-test.sh` tjedno), generiranje
   `age` ključa (`age-keygen`; privatni ključ **nikad** na serveru, samo kod vlasnika + sigurna kopija),
   rclone konfiguracija za R2 (EU jurisdikcija), i **lokalna Docker varijanta** za razvoj:
   ```bash
   cd backend
   docker compose exec -T postgres pg_dump -U wediplan -d wediplan -Fc > wediplan-$(date +%Y%m%d-%H%M).dump
   docker compose exec -T postgres createdb -U wediplan wediplan_restore_test
   docker compose exec -T postgres pg_restore -U wediplan -d wediplan_restore_test --no-owner < wediplan-XXXX.dump
   docker compose exec -T postgres psql -U wediplan -d wediplan_restore_test -c "select count(*) from vendors;"
   docker compose exec -T postgres dropdb -U wediplan wediplan_restore_test
   ```
5. U `DEPLOY.md` dodati kratki GDPR odlomak: backupi sadrže osobne podatke; brisanja se ne primjenjuju retroaktivno
   na backupe, nego backupi istječu nakon `KEEP_DAYS`; pri restoreu se ponovno primjenjuju brisanja iz tog
   razdoblja (izvor: `audit_log` akcije `account_deleted` i `optout` nakon Zadatka 14). Isto navesti u politici
   privatnosti (vlasnik, pravna provjera).
6. **Runbook „prije migracije"** (u `DEPLOY.md`): backup → `restore-test.sh` na tom backupu → tek onda
   `dotnet ef database update` / deploy.

**Kriterij gotovo:** `bash -n` i `shellcheck` (ako dostupan) čisti na sve tri skripte; vlasnik lokalno napravio
backup i uspješan `restore-test.sh`. `STANJE.md` ažuriran.

---

## Zadatak 13 — Migracija `AuditIModeracija` (sva nova shema odjednom)

**Cilj:** jedna migracija koja dodaje sve stupce/tablice potrebne za Zadatke 14–17 i buduću Google Places
integraciju. **Ovaj zadatak mijenja samo shemu i minimalno postavljanje novih polja pri stvaranju redaka** —
logika moderacije/audita dolazi u 14–17. Preduvjet: Zadatak 12 gotov.

**Datoteke:** `Domain/Entities.cs`, `Domain/ProviderEntities.cs`, `Domain/AuditEntities.cs` (novo),
`Data/AppDbContext.cs`, `Controllers/PhotosController.cs` (samo postavljanje polja), `API.md` (samo napomena).

**1. Nova tablica `audit_log`** (`Domain/AuditEntities.cs`):
```csharp
public class AuditLog
{
    public long Id { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string ActorType { get; set; } = "system"; // admin | partner | user | public | import | system
    public Guid? ActorUserId { get; set; }            // bez FK — mora preživjeti brisanje korisnika
    public string EntityType { get; set; } = "";      // vendor | vendor_photo | imported_review | user_review | claim | user
    public string EntityId { get; set; } = "";        // Guid kao string
    public string Action { get; set; } = "";          // create | update | delete | optout | account_deleted | owner_unlinked | …
    public string? Changes { get; set; }              // jsonb: {"polje":{"old":…,"new":…}} ili {"polje":{"changed":true}}
    public string? Source { get; set; }               // npr. "api:PUT /api/provider/…", "import:vendors-2026-09.xlsx"
    public string? Note { get; set; }
}
```
Mapiranje: `ToTable("audit_log")`, `Changes` → `HasColumnType("jsonb")`, indeksi `(EntityType, EntityId, OccurredAt)`,
`(ActorUserId)`, `(OccurredAt)`. **Bez FK-ova.** `DbSet<AuditLog> AuditLogs`.

**2. `Vendor` — porijeklo, privola, Google** (sve admin-interno, **nikad** u javni DTO):
| Svojstvo | Tip | Default (i za postojeće retke) |
|---|---|---|
| `DataSource` | `string?` | `null` (vrijednosti: `google_maps`, `web`, `instagram`, `facebook`, `partner`, `preporuka`, `drugo`) |
| `DataCollectedAt` | `DateTime?` | `null` |
| `ConsentStatus` | `string` | `"unknown"` (`unknown | requested | granted | refused`) — **`HasDefaultValue("unknown")`** |
| `ConsentRequestedAt` | `DateTime?` | `null` |
| `ConsentAt` | `DateTime?` | `null` |
| `ConsentChannel` | `string?` | `null` (`email | instagram | facebook | telefon | osobno | claim`) |
| `ConsentScope` | `List<string>` | prazno, `text[]` (`data | photos | reviews`) — `HasDefaultValueSql("'{}'")` **samo u `isNpgsql` grani** |
| `ConsentNote` | `string?` | `null` |
| `GooglePlaceId` | `string?` | `null`, indeks (ne unique — isti place može imati dva profila, npr. sala + catering) |

**3. `VendorPhoto`:**
| Svojstvo | Tip | Default |
|---|---|---|
| `CreatedAt` | `DateTime?` | `null` za postojeće (**ne izmišljati**); nove postavlja kod |
| `UploadedByUserId` | `Guid?` | `null` |
| `Source` | `string` | `HasDefaultValue("partner")` — svi postojeći retci su partnerski uploadi (import slike su statične datoteke izvan baze) |
| `RightsConfirmedAt` | `DateTime?` | `null` |
| `ModerationStatus` | `string` | `HasDefaultValue("unreviewed")` (`unreviewed | approved | flagged`) |
| `ReviewedByUserId` | `Guid?` | `null` |
| `ReviewedAt` | `DateTime?` | `null` |
| `ModerationNote` | `string?` | `null`, `HasMaxLength(1000)` |
Indeks `(ModerationStatus)`.

**4. `ImportedReview`:**
| Svojstvo | Tip | Default |
|---|---|---|
| `ExternalKey` | `string?` | `null` za postojeće; puni ga importer (Zadatak 16) |
| `CreatedAt` | `DateTime?` | `null` |
| `UpdatedAt` | `DateTime?` | `null` |
| `VerificationStatus` | `string` | `HasDefaultValue("unverified")` (`unverified | verified | rejected`) |
| `VerifiedByUserId` | `Guid?` | `null` |
| `VerifiedAt` | `DateTime?` | `null` |
| `EvidenceNote` | `string?` | `null`, `HasMaxLength(1000)` (npr. „screenshot u Drive/Recenzije/…") |
Indeks: unique `(VendorId, ExternalKey)` s filterom `"external_key IS NOT NULL"` — **filter samo u `isNpgsql` grani**;
izvan nje običan ne-unique indeks ili nikakav.

**5. `UserReview`:** `DecidedBy` (`Guid?`), `RejectReason` (`string?`, `HasMaxLength(500)`).

**6. Minimalno postavljanje pri stvaranju** (samo ovo, bez ostale logike):
- `PhotosController.Upload`: `CreatedAt = DateTime.UtcNow`, `UploadedByUserId = <uid>`, `Source = "partner"`
  (eksplicitno, ne oslanjati se na DB default).
- `ExcelImporter` pri dodavanju `ImportedReview`: `CreatedAt = UtcNow`.

**7. Napomene za generiranje migracije (vlasnik):**
```bash
cd backend/Wediplan.Api && dotnet ef migrations add AuditIModeracija
```
Prije `database update` **pregledati generiranu datoteku**: `AddColumn` za `consent_status`,
`moderation_status`, `verification_status`, `source` mora imati `defaultValue` iz tablica gore (inače postojeći
retci dobiju `""`). Svi `*_at` stupci za staru povijest moraju biti `nullable: true`. Zatim runbook iz Zadatka 12
(backup → restore-test → update).

**Ne raditi:** ne mijenjati javne DTO-ove (`VendorDto`, `VendorProfileDto`) — novi podaci su interni.
Ne dodavati logiku filtriranja/moderacije (to su 15–16).

**Kriterij gotovo:** `dotnet build` + `dotnet test` zeleno (postojeći testovi, bez novih); migracija generirana i
pregledana; `database update` uspješan na lokalnoj bazi s uvezenim podacima; `select consent_status, count(*) from
vendors group by 1` vraća samo `unknown`. `PLAN-ARHITEKTURA.md` §3 (model podataka) ažuriran. `STANJE.md` ažuriran.

---

## Zadatak 14 — Audit log (interceptor) + admin pregled povijesti

**Cilj:** svaka promjena partnerskih/javnih podataka automatski se bilježi u `audit_log` (tko, kad, koje polje,
staro→novo), bez mijenjanja postojećih kontrolera. Plus eksplicitni zapisi za pisanja koja zaobilaze ChangeTracker.

**Datoteke:**
- `Infrastructure/Audit/IAuditContext.cs`, `Infrastructure/Audit/AuditContext.cs` (novo)
- `Infrastructure/Audit/AuditSaveChangesInterceptor.cs` (novo)
- `Infrastructure/Audit/AuditRules.cs` (novo — koje entitete/polja pratiti)
- `Program.cs` (registracija, `--audit-prune` CLI)
- `Import/ExcelImporter.cs` / `Program.cs RunImportAsync` (akter = import)
- `Controllers/AccountController.cs`, `Controllers/OptOutController.cs` (eksplicitni zapisi)
- `Controllers/AdminController.cs` (`GET /api/admin/audit`)
- `Contracts/ProviderContracts.cs` (`AdminAuditEntryDto`)
- `lib/api/provider.ts`, `lib/types.ts`, `components/AdminPanel.tsx` (prikaz povijesti)
- `backend/Wediplan.Api.Tests/AuditInterceptorTests.cs` (novo)

**Koraci:**
1. **`IAuditContext`** (scoped): `ActorType`, `ActorUserId`, `Source`, `Set(actorType, userId, source)`.
   `AuditContext` se lijeno inicijalizira iz `IHttpContextAccessor` ako `Set` nije pozvan:
   - nema `HttpContext` → `system`
   - neautentificiran → `public`
   - korisnik u roli `Roles.Admin` → `admin`
   - korisnik u roli `Roles.Provider` → `partner`
   - ostali autentificirani → `user`
   - `Source = $"api:{method} {path}"`.
   Registrirati `AddHttpContextAccessor()` ako već nije.
2. **`AuditRules`**:
   - Praćeni entiteti i `EntityType`: `Vendor` → `vendor`, `VendorPhoto` → `vendor_photo`,
     `ImportedReview` → `imported_review`, `UserReview` → `user_review`, `Claim` → `claim`.
   - **Ne prate se:** `VendorDraft`, `Favorite`, `BudgetPlan`, `Event`, `DailyStat`, tokeni, Identity tablice,
     sam `AuditLog`.
   - Ignorirana svojstva: `UpdatedAt`, `Search`, navigacije.
   - **Maskirana svojstva** (bilježi se samo `{"changed":true}`): `Vendor.Phone`, `Vendor.Email`,
     `Vendor.SocialInstagram`, `Vendor.SocialFacebook`, `Vendor.ConsentNote`, `UserReview.Text`,
     `ImportedReview.Author`, `ImportedReview.Text`. (Tekstovi recenzija su sadržaj pojedinaca; povijest njihovog
     teksta ne treba nam za GDPR svrhu.)
3. **`AuditSaveChangesInterceptor : SaveChangesInterceptor`** (scoped, prima `IAuditContext`):
   - U `SavingChanges`/`SavingChangesAsync`: proći `eventData.Context.ChangeTracker.Entries()` za praćene tipove u
     stanju `Added | Modified | Deleted`; za `Modified` samo svojstva s `IsModified && !Equals(Original, Current)`.
   - Za svaki entry dodati `AuditLog` u isti context (isti `SaveChanges` → ista transakcija). `EntityId` =
     primarni ključ kao string. `Changes` serijalizirati `System.Text.Json` (liste kao JSON nizovi, `DateTime` ISO).
     `Added` → `new` vrijednosti (maskirana polja `{"set":true}` ako nisu null), `Deleted` → bez `Changes` (ili
     samo ključna polja za prepoznavanje, npr. `StorageKey` za sliku).
   - Ne ulaziti u rekurziju (AuditLog entries preskočiti). Ako nema praćenih promjena → ništa.
   - Registracija u `Program.cs`:
     `builder.Services.AddScoped<IAuditContext, AuditContext>(); builder.Services.AddScoped<AuditSaveChangesInterceptor>();`
     i `AddDbContext<AppDbContext>((sp, o) => o.UseNpgsql(...).AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()))`
     — zadržati postojeći `UseQuerySplittingBehavior`.
4. **Import:** u `RunImportAsync` nakon `CreateScope()`:
   `scope.ServiceProvider.GetRequiredService<IAuditContext>().Set("import", null, $"import:{Path.GetFileName(path)}");`
   — `db` mora doći iz istog scopea (već dolazi). Import je velik: provjeriti da audit ne udvostruči vrijeme
   neprihvatljivo (očekivano: jedan audit redak po promijenjenom retku; `dry-run` ne piše ništa).
5. **Eksplicitni zapisi** (ChangeTracker ih ne vidi):
   - `AccountController.Delete`: prije `ExecuteUpdateAsync` dohvatiti ID-jeve vendora s `OwnerUserId == uid`; nakon
     njega dodati po jedan `AuditLog { EntityType="vendor", Action="owner_unlinked", ActorType="user", ActorUserId=uid }`
     i jedan `{ EntityType="user", EntityId=uid, Action="account_deleted" }`; `SaveChanges` unutar postojeće
     transakcije, prije `CommitAsync`. **Bez e-maila.**
   - `OptOutController.Submit`: interceptor će već zabilježiti `OptOut false→true` kao `update` s akterom `public`.
     Dodatno postaviti `Action` razumljivije: najjednostavnije je da `AuditRules` prepozna promjenu samo
     `Vendor.OptOut` i tada koristi `Action = "optout"` / `"optout_restored"`. **Razlog i kontakt se ne bilježe.**
   - `AdminController.RestoreOptOut`: pokriveno istim pravilom (`optout_restored`, akter `admin`).
6. **Admin pregled:** `GET /api/admin/audit?entityType=vendor&slug={slug}&limit=100` (za vendor rješava slug → Id i
   uključuje i zapise za njegove slike/recenzije: `vendor_photo`/`imported_review`/`user_review` čiji entitet ima
   taj `VendorId` — dovoljno je dohvatiti ID-jeve tih entiteta pa filtrirati `EntityId IN (...)`; obrisani entiteti
   se neće naći tim putem, zato **u `Changes`/`Note` za `vendor_photo` i recenzije uvijek dodati `vendorId`**).
   Vraća `AdminAuditEntryDto(long Id, DateTime OccurredAt, string ActorType, string? ActorEmail, string EntityType,
   string EntityId, string Action, string? Changes, string? Source)`. `ActorEmail` razriješiti iz `Users` (null ako
   je korisnik obrisan). Poredak: najnovije prvo.
7. **Frontend:** u `AdminPanel.tsx` sekcija „Povijest promjena": input za slug + gumb, tablica (vrijeme, tko,
   entitet, akcija, promjene kao sažeti `polje: staro → novo`). `adminApi.audit(slug)`, tip `AdminAuditEntry`.
8. **Rok čuvanja:** CLI `dotnet run -- --audit-prune [--months 24]` (isti obrazac kao `--rollup`) briše
   `OccurredAt < now - N mjeseci` preko `ExecuteDeleteAsync`. Cron primjer (mjesečno) u `DEPLOY.md`.
9. **Testovi** (`AuditInterceptorTests`, InMemory, interceptor registriran ručno uz `AuditContext` s ručnim `Set`):
   - `VendorUpdate_WritesOneRow_WithOldAndNewValue` (npr. `PriceFrom`)
   - `ContactFieldChange_IsMasked` (`Phone` → `{"changed":true}`, bez starog/novog broja u JSON-u)
   - `UntrackedEntity_WritesNothing` (`Favorite`)
   - `UpdatedAtOnlyChange_WritesNothing`
   - `OptOutChange_UsesOptoutAction`
   Nakon izmjene `Program.cs` pokrenuti **cijeli** test suite — `HealthEndpointTests`/`RateLimitingTests` mijenjaju
   registraciju DbContexta; ako padnu zbog interceptora, u njihovom `ConfigureServices` dodati
   `AddInterceptors(...)` ili provjeriti da scoped servisi postoje u app containeru (postoje, iz `Program.cs`).

**Kriterij gotovo:** build/test zeleno (vlasnik); ručno: partner objavi draft → u `/admin` povijest pokazuje
`PriceFrom: 800 → 950` s akterom `partner`; promjena telefona importom pokazuje samo „promijenjeno"; brisanje
računa ostavlja `account_deleted` bez e-maila. `API.md` (nova ruta), `DEPLOY.md` (prune cron),
`PLAN-ARHITEKTURA.md` (sekcija o auditu), `STANJE.md` ažurirani.

---

## Zadatak 15 — Moderacija slika (post-moderacija, evidencija admina)

**Cilj:** partnerove slike odmah idu javno, a admin vodi evidenciju koje je pregledao i odobrio ili sakrio.
Partner mora potvrditi prava na fotografiju. Preduvjet: 13 (stupci), preporučeno 14 (audit akcija admina).

**Datoteke:** `Controllers/PhotosController.cs`, `Controllers/AdminController.cs`, `Data/VendorMapper.cs`,
`Controllers/PinsController.cs`, `Data/ProviderMapper.cs`, `Contracts/ProviderContracts.cs`, `lib/api/provider.ts`,
`lib/types.ts`, `components/ProviderDashboard.tsx`, `components/AdminPanel.tsx`, `app/globals.css`,
`backend/Wediplan.Api.Tests/PhotoModerationTests.cs` (novo).

**Koraci:**
1. **Upload:** `Upload(string slug, IFormFile? file, [FromForm] bool rightsConfirmed = false)`. Ako `!rightsConfirmed`
   → `400 { error = "rights_not_confirmed" }` (provjera **prije** obrade slike). Postaviti
   `RightsConfirmedAt = UtcNow`, `ModerationStatus = "unreviewed"` (+ polja iz Zadatka 13).
2. **Javni prikaz bez `flagged`:** u `VendorMapper` (lista `Photos`) i `PinsController` (cover) filtrirati
   `p.ModerationStatus != "flagged"`. Provjeriti sva tri `Include(v => v.Photos)` u `VendorsController` — filtriranje
   mora biti u mapperu ili filtered include (`Include(v => v.Photos.Where(...))`), da ne ostane nijedan put koji
   vraća sakrivenu sliku. Ako je sakrivena slika bila cover, javno se prikazuje sljedeća po `SortOrder` (vlasnik
   vidi upozorenje).
3. **Partner vidi status:** `ProviderPhotoDto` + `string ModerationStatus, string? ModerationNote`
   (dodati na kraj recorda). U `ProviderDashboard` na sakrivenoj slici oznaka „Skriveno od administratora" +
   napomena. `unreviewed`/`approved` se partneru **ne** prikazuju kao razlika (nema potrebe).
4. **Checkbox u uploadu:** u `ProviderDashboard` iznad upload kontrole: „Potvrđujem da imam pravo objaviti ove
   fotografije (autor sam ili imam dozvolu autora)." Upload gumb onemogućen dok nije označeno.
   `providerApi.uploadPhoto(slug, file)` → `fd.append("rightsConfirmed", "true")`. Poruka za
   `rights_not_confirmed` u `providerMessage` (HR).
5. **Admin API:**
   - `GET /api/admin/photos?status=unreviewed&limit=60` → `AdminPhotoDto(string Id, string VendorSlug,
     string VendorName, string Url, string ThumbUrl, bool IsCover, string Source, string ModerationStatus,
     DateTime? CreatedAt, DateTime? RightsConfirmedAt, string? UploaderEmail, DateTime? ReviewedAt,
     string? ReviewerEmail, string? ModerationNote)`. Poredak: najstarije prvo (`CreatedAt` null na kraj).
   - `POST /api/admin/photos/{id}/approve` → `approved`.
   - `POST /api/admin/photos/{id}/flag` body `{ note: string }` (obavezno, max 1000) → `flagged`.
   - `POST /api/admin/photos/{id}/unflag` → `approved`.
   - Svaka akcija postavlja `ReviewedByUserId = Uid()`, `ReviewedAt = UtcNow`. Dozvoljeni prijelazi:
     `unreviewed→approved|flagged`, `approved→flagged`, `flagged→approved`; ostalo `409 invalid_transition`.
   - Bulk: `POST /api/admin/photos/approve-batch` body `{ ids: string[] }` (max 60) — za brzo prolaženje.
6. **Admin UI:** sekcija „Fotografije za pregled (N)" — mreža thumbnaila (lazy `<img loading="lazy">`), ispod
   svake: pružatelj (link na profil), izvor, datum, gumbi „U redu" / „Sakrij…" (prompt za napomenu);
   „Odobri sve prikazane". Filter statusa (unreviewed / flagged / approved).
7. **Opcionalno (preporučeno):** obavijest partneru mailom kad se slika sakrije — `PartnerEmails.SendPhotoFlagged`,
   best-effort (isti obrazac try/catch kao `SendReviewPublished`).
8. **Testovi:** upload bez `rightsConfirmed` → 400; `flagged` slika ne ulazi u `VendorMapper` izlaz; nedozvoljeni
   prijelaz → 409; approve postavlja `ReviewedByUserId`.

**Ne raditi:** ne uvoditi pred-moderaciju (slika NE čeka admina). Statične slike iz `public/images/vendors/`
ostaju izvan ovog sustava (v. „Budući rad").

**Kriterij gotovo:** build/test zeleno (vlasnik), `tsc`/`build` čisti; ručno: partner uploada (bez checkboxa ne
može), slika je odmah javna, admin je vidi u redu za pregled, „Sakrij" je makne s profila i karte, partner vidi
oznaku. `API.md`, `STANJE.md` ažurirani.

---

## Zadatak 16 — Recenzije: evidencija odluka + verifikacija uvezenih

**Cilj:** (a) za korisničke recenzije bilježiti koji admin je odlučio i zašto je odbijeno; (b) uvezene recenzije
(„što oni kažu" — screenshotovi od pružatelja) dobivaju status provjere koji preživljava ponovni import.
Preduvjet: 13; preporučeno 10 (dira isti `UpsertAsync`).

**Datoteke:** `Controllers/AdminController.cs`, `Import/ExcelImporter.cs`, `Import/ImportRules.cs` (ili novi helper),
`Controllers/VendorsController.cs`, `Contracts/Contracts.cs`, `Contracts/ProviderContracts.cs`, `lib/types.ts`,
`lib/api/provider.ts`, `components/AdminPanel.tsx`, `components/VendorProfile.tsx`,
`backend/Wediplan.Api.Tests/ImportedReviewKeyTests.cs` (novo).

**Koraci:**
1. **Korisničke recenzije:** `ApproveReview`/`RejectReview` postavljaju `DecidedBy = Uid()`. `RejectReview`
   prima opcionalni body `{ reason?: string }` (max 500) → `RejectReason`. `AdminReviewDto` dobiva
   `DateTime? DecidedAt, string? DeciderEmail, string? RejectReason` (na kraj). U `AdminPanel` pri odbijanju
   opcionalni prompt za razlog; filter „odbijene" prikazuje razlog. Razlog se **ne** prikazuje javno ni autoru
   (za sada).
2. **Stabilni ključ uvezene recenzije:** `ImportRules.ReviewKey(slug, author, text, source, year)` =
   prvih 32 hex znaka SHA-256 od `slug|Norm(author)|Norm(text)|Norm(source)|year` (`ImportRules.Norm` već
   postoji). Promjena teksta = nova recenzija (namjerno — admin je provjeravao konkretan tekst).
3. **Upsert u `ExcelImporter.UpsertAsync`:** umjesto `RemoveRange` + dodavanja:
   - postojeće s istim `ExternalKey` → ostaju (ažurirati samo `Rating` ako se razlikuje + `UpdatedAt`); status
     provjere se **ne dira**;
   - nove → dodati s `ExternalKey`, `CreatedAt`, `VerificationStatus = "unverified"`;
   - postojeće kojih više nema u Excelu → obrisati (Excel je izvor istine; audit bilježi brisanje);
   - postojeće s `ExternalKey == null` (stari retci prije migracije) → pri prvom importu izračunati ključ iz
     njihovih polja i upariti; ako par ne postoji, obrisati kao i dosad.
   - u izvještaj: broj novih / zadržanih / obrisanih.
4. **Admin API:** `GET /api/admin/imported-reviews?status=unverified&limit=100` →
   `AdminImportedReviewDto(Id, VendorSlug, VendorName, Author, Rating, Text, Source, Year, VerificationStatus,
   DateTime? VerifiedAt, string? VerifierEmail, string? EvidenceNote)`;
   `POST /api/admin/imported-reviews/{id}/verify` body `{ evidenceNote?: string }`;
   `POST /api/admin/imported-reviews/{id}/reject` body `{ evidenceNote?: string }`. Obje postavljaju
   `VerifiedByUserId`, `VerifiedAt`.
5. **Javno:** `VendorsController` isključuje `VerificationStatus == "rejected"`; `ImportedReviewDto` dobiva
   `bool Verified` (na kraj, default `false`). `VendorProfile.tsx` uz provjerene prikazuje mali bedž
   „✓ provjereno" (koristiti postojeći stil bedževa). Tip `ImportedReview` u `lib/types.ts` + mock podaci ako ih
   frontend mock koristi (`lib/mock/*`) — `verified` opcionalan.
6. **Admin UI:** sekcija „Uvezene recenzije za provjeru" (tekst, izvor, godina, pružatelj, polje za napomenu o
   dokazu, gumbi „Provjereno" / „Odbij").
7. **Testovi:** `ReviewKey` stabilan na razlike u razmacima/velikim slovima i različit za drugačiji tekst;
   (ako izvedivo bez Excela) upsert čuva `verified` status pri ponovnom importu — logiku upsert-a izdvojiti u
   testabilnu metodu nad listama kao u Zadatku 10.

**Kriterij gotovo:** build/test zeleno (vlasnik), `tsc`/`build` čisti; ručno: verificiraj recenziju → ponovni
import istog Excela → i dalje verificirana; odbijena nestaje s profila. `API.md`, `STANJE.md` ažurirani.

---

## Zadatak 17 — Porijeklo podataka i privola pružatelja (Excel + import + admin)

**Cilj:** za svakog pružatelja evidentirati odakle su podaci i je li (i za što) dao privolu, jer mnogi su obrtnici
(fizičke osobe). Podaci dolaze iz Excela (vlasnik kontaktira pružatelje ručno), a odobren claim automatski znači
privolu. Preduvjet: 13; preporučeno 10.

**Datoteke:** `scripts/make-template.py`, `data/vendors-template.xlsx` (regenerirati), `Import/ExcelImporter.cs`,
`Services/ClaimApprovalService.cs`, `Controllers/AdminController.cs`, `Contracts/ProviderContracts.cs`,
`lib/types.ts`, `lib/api/provider.ts`, `components/AdminPanel.tsx`, `scripts/import-vendors.mjs` (samo provjera).

**Koraci:**
1. **Predložak:** u `make-template.py` na kraj lista „Pružatelji" dodati stupce (s opisima u listu „Upute" i
   padajućim listama gdje ima smisla):
   `izvor_podataka` (google_maps | web | instagram | facebook | partner | preporuka | drugo),
   `datum_prikupljanja` (datum), `privola_status` (nepoznato | zatraženo | dano | odbijeno),
   `privola_zatrazena` (datum), `privola_datum` (datum), `privola_kanal` (email | instagram | facebook | telefon |
   osobno), `privola_opseg` (zarezom: podaci, slike, recenzije), `privola_napomena`, `google_place_id`.
   U „Upute" dodati kratki odlomak: gdje naći Place ID (Googleov Place ID Finder) i da se **ne kopiraju** Google
   ocjene/recenzije/fotografije.
   Regenerirati `data/vendors-template.xlsx`. Postojeći stupci i njihov redoslijed se **ne mijenjaju**.
2. **Import:** parsirati nove stupce (prazno = ne mijenjaj postojeću vrijednost u bazi — ne brisati ručno unesene
   podatke praznim ćelijama). Mapiranje HR → interne vrijednosti (`dano`→`granted`, `odbijeno`→`refused`,
   `zatraženo`→`requested`, `nepoznato`→`unknown`). Nepoznata vrijednost → `Warn` + ignorirati.
   Pravila:
   - `refused` → `OptOut = true` + `Warn("privola odbijena — profil skriven (opt-out)")`.
   - Importer **nikad** ne postavlja `OptOut = false`.
   - Ako je profil `claimed` i u bazi `ConsentStatus == "granted"` s kanalom `claim`, Excel ga ne smije vratiti
     na slabiji status (`unknown`/`requested`) — samo `refused` ima prednost.
   - `google_place_id` → `GooglePlaceId` (samo trim; format se ne validira strogo, ali `Warn` ako sadrži razmake).
3. **Claim = privola:** u `ClaimApprovalService.ApproveAsync` nakon odobrenja: ako `ConsentStatus` nije
   `refused` → `ConsentStatus = "granted"`, `ConsentChannel = "claim"`, `ConsentAt = UtcNow`,
   `ConsentScope = ["data","photos","reviews"]`. (Pretpostavka: partner pri claimu prihvaća uvjete za partnere —
   vlasnik to mora osigurati u tekstu uvjeta; zabilježiti u `STANJE.md` kao OPS korak.)
4. **Admin:** `GET /api/admin/vendors/{slug}/provenance` → `AdminProvenanceDto(DataSource, DataCollectedAt,
   ConsentStatus, ConsentRequestedAt, ConsentAt, ConsentChannel, ConsentScope, ConsentNote, GooglePlaceId)` i
   `PUT` istog oblika za ručnu korekciju (audit ga bilježi). Frontend: u `AdminPanel` uz „Povijest promjena"
   (isti slug input) prikaz i uređivanje ovih polja. Plus pregled `GET /api/admin/consent-summary` → brojevi po
   `ConsentStatus` (za praćenje kampanje kontaktiranja).
5. **Provjera:** `scripts/import-vendors.mjs` (frontend mock) mora ignorirati nove stupce — pokrenuti ga na novom
   predlošku i potvrditi da nema greške. **Nijedno novo polje ne smije ući u javni `VendorDto` ni u
   `data/vendors.json`.**
6. **Testovi:** mapiranje HR vrijednosti; `refused` postavlja `OptOut`; prazna ćelija ne briše postojeću
   vrijednost; claim odobrenje postavlja `granted` (proširiti `ClaimVerificationTests` ili novi test uz isti
   `Build()` obrazac).

**Kriterij gotovo:** build/test zeleno (vlasnik); predložak ima nove stupce i upute; import s `privola_status =
odbijeno` skriva profil; admin vidi i uređuje porijeklo/privolu. `API.md`, `STANJE.md` ažurirani; u `README.md`
sekcija „Unos stvarnih podataka" spominje nove stupce.

---

## Budući rad (NE implementirati u ovom planu)

### Google Places API — ocjena i broj recenzija
Provjereno 2026-09-30 u Googleovoj dokumentaciji (Places API policies, Maps Platform Service Specific Terms):
**`place_id` se smije spremati trajno**; ostali sadržaj Places API-ja (uključujući rating i broj recenzija) se
**ne smije pre-fetchati, cacheirati ni spremati** osim uz izričite iznimke (npr. lat/lng do 30 dana). Prikaz
podataka bez Google karte zahtijeva Google logo/atribuciju. Za korisnike s EEA adresom naplate vrijede posebni
EEA uvjeti — **vlasnik mora pregledati aktualne EEA uvjete prije implementacije**.

Posljedice za dizajn (već ugrađene u ovaj plan):
- Zadatak 13 dodaje `Vendor.GooglePlaceId`; Zadatak 17 ga puni iz Excela.
- Rating/count se **nikad ne spremaju** u `Vendor.Rating/ReviewCount` ni drugdje u bazu. Kad dođe na red:
  server-side dohvat pri renderu profila (Place Details, samo polja `rating`, `userRatingCount` — najjeftiniji
  field mask), kratkotrajni in-memory cache u skladu s uvjetima, zaseban UI blok „Google ocjena" s atribucijom,
  **odvojeno** od naših ocjena.
- **Nikad u JSON-LD** (Zadatak 18 to već osigurava za vlastite vs. vanjske ocjene).
- Periodična provjera valjanosti `place_id` (Google ih povremeno mijenja → `NOT_FOUND`) — besplatni/jeftini
  „ID refresh" poziv; tada se može dodati `GooglePlaceIdCheckedAt`.

**Otvoreno pitanje za vlasnika (nije blokirajuće):** trenutni Excel stupci `ocjena` / `broj_recenzija` /
`izvor_ocjene` — ako su ručno prepisani s Google Mapsa, to je isti sadržaj kojeg Googleovi uvjeti štite. Kad se
uvede Places API, preporuka je te stupce za Google izvor prestati puniti i prikazivati samo živi Google podatak.

### Ostalo
- Statične import slike (`public/images/vendors/<slug>/`, `scripts/sync-images.mjs`) prebaciti u `VendorPhoto`
  retke (`Source = "import"`) da ulaze u moderaciju i audit.
- Screenshotovi recenzija kao dokaz u aplikaciji (upload uz uvezenu recenziju) — za sada `EvidenceNote` tekst.
- OPS: politika privatnosti mora opisati `audit_log` (svrha, rok 24 mj.), backupe (rok) i izvore podataka o
  pružateljima (GDPR čl. 14).
