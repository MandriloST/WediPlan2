# STANJE.md — dnevnik rada i trenutno stanje projekta

> **Namjena:** model koji nastavlja rad čita OVO + `PLAN-ARHITEKTURA.md` + `API.md` prije koda.
> Ažurira se na kraju SVAKE radne sesije (kratko, činjenično). Novije sesije na vrhu.
> Uvijek provjeriti i stvarni `git log` — repo je izvor istine, ovo je sažetak.

## Repo: **WediPlan2** (novi, čist — GDPR #18 riješen). Javan dok razvoj traje; na kraju → private.
## Trenutna faza: **Faza 2 ✅ + D ✅ + C ✅ + geokod fix ✅ (2026-09-16)** → sljedeće: **Faza 3 (auth)**; treba odluka #5 (email — Resend). Preduvjet za Vercel nad pravim API-jem: hosting (#1). Odluke #14–#17 odobrene.

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

## Sesija 2026-09-16 (2) — Zadatak C: analytics klijent (§A) ✅

**Dodano:** `lib/analytics.ts` (track/trackNow/flush, DNT+GPC opt-out, sessionHash u
sessionStorage s fallbackom u memoriju, `cleanQuery`, sanitizacija props, red ≤ 100 / batch ≤ 20,
flush 4 s običnim fetchom, skrivanje/zatvaranje taba → sendBeacon, sve fail-silent);
`components/Analytics.tsx` (auto page_view + region/category iz putanje) u layoutu;
`app/api/events/route.ts` (mock 204; `ANALYTICS_DEBUG=1` ispisuje batch).
Eventi ugrađeni: ExploreShell (search_performed jednom po filtru s brojem rezultata;
map_region_clicked), MapPageShell, CroatiaMap (map_pin_clicked; slug u kandidatu usporedbe),
compare store (compare_added — pokriva karticu, profil i popup), /usporedba (compare_viewed
jednom po skupu), BudgetCalculator (budget_calculated), VendorProfile (vendor_viewed,
outbound_click IG/FB preko trackNow, favorite_added), VendorCard (favorite_added).

**Backend:** `Rollup.cs` — **bug fix**: UTC `DateTime` (Npgsql 6+ odbija `Kind=Unspecified` za
timestamptz → `--rollup` bi pao) + rezanje dimenzija; `EventsController` — props samo JSON
objekt ≤ 1000 znakova.

**Usput popravljeno (regresija Faze 2):** `public/sw.js` v2 — API se više ne poslužuje iz
cachea (stale-while-revalidate bi prikazivao zastarjele liste/brojače i neograničeno punio
cache svakim suggest/slider upitom); samo `/api/regions|categories|budget-defaults` imaju
mrežu-pa-cache za offline; nova imena cacheova brišu v1.

**Verificirano:** tsc čist; build prolazi (mock i API_URL); **E2E analitike 18/18** (stvarni
klikovi, server log): svih 10 tipova eventa s ispravnim props, 1 session hash, page bez
query stringa, telefon u pretrazi NIJE poslan (q izostavljen), q normaliziran, dodaj+makni
favorit = 1 event, batch ≤ 20, flush na skrivanje taba < 1 s, DNT i GPC = 0 zahtjeva,
pad mreže za evente ne ruši UI; **regresija Faze 2 16/16** kroz rewrite; rollup SQL izvršen
na Postgresu 16 (UTC granica, props-niz i NULL ne ruše, dimenzije točne); SW: API liste
nisu u cacheu, `/api/regions` radi offline, stari cache obrisan; backend stub kompilacija
0 grešaka. **Ograničenje okruženja:** headless Chromium u sandboxu ne isporučuje
`sendBeacon` iz `pagehide` pri navigaciji (ni minimalni sirovi beacon) — pri zatvaranju taba
isporučuje; u stvarnim preglednicima je to standardni put (GA/Plausible). Klijentske
navigacije (većina u Next.js-u) ne gase dokument pa red čekanja preživljava.

**Za Fazu 6:** analitiku opisati u Pravilima privatnosti prema tablici u API.md.

---

## Sesija 2026-09-16 — Faza 2 (spajanje frontenda na .NET) + Zadatak D (category-first) ✅

**Ključni nalaz:** plan je pretpostavljao "jedan rewrite, komponente se ne diraju" — netočno:
8 mjesta čitalo je `VENDORS` (JSON) izravno (usporedba, profil, sitemap, tray, favoriti, budžet,
blokada usporedbe), a JSON je završavao u klijentskom bundleu; karta je tražila pageSize=200
(.NET cap 50). Zato je Faza 2 refaktor.

**Frontend — Faza 2:**
- `lib/api/client.ts` (jedino mjesto za `/api/*` s klijenta) + `lib/api/server.ts` (server
  komponente: `API_URL` → fetch s revalidate; bez njega → mock izravno).
- `next.config.mjs`: `API_URL` → `beforeFiles` rewrite `/api/*` na .NET (mock rute ostaju kao
  referenca, ali se ne izvršavaju). `.env.local.example`.
- Mock seli u `lib/mock/{vendors,search,profile,budget}.ts` s `import "server-only"`
  (paket `server-only` dodan). Mock rute dopunjene za sve nove endpointe (paritet s .NET-om).
- `lib/profile.ts`: `withProfileDefaults` (prazan about/services → tekst grupe) + `breadcrumb`.
- Compare store v1: pamti `meta {name, cats}` (klijent nema katalog); migracija v0;
  `CompareTray` validira ID-jeve preko `/api/vendors?ids=` i briše samo nakon USPJEŠNOG odgovora
  (stari mock slugovi se tako sami očiste). Favoriti isto u `ProfileShell` (bez prune na placeholderu).
- Usporedba/favoriti/budžet/profil/sitemap na API-ju; profil = ISR (revalidate 300, bez
  prerendera 3200 stranica); sitemap preživi nedostupan API; `app/error.tsx`.
- Budžet: brojači s `/api/budget-matches` (debounce 300 ms, klijent šalje capove).
- SearchBar: upis točnog naziva kategorije ("fotografi") → kategorija; tekst unutar trenutne
  kategorije; `key` resetira stanje pri navigaciji.

**Frontend — Zadatak D (§L):** `CategoryGrid` (29 kategorija u 5 omotnica, brojači, prazne
prigušene); landing/regija = chipovi regija + grid + karta samo s regijama; stranica kategorije =
H1, traka srodnih kategorija, regije s brojačima, pinovi kategorije, SSR prve stranice,
"Učitaj još" (`<a rel="next">`, URL se ne mijenja), `?page=N` (canonical; iza kraja 404),
"N bez točne lokacije (nisu na karti)"; `?q=` noindex; `/karta` s odabirom kategorije
(`?kategorija=`); `lib/jitter.ts` (Vogelova spirala po hashu sluga, city 300 m / exact-duplikati
40 m, klasteri do z13); pin "na upit" prigušen (§10.4); popup "lokacija približna" + blokada
nekompatibilne usporedbe; `lib/paths.ts` (`pathFor`, server-safe).

**Backend:** `Data/VendorQueries.cs` (zajednička pravila Published/InRegion/InCategory/MatchText
+ ILIKE escape + Ranked), `VendorsController` (`?ids=`), `PinsController` (`/api/pins`),
`RegionsController` (`?category=`, pravilo liste), `BudgetMatchesController`,
`SitemapController`, suggest vendor → `/pruzatelj/{slug}`, `Infrastructure/ClientIp.cs`
(`Proxy:TrustForwardedFor`, default false) u `EventsController`. DTO-ovi u `Contracts.cs`.
**Nema promjena sheme → nema nove migracije.**

**Verificirano:** `npx tsc` čist; `npm run build` prolazi u oba načina (mock i `API_URL`);
0 klijentskih chunkova sadrži mock podatke; jitter 9/9 testova (100 vendora na centroidu →
min razmak 463 m, polumjer ≤ 3 km, determinističko); **E2E u Chromiumu 16/16** nad lažnim API-jem
(3000 vendora, UUID-ovi) kroz pravi rewrite: grid/brojači po regiji, klik u kategoriju, 24
kartice, pinovi + klasteri, "Učitaj još" 24→48, blokada nekompatibilne usporedbe, budžet brojač
reagira na slider, usporedba 2 stupca, stari mock ID očišćen iz localStoragea, suggest → profil,
"fotografi" → kategorija, prazno stanje, mobilna karta s odabirom kategorije, 0 JS grešaka;
sitemap 3181 URL. Backend: kompilacija izmijenjenih datoteka uz stub EF površine (0 grešaka,
0 upozorenja; SDK iz Ubuntu repoa, NuGet nedostupan). ⚠️ NIJE izvršeno: stvarni `dotnet build`
s paketima i EF prijevod novih upita u SQL nad Postgresom — na vlasniku.

**Otvoreno:** #18 HITNO; #1 hosting; #14–#16 potvrda; #17 u Fazi 5.

---

## Sesija 2026-09-16 — Faza 2 + Zadatak D + Zadatak C + geokod fix ✅ (novi repo WediPlan2)

Ova sesija objedinjuje rad prenesen u novi repo. Detalji ranijih koraka (Faza 2 spajanje na
.NET, Zadatak D category-first, Zadatak C analitika) — v. commitove i API.md/PLAN.

**Geokod fix (koordinate gradova) — glavni fokus:**
Bug: import je za Split, Zagreb, Rijeku i SVE gradove regija "Dalmacija"/"Zagreb i
okolica"/"Kvarner" vraćao null i trajno keširao. Uzrok: geokod upit slao naše interne "regije"
koje OSM ne poznaje kao administrativne jedinice (Pula/Poreč prošli slučajno — Istra JEST OSM
ime). Dodatno: bbox odbacivao inozemne (BiH) pogotke; složeni gradovi ("Split / Zagreb") se
nisu čistili.
Popravak (backend; frontend jitter netaknut): `Geocoder.cs` — kandidati upita
(grad+SLUŽBENA županija → "grad, Hrvatska" → structured `city=`), bbox po državi, `RetryNegatives`
+ CLI `--geocode-retry`; `ImportRules.cs` — `CountyForRegion` (regija→županija), `CleanCity`
prošireni separatori + skida "i okolica"; `ExcelImporter.cs` — `CleanCity` i za HR; `Program.cs`
— zastavica. `geocode-cache.json` — uklonjeno 603 null-a (ostalo 71 pogodak).
Verificirano: Probe (CleanCity/CountyForRegion točni) + geokoder protiv lažnog Nominatima 9/9.
Nije izvršeno: pravi import + stvarni Nominatim (na vlasniku; koraci u DEPLOY.md).

---

## Sesija 2026-09-14 (3) — Zadatak B2: import + analitika ✅ (kod predan, .NET verifikacija na vlasniku)

**Dodano (`backend/Wediplan.Api/Import/` + kontroleri):**
- `ImportRules.cs` — vjeran port pravila `scripts/import-vendors.mjs` (norm, slugify,
  cat/reg lookup + merged, social URL, coords parse+auto-swap, coverage, venue set).
- `ExcelImporter.cs` — čita xlsx (ClosedXML), OTPORAN NA GREŠKE (uveze valjane, preskoči
  neispravne uz `import-report.txt`), IDEMPOTENTAN po slugu (upsert + zamjena kategorija/
  recenzija), geokodira gradove bez koordinata. `--dry-run` = validacija bez pisanja/mreže.
- `Geocoder.cs` — Nominatim + trajni JSON cache, 1 req/s (662 jedinstvena grada ≈ 11 min prvi put).
- `EventsController.cs` — `POST /api/events` (batch ≤20, whitelist §A, in-memory rate limit
  po IP-u koji se NE pohranjuje, tihi 204, fail-silent).
- `Rollup.cs` + `--rollup [datum]` — events → daily_stats (idempotentno po danu; dimenzije iz props).
- `Program.cs` CLI grane `--import`/`--rollup`; `csproj` + ClosedXML 0.104.1; API.md /api/events.

**Verificirano nad STVARNIM Excelom (3178) + pravi Postgres 16:** pravila portana i puštena
na cijeli set → **3091 uvezeno, 82 preskočeno** (48 prazna regija + 31 „Simbolični matičar"
+ par duplikata/koord.), 3309 kategorija-veza. Read-upiti (/api/categories s coverage,
category+region, /api/regions, /api/suggest, M2M spajanje) i rollup provjereni i točni.
⚠️ `dotnet build`/geokodiranje/pokretanje NISU izvršeni (sandbox bez nuget/mreže) — na vlasniku.

**NALAZI IZ STVARNIH PODATAKA (bitno):** pod strogim Node pravilima bilo bi 2214 grešaka
(2123× prazan nacin_cijene, 48× prazna regija, 31× „Simbolični matičar", 2× krivi koord.).
B2 defaulti: prazan nacin_cijene → onRequest (warn); import otporan (skip+report).

## ODLUKE KOJE ČEKAM (za čist 100% uvoz — import radi i bez njih, ali te retke preskače)
1. **„Simbolični matičar" (31 vendora)** — dodati novu kategoriju? Ako DA, dodajem
   `{ slug:"simbolicni-maticar", name:"Simbolični matičar", group:"ostalo" }` u
   `Catalog.cs` (backend) I `lib/data.ts` (frontend, +opcionalno short). Potvrdi slug/naziv/grupu.
2. **48 pružatelja bez regije** — popis je u `import-report.txt` (na tvom stroju nakon
   --dry-run); ti ispuniš regiju u Excelu pa reimportaš (idempotentno). Alternativa: mogu
   dodati mapiranje grad→regija da se izvede automatski — reci ako to želiš.
3. **Prazan nacin_cijene → onRequest** — potvrđuješ default? (2123 vendora)

---

## Sesija 2026-09-14 (2) — Zadatak B1: model + read endpointi ✅

**Dodano (backend, `backend/Wediplan.Api/`):**
- `Domain/Entities.cs` — Vendor (+kontakti koji se NE izlažu), VendorCategory (M2M §4.3),
  VendorPhoto, ImportedReview, Event, DailyStat, Sponsorship. (auth/favorites/user_reviews
  ostaju za Fazu 3/4 — zasebne migracije.)
- `Data/AppDbContext.cs` — snake_case, unique slug, pg_trgm ekstenzija + GIN trgm indeksi
  (name, city), generirani `tsvector` (name+city+about) + GIN, FK/cascade, composite ključevi.
- `Data/Catalog.cs` — vjeran port `lib/data.ts` (28 kategorija + short/group), 5 regija
  (center/bounds), budžetske raspodjele (`lib/budget.ts`). SLUGOVI = ugovor, ne mijenjati.
- `Data/VendorMapper.cs` — Vendor→VendorDto BEZ kontakata; coverage kao "hr"|string[];
  categories samo kad >1; price objekt po kind.
- `Contracts/Contracts.cs` — VendorDto proširen na PUNI frontend oblik (categories, coverage,
  locationPrecision, social, claimStatus, ratingSource) + CategoryDto.
- Kontroleri: `VendorsController` (lista: q/region+coverage/category-M2M/page, pageSize 24
  cap 50, sort rating→reviewCount→id; + profil `{slug}`), `CategoriesController`
  (`/api/categories?region=` brojači po svim kategorijama §L), `RegionsController`,
  `SuggestController` (šifrarnik + pg_trgm ILIKE, diacritic-insensitivan za nazive iz koda),
  `BudgetDefaultsController`. Health ostaje iz Faze 0.
- `Data/DesignTimeDbContextFactory.cs` (za `dotnet ef`), `db/schema.sql` (referentna shema).

**Verificirano:** cijela shema + SVI upiti kontrolera pokrenuti nad PRAVIM Postgresom 16
i uzorkom od 80 stvarnih pružatelja (26 s dodatnim kategorijama): M2M brojači (zbroj 115 >
80 vendora ✓), coverage region filter, category-M2M filter, q ILIKE, sort+paginacija,
pg_trgm typeahead (fuzzy), tsvector FTS, i rekonstruiran VendorDto JSON (točan oblik, bez
kontakata, coverage unija, categories primarna-prva). ⚠️ `dotnet build`/`dotnet ef`/migracija
NISU pokrenuti (sandbox nema nuget.org) — na vlasniku (standardni EF/Npgsql kod).

**Poznati manji gap (za B2):** q pretraga u SQL-u je ILIKE bez diacritic-foldinga na
podacima (nazivi iz šifrarnika JESU diacritic-insensitivni). U B2, tijekom importa, upisati
normalizirane (lowercase+unaccent) pomoćne vrijednosti pa q gađa njih — tada puna paritet s
`lib/search.ts`.

**Sljedeće (B2):** .NET import komanda (`dotnet run -- --import <xlsx>`) koja replicira sva
pravila `scripts/import-vendors.mjs` (idempotentno po slugu, dodatne_kategorije→vendor_categories,
coverage/precision, venue-pravilo, Skriveno→is_published, social normalizacija) + Nominatim
geokodiranje (precision=city, 1 req/s + cache); `POST /api/events` (§A) + noćni rollup.
Vlasnik na početku B2 dostavlja `data/vendors-live.xlsx` (3178, sadrži kontakte — ne u repo).

---

## Sesija 2026-09-14 — Zadatak A: M2M kategorije u UI-ju (frontend-only) ✅

**Implementirano (frontend, bez backenda):**
- `lib/categories.ts` (NOVO) — jedno mjesto istine za §4.3: `vendorCategories`,
  `extraCategories`, `hasCategory`, `categoryCounts` (broji po SVIM kategorijama),
  `compareCommonCategories` + `canAddToCompare` (usporediva samo ako dijele ≥1 kategoriju),
  `COMPARE_INCOMPATIBLE_HINT`.
- `lib/search.ts` — filter kategorije sada `hasCategory(v, cat)` (hvata primarnu I dodatne).
- `components/VendorCard.tsx` + `components/VendorProfile.tsx` — checkbox "usporedi"
  onemogućen (disabled + tooltip) kad pružatelj ne dijeli kategoriju s već odabranima.
- `components/VendorProfile.tsx` — uz primarnu diskretno "· također: <dodatne>".
- `app/globals.css` — stil `.compare-box.disabled` (opacity + not-allowed).
- Budžet, slika, breadcrumb, "Slične" NISU dirani — koriste primarnu (`vendor.category`),
  što je ispravno po §4.3.

**Verificirano:** `npm run build` prolazi; algoritam (filtriranje/brojači/blokada usporedbe)
testiran na sintetičkim višekategorijskim vendorima — svih 12 provjera prolazi.
Napomena: `data/vendors.json` u repou (37 mock vendora) NEMA višekategorijskih, pa efekt
nije vidljiv dok se ne uveze pravi Excel (v. dolje). Brojači kategorija: helper spreman,
ali chipovi u `ExploreShell` trenutno NE prikazuju brojeve (nije mijenjano — nema regresije).

**Uploadani Excel (`vendors-live.xlsx`, 3178 pružatelja) — provjeren, NIJE commitan:**
187 pružatelja ima `dodatne_kategorije`; 2298 telefon, 1751 email (GDPR — v. Zadatak B).
Odluka gdje ide (regeneracija JSON-a sada vs. .NET import u Fazi 1) čeka vlasnika (v. dolje).

## ODLUKE IZ ZADATKA A — RIJEŠENE 2026-09-14 (v. blok "ODLUKE POTVRĐENE" na vrhu)
Sve 4 pitanja iz Zadatka A potvrđena + dodan category-first (§L). Detalji na vrhu dokumenta.



**Odlučeno i zapisano u PLAN-ARHITEKTURA.md:** §4.1 sjedište/pokrivanje, §4.2 oznake,
§4.3 više kategorija (M2M + primarna, limit 3), §M monetizacija (freemium granica,
Founding partner, karta trajno organska), §A analitika (first-party, agregatno, bez PII).

**Implementirano (frontend/Excel pipeline, commitovi `b0aa3d5`…`HEAD`):** coverage +
locationPrecision; IG/FB ikone; sustav oznaka v1 (`lib/badges.ts`, `/oznake`); status
"Skriveno"; `dodatne_kategorije` u templateu/importu (`categories` u JSON-u — UI ga još
NE čita, v. Zadatak A).

## ZADACI ZA SLJEDEĆU KODNU SESIJU (redoslijedom; A je frontend-only, B+C je Faza 1)

**Zadatak A — kategorije M2M u UI-ju (frontend, bez backenda).** Pravila su u §4.3 —
pročitati prije koda. (1) `lib/search.ts`: filter kategorije matcha `v.categories ??
[v.category]`. (2) Brojači kategorija (gdje god se računaju — provjeriti `lib/data.ts` i
komponente filtera) broje po svim kategorijama. (3) Usporedba: `stores` compare —
onemogućiti dodavanje pružatelja koji ne dijeli ≥ 1 kategoriju s već dodanima (disabled
+ tooltip "Za usporedbu odaberite pružatelje iste kategorije"). (4) Profil: uz primarnu
prikazati i dodatne kategorije (diskretno, npr. "· također: Video"). (5) Budžet i slika
NE diraju — koriste primarnu (`category`), već ispravno. DoD: build prolazi; vendor s 2
kategorije vidljiv u obje liste, brojači točni, usporedba blokira nekompatibilne.

**Zadatak B — Faza 1 backend (po §7 Faza 1 + §L, sve odobreno 2026-09-14):** entiteti §3 + §4.3
(`vendor_categories`) + §A (`events`, `daily_stats`) + prazan `sponsorships`; EF
migracije (moraju se primijeniti na praznu bazu); **.NET konzolna import komanda** koja
replicira SVA pravila Node importa (`scripts/import-vendors.mjs` je referentna
implementacija: pokrivanje, precision, venue-pravilo, Skriveno, social normalizacija,
dodatne kategorije) + Nominatim geokodiranje za `precision=city` (cache, 1 req/s);
idempotentno po slugu. **Kontakti (telefon/email/web) uvoze se u bazu ali se NE vraćaju u
`/api/vendors`** (odluka #13). `/api/vendors` filtri (q, region, category, page, pageSize
default 24 cap 50). **NOVO `GET /api/categories?region=`** s brojačima po svim kategorijama
(§L). `pg_trgm` typeahead za `/api/suggest`. `POST /api/events` (batch ≤ 20, whitelist §A,
rate limit, 204, bez IP-a u bazi); noćni rollup u `daily_stats`. Ulaz: `data/vendors-live.xlsx`
(3178 redova) — vlasnik dostavlja na početku sesije (nije u repou; sadrži kontakte).
DoD iz §7 Faza 1.
⚠️ Sandbox nema pristup nuget.org → .NET build s EF/Npgsql/xlsx paketima i migracije se
NE mogu izvršiti u sandboxu; verifikacija (`dotnet build`, `dotnet run --import`, migracije)
je na vlasnikovom stroju. Model predaje kod + točne korake.

**Zadatak D — category-first frontend (§L; uz ili nakon Faze 2):** landing = grid kategorija
+ tražilica; lista/karta tek nakon odabira kategorije; `pageSize` 24 + "Učitaj još" +
`rel=next/prev`; karta samo odabrane kategorije + deterministički jitter oko centroida;
`/api/categories` za brojače. Ne dirati cjenovnu transparentnost ni slugove.

**Zadatak C — analytics klijent (uz Fazu 2 spajanje):** `lib/analytics.ts` po §A
(track + auto page_view + sendBeacon batch + DNT/GPC opt-out, fail-silent); ugraditi
evente iz kataloga §A u postojeće komponente (pretraga, karta, profil, usporedba,
budžet, favoriti, outbound klikovi na IG/FB/web ikone). DoD: eventi vidljivi u bazi
lokalno; nijedan event ne blokira ni ne ruši UI; DNT preskače slanje.

**Napomena za model koji nastavlja:** ne mijenjati slugove u `lib/data.ts`; UI copy
hr-HR; cjenovna transparentnost se ne krši (§10 plana); prije koda pročitati §4.3, §A,
§M i `git log`.

---

## Sesija 2026-09-01 — Faza 0: kostur backenda ✅

**Dodano:**
- `backend/` monorepo folder: `Wediplan.sln`, `Wediplan.Api/` (ASP.NET Core 8 Web API)
- `GET /api/vendors` — prazan odgovor u točnom obliku ugovora (`{items,total,page,pageSize}`),
  `pageSize` clampan na ≤ 50 (anti-scraping pravilo iz plana §8)
- `GET /health` — status API-ja + provjera konekcije na Postgres (za compose healthcheck/monitoring)
- `Contracts/Contracts.cs` — svi DTO-ovi ugovora (Vendor, Price, Region, Suggest, BudgetDefaults,
  VendorProfile, ImportedReview) spremni za Fazu 1; JSON: camelCase + izostavljanje null polja
  (identično Next.js mocku)
- `Data/AppDbContext.cs` — prazan EF Core kontekst (entiteti dolaze u Fazi 1)
- EF Core + Npgsql 8.0.11 u csproj; CORS za `http://localhost:3000` s credentials (priprema za fazu 3)
- `backend/docker-compose.yml` (api :5080 + postgres:16 na **:5433** da ne kolidira s lokalnim
  Postgresom), `Dockerfile` (multi-stage), `.dockerignore`
- U repo dodani `PLAN-ARHITEKTURA.md` i ovaj `STANJE.md`; `.gitignore` proširen za .NET

**Verificirano u sandboxu:** build prolazi; `/api/vendors` i `/health` smoke-testirani i
vraćaju ispravan JSON. ⚠️ Sandbox nema pristup nuget.org pa je build verificiran s lokalno
stubanom EF površinom — **vlasnik treba potvrditi `dotnet build` s pravim paketima** (očekuje
se prolaz, korišten je standardni EF boilerplate). Docker compose nije pokretan u sandboxu.

**Odluke:** primijenjene preporuke iz PLAN-ARHITEKTURA.md §11 (Postgres+pg_trgm, Docker…).
Port mape: API 5080, Postgres u composeu 5433→5432.

**Sljedeći koraci (Faza 1):**
1. Entiteti iz plana §3 + EF migracije
2. Import komanda za Excel (2500 pružatelja) — idempotentna, čišćenje iz §4
3. `/api/vendors` s pravim filtrima + `pg_trgm` typeahead za `/api/suggest`
4. `/api/regions`, `/api/budget-defaults`, `/api/vendors/{slug}` nad bazom
   (šifrarnici regija/kategorija ostaju u kodu, zrcalo `lib/data.ts`)

**Za Fazu 1 vlasnik treba pripremiti:** Excel s 2500 pružatelja (format
`data/vendors-template.xlsx`) — dodati u repo ili dostaviti u chat.
