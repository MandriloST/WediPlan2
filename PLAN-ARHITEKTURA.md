# wediplan — Plan, arhitektura i upute za nastavak rada

> 📍 **KANONSKI REPO: https://github.com/MandriloST/WediPlan2.git** (grana `develop`). Ovo je JEDINI ispravan repo — NE `WediPlan`/`wediplan` bez 2.


> **Namjena dokumenta:** Ovo je glavni radni dokument za buduće razgovore u ovom projektu.
> Model (Sonnet/Opus) koji nastavlja rad treba ga pročitati PRIJE pisanja koda, zajedno s
> `README.md` i `API.md` u repou. Odluke označene **[ZA ODOBRENJE]** vlasnik mora potvrditi
> prije implementacije — ako u razgovoru nije rečeno drugačije, koristi se preporučena opcija.

---

## 1. Snimka postojećeg stanja (rujan 2026.)

**Repo:** `https://github.com/MandriloST/WediPlan2.git` — radi se ISKLJUČIVO na grani `develop`.
> ⚠️ **JEDINI ISPRAVAN REPO je `WediPlan2` (s brojem 2).** NIKAD `WediPlan`/`wediplan` bez 2 — to je stari, GDPR-zaraženi repo (ide na private). Ako model ode na krivi, STANI i prebaci se na WediPlan2.
`main` je produkcija; vlasnik sam merga develop → main preko GitHub PR-a. Model nikad ne dira `main`.

**Što postoji i radi (frontend, Next.js 14 App Router):**
- Istraži/landing: search s typeaheadom, lista regija s brojačima, MapLibre karta Hrvatske (od 2026-09-19 landing = 3a, grid na `/kategorije`)
  sa stvarnim GeoJSON granicama 5 regija, cijene na pinovima, klasteri, popup kartice
- URL-driven filtri (`/dalmacija/foto-i-video` — shareable/SEO), sitemap, robots
- Usporedba 2–4 pružatelja, budžet kalkulator (drawer/tab), "Vaš plan" s capovima
- Profil pružatelja (`/pruzatelj/[slug]`) s uvezenim recenzijama i mock kalendarom dostupnosti
- Mobilna PWA: 5 tabova, manifest, service worker, offline plan/favoriti (localStorage)
- 29 kategorija (slugovi stabilni u `lib/data.ts`), cjenovni modeli: `from`, `perPerson`, `na upit`
- Excel → JSON import pipeline (`scripts/import-vendors.mjs`, `data/vendors-live.xlsx`)
- Deploy: Vercel (main = produkcija, develop = preview)

**Što NE postoji:** backend (.NET), baza podataka, auth, provider claim, prave recenzije
korisnika, admin sučelje, pohrana slika izvan gita. API rute u `app/api/*` su mock koji
čita JSON — `API.md` je ugovor koji .NET servis treba implementirati identično.

**Novi resurs:** vlasnik je prikupio **2500+ pružatelja** (≈100 po kategoriji) — trenutno
izvan repoa (Excel). To je okidač za prelazak s JSON-a na pravu bazu.

---

## 2. Ciljana arhitektura

```
[Korisnik] → Cloudflare (DNS, WAF, bot zaštita, cache)
              ├── wediplan.hr        → Vercel (Next.js frontend, PWA)
              └── api.wediplan.hr    → .NET 8 API (ASP.NET Core) u Dockeru
                                        ├── PostgreSQL 16 (podaci)
                                        └── Object storage (slike pružatelja)
```

- Frontend ostaje na Vercelu (već postavljeno, radi dobro, preview po grani).
- Backend je **ASP.NET Core 8 Web API** (vlasnikov teren) koji implementira postojeći
  `API.md` ugovor 1:1, plus nove endpointe (auth, claim, recenzije — v. §5–6).
- Frontend se na .NET prebacuje env varijablom **`API_URL`** (server-only): `next.config.mjs`
  tada `beforeFiles` rewriteom šalje `/api/*` na .NET, a server komponente dohvaćaju preko
  `lib/api/server.ts`. **Ispravak (Faza 2):** izvorna pretpostavka "jedan rewrite, komponente se
  ne diraju" nije vrijedila — 8 mjesta čitalo je mock JSON izravno; refaktorirano tako da
  komponente koriste isključivo `lib/api/client.ts`, a mock živi u `lib/mock/` (`server-only`).
  Rewrite (isti origin) usput uklanja potrebu za CORS-om i olakšava cookie auth u Fazi 3.

### 2.1 [ZA ODOBRENJE] Hosting backenda
| Opcija | Trošak | Napomena |
|---|---|---|
| **A (preporuka): Hetzner VPS** (CX22, ~5 €/mj) + Docker Compose (API + Postgres + Caddy) | ~5–10 €/mj | Najjeftinije, EU lokacija (GDPR-friendly), potpuna kontrola; zahtijeva osnovni ops (backup skripta, docker compose pull za deploy) |
| B: Azure App Service B1 + Azure Database for PostgreSQL | ~30–60 €/mj | Managed, prirodno za .NET, ali 5–10× skuplje; ima smisla tek kad promet naraste |
| C: Fly.io / Railway | ~10–20 €/mj | Sredina; manje standardno za .NET |

Preporuka: **A** za lansiranje. Backup baze: noćni `pg_dump` na object storage. Migracija
na Azure kasnije je trivijalna jer je sve u Dockeru.

### 2.2 [ZA ODOBRENJE] Baza i pretraga
- **PostgreSQL 16 + EF Core.** Za 2500–10.000 pružatelja **NE treba** Typesense/Meilisearch:
  Postgres `pg_trgm` (typeahead, tolerancija tipfelera) + generirani `tsvector` stupac
  (full-text) pokrivaju sve postojeće search zahtjeve uz jednostavniju infrastrukturu.
  Dedicated search engine dodati tek ako typeahead postane spor (>50k zapisa) — odluka se
  tada ne tiče frontenda jer je `/api/suggest` ugovor stabilan.

### 2.3 [ZA ODOBRENJE] Slike
- Slike pružatelja SELE SE IZ GITA u object storage: **Cloudflare R2** (preporuka —
  nema egress naplate, S3-kompatibilan, ~0 €/mj na ovom volumenu) ili Azure Blob.
- Upload/resize radi backend (varijante: thumb 400px, card 800px, full 1600px, WebP).
- Frontend koristi postojeći `lib/images.ts` — samo se izvor mijenja s `/public` na CDN URL.
- Default slike po kategoriji (29 kom) ostaju u repou.
- Vodeni žig (suptilan, kut slike) dodaje se pri generiranju varijanti — dio anti-scraping
  strategije (v. §8), ali NE na slikama koje pružatelj označi kao vlastiti portfolio s
  vlastitim žigom.

---

## 3. Model podataka (PostgreSQL, EF Core entiteti)

Minimalan skup za faze 1–4. Imena tablica engleski, sadržaj hrvatski.

```
vendors            id (uuid), slug (unique), name, category_slug (PRIMARNA, v. §4.3),
                   region_slug, city,
                   lat (nullable), lng (nullable),
                   location_precision (exact|city|region),
                   coverage_regions text[] (prazno = samo vlastita regija; ['hr'] = cijela HR),
                   coverage_note text (nullable),
                   instagram_url text (nullable), facebook_url text (nullable),   -- JAVNO (ikone na profilu)
vendor_categories  vendor_id, category_slug, is_primary (bool) — M2M, v. §4.3
events             v. §A — analitika (Faza 1)
daily_stats        v. §A — agregati za reporte i premium statistiku
sponsorships       v. §M.4 (Faza 1, prazan do naplate)
subscriptions      v. §M.4 (faza 3/4 — uz claim)
                   price_kind (from|per_person|on_request), price_from, price_to,
                   style_tags text[], about, services text[], website, phone, email,
                   verified bool, live_calendar bool, claim_status (unclaimed|pending|claimed),
                   owner_user_id (nullable FK), is_published bool, opt_out bool,
                   search tsvector (generated), created_at, updated_at
vendor_photos      id, vendor_id, storage_key, sort_order, is_cover
imported_reviews   id, vendor_id, author, rating, text, source, year   -- "što oni kažu"
user_reviews       id, vendor_id, user_id, rating, text, status (pending|published|rejected),
                   created_at                                          -- "što korisnici kažu"
users              id, email (unique), name, google_sub (nullable), password_hash (nullable),
                   role (couple|provider|admin), created_at
magic_links        id, user_id, token_hash, expires_at, used_at
claims             id, vendor_id, user_id, message, evidence (npr. email s domene weba),
                   status (pending|approved|rejected), decided_by, decided_at
favorites          user_id, vendor_id  (PK par)
budget_plans       id, user_id, guests, region_slug, total, caps jsonb, updated_at
profile_views      vendor_id, day (date), views int  -- agregirano po danu, ne po requestu
```

Napomene:
- `claim_status` + `owner_user_id` na vendoru je izvor istine tko smije uređivati.
- `opt_out` podržava GDPR zahtjev "ovo nije moj profil / uklonite me" (v. §9).
- `profile_views` je namjerno dnevni agregat (privacy + jeftino), dovoljan za
  "profil pregledan 340× ovaj mjesec" vrijednost pružateljima.
- Kategorije i regije ostaju sifrarnici u kodu (kao danas u `lib/data.ts`), NE tablice —
  mijenjaju se rijetko, a slugovi su ugovor s URL-ovima/SEO-om.

---

## 4. Import 2500 pružatelja

- Postojeći Excel format (`data/vendors-template.xlsx`) je polazna točka; import skripta
  se prepisuje da piše u Postgres (`scripts` → .NET konzolna komanda `dotnet run --import`
  ili zadržati Node skriptu koja gađa API — **[ZA ODOBRENJE]**, preporuka: .NET komanda,
  jedan jezik za sav backend posao).
- Import mora biti **idempotentan** (ključ: slug): ponovno pokretanje ažurira, ne duplicira.
- Obavezni koraci čišćenja pri importu: dedup po (name+city), normalizacija regija/kategorija
  na postojeće slugove, validacija cijena (from ≤ to; "na upit" ⇒ bez brojki), geokodiranje
  zapisa bez lat/lng (Nominatim uz rate limit, cache rezultata).
- Svi importirani pružatelji kreću kao `claim_status = unclaimed`, `verified = false`.
  `verified = true` ostaje rezerviran za platformom provjerene pružatelje (postojeći badge).

### 4.1 Sjedište vs. pokrivanje — IMPLEMENTIRANO u templateu i Node importu (rujan 2026.)

Odluka **[ZA ODOBRENJE #7]** (implementirana na frontend/Excel razini; .NET je preuzima u Fazi 1):
- **Sjedište** (`city, lat, lng, region_slug`) i **pokrivanje** (`coverage_regions`) su odvojeni
  koncepti. Jedan pružatelj = jedan pin (na sjedištu); nikad više pinova, nikad izmišljena
  koordinata.
- `location_precision` se IZVODI pri importu: koordinate → `exact`; samo grad → `city`
  (geokodiranje u Fazi 1: Nominatim, cache, rate limit — dotad bez pina); samo regija →
  `region` (bez pina, vidljiv u listi regije, kartica prikazuje "pokriva regiju").
- Dvorane (`restorani-i-sale`, `konobe-i-prostori`, `najam-kuce`) MORAJU imati grad i
  koordinate — import ih inače odbija (lokacija im je bit ponude).
- Filtar regije X: `region == X OR X ∈ coverage_regions OR 'hr' ∈ coverage_regions`.
- Excel: novi stupci `pokriva_regije` ("Dalmacija; Kvarner" ili "cijela Hrvatska") i
  `pokrivanje_napomena` (slobodni tekst, prikazuje se na profilu); `grad` i `koordinate`
  više nisu obavezni (osim za dvorane). Primjeri svih rubnih slučajeva su u templateu.
- Prazna polja profila: sekcija se ne prikazuje (ne "Nema podataka"); opis se može
  generirati kao neutralna činjenična rečenica; mršavi unclaimed profili ističu CTA
  "Preuzmite i dopunite profil" (akvizicijski lijevak) — UI dio slijedi u kasnijoj fazi.

### 4.2 Sustav oznaka (badgeva) — IMPLEMENTIRANO v1 (rujan 2026.)

Odluka **[ZA ODOBRENJE #9]** — dogovoreni okvir: 2 slota, oznake se IZVODE iz podataka
(nikad ručni unos), negativna stanja su interna, kriteriji javni na `/oznake`.

- **Slot povjerenja** (najviše jedna, prioritet): ✓ Verificirani profil (`claim_status =
  claimed`, faza 3/4) → ✓ Provjereni podaci (`provjereno = DA` u Excelu) → Novi na
  WediPlanu (v1 proxy: `reviewCount == 0`; Faza 1 uvodi `created_at` pa proxy zamijeniti).
- **Slot zasluge** (najjača): ★ Top ocijenjen — ocjena ≥ 4.8 i ≥ 20 recenzija, izvor
  uvijek naveden. Kasnije u isti slot (uz tracking/CRM): Brzo odgovara, Popularan.
- Pragovi i logika žive SAMO u `lib/badges.ts` (frontend) — .NET u Fazi 1 preuzima ista
  pravila; pragovi se ne dupliciraju po komponentama.
- Interna negativna stanja (moguće neaktivan, prijavljen problem): Excel status
  **"Skriveno"** → import preskače redak (tiho uklanjanje). Nikad javne negativne oznake.
- Kartica: oznake prve u redu badgeva (thumb je 86px — overlay na slici tek ako kartice
  jednom dobiju velike slike). Profil: oznake u zaglavlju s tooltipom + link na `/oznake`.

### 4.3 Više kategorija po pružatelju [odluka #10 — ODOBRENO]

Many-to-many s **jednom primarnom** kategorijom (model Google Businessa). Limit **3
kategorije ukupno**; više od 3 samo uz odobrenje admina (zahtjev u provider dashboardu,
faza 4). Nakon claima pružatelj sam uređuje kategorije unutar limita.

Točna podjela odgovornosti (ovo su pravila koja komponente MORAJU slijediti):
- **PRIMARNA određuje:** default sliku, breadcrumb profila, grupu u kalkulatoru budžeta
  (jedan pružatelj se broji u TOČNO jednu budžetsku omotnicu — nikad dvostruko),
  "Slične pružatelje".
- **SVE kategorije određuju:** pojavljivanje u listinzima/filtrima i brojače kategorija.
  Posljedica: zbroj brojača po kategorijama > broj pružatelja — to je očekivano, ne bug.
- **Usporedba:** uspoređivati se smiju samo pružatelji koji dijele ≥ 1 kategoriju
  (inače atributi nisu usporedivi). UI onemogućuje dodavanje neusporedivog.
- Cijena ostaje jedna po pružatelju (v1 pojednostavljenje); cijena po kategoriji je
  premium mogućnost (§M.1).

Excel: novi stupac **`dodatne_kategorije`** (odmah nakon `kategorija`) — imena iz
šifrarnika odvojena `;`, najviše 2, ne smije ponavljati primarnu. Import validira i
zapisuje `categories = [primarna, ...dodatne]`; postojeći `category` ostaje primarna
(unatrag kompatibilno). DB (Faza 1): tablica `vendor_categories` (vendor_id,
category_slug, is_primary) uz zadržani `vendors.category_slug` kao denormaliziranu
primarnu radi jednostavnosti upita.

### §A Analitika i event tracking [odluka #11 — ODOBRENO]

Odluka: **vlastiti first-party event tracking u Postgresu — agregatno, nikad
individualno.** Bez heatmapa, session-snimki i vanjskih trackera. Cilj: reporti
("što se najviše pretražuje, kada, gdje"), sezonalnost za §M.2, i "Statistika
profila" kao premium (§M.1).

Privatnost (uvjeti pod kojima je odluka donesena — ne mijenjati bez rasprave):
- Bez PII: ne pohranjuje se IP, user id se NE veže na evente ni kad auth dođe.
- `session_hash`: nasumičan ID generiran na klijentu (sessionStorage), bez kolačića,
  ne preživljava zatvaranje taba; služi samo za "sesija = niz eventa".
- Poštivati Do Not Track / Global Privacy Control (ne slati evente).
- Dokumentirati u Pravilima privatnosti (§9); cookie notice ostaje minimalan.

Model (Faza 1, uz ostale entitete):
```
events   id bigserial, ts timestamptz, event_name text, session_hash text,
         page text, props jsonb
         indeksi: (ts), (event_name, ts); rollup job → daily_stats
daily_stats  day, event_name, category_slug, region_slug, vendor_slug, count
             (materijalizirano dnevno — reporti čitaju SAMO ovo, nikad sirove evente)
```

API: `POST /api/events` — prima batch (max 20), whitelist `event_name`, rate limit
po IP-u (IP se koristi za limit, NE pohranjuje), tihi 204 (nikad ne ruši UX).

Katalog eventa v1 (whitelist — proširenja se dodaju ovdje pa u kod):
`page_view`, `search_performed {q?, category?, region?, filters}`,
`vendor_viewed {slug, category, region}`, `map_region_clicked {region}`,
`map_pin_clicked {slug}`, `compare_added {slug}`, `compare_viewed {slugs, category}`,
`budget_calculated {guests, region}`, `outbound_click {slug, target: web|instagram|facebook}`,
`favorite_added {slug}`.
Napomena: `outbound_click` je ujedno izvor za premium "Statistiku profila" — najvrjedniji
event za dokaz vrijednosti pružateljima.

Klijent: `lib/analytics.ts` — `track(name, props)`, automatski `page_view`, red čekanja
s `sendBeacon`/batch flushom, nikad blokira render, fail-silent.

**Implementirano (Zadatak C, 2026-09-16):** `lib/analytics.ts` (red ≤ 100, batch ≤ 20, flush
4 s običnim fetchom; skrivanje/zatvaranje taba → `sendBeacon`, fallback keepalive fetch),
`components/Analytics.tsx` (page_view + dimenzije iz putanje), eventi ugrađeni u Explore,
kartu, /karta, usporedbu (store — jedno mjesto za sva tri ulaza), budžet, profil (vendor_viewed,
outbound_click za IG/FB), favorite. Dodatna zaštita: `cleanQuery` (bez `@` i nizova 6+
znamenki), `page` bez query stringa. Server: `props` samo JSON objekt ≤ 1000 znakova; rollup
reže dimenzije i koristi UTC granice dana (ispravljen bug: Npgsql odbija `Kind=Unspecified`).
Tablica eventa i props: API.md. **Obaveza za Fazu 6:** analitiku opisati u Pravilima
privatnosti (§9) — tekst mora odgovarati tablici u API.md.

## §M Monetizacija — ODOBRENO uz dopune (rujan 2026.) [odluka #8]

Strategija: **prvo publika i partneri, pa naplata.** Do tada sve besplatno; podaci
(analitika, §A) se skupljaju od prvog dana da naplata krene informirana.

Načela (trajna, ne krše se ni u jednom proizvodu):
1. **Sponzorstvo je treći kanal** — nikad ne utječe na oznake, ocjene ni organski
   redoslijed. Uvijek označeno "Istaknuto"/"Sponzorirano" (EU obveza označavanja).
2. **Karta ostaje organska — POTVRĐENO.** Sponzorirani/obojani pinovi se ne prodaju.
   Eventualna buduća promjena zahtijeva eksplicitnu izmjenu ove odluke (ne tihu) i
   stroge uvjete: ista veličina pina, oznaka "Istaknuto" u popupu, limit po prikazu,
   nula utjecaja na brojače i klasteriranje, mjerenje utjecaja na povjerenje.
3. **Nikako, trajno:** skrivanje cijena iza plaćanja pružatelja; naplata parovima.
   Alati za parove (budžet, usporedba, karta, favoriti) ostaju besplatni — to je
   akvizicijski jarak platforme.

### M.1 Freemium profil (prvi proizvod, veže se na claim flow §6)

Granica: **free = sve što paru treba za odluku; premium = sve što pružatelja prodaje
bolje.** Prazni profili štete platformi, ne pružatelju — zato je osnovno uvijek besplatno.

- **FREE:** naziv, primarna + dodatne kategorije (do limita, §4.3), regija + pokrivanje,
  **cijena (uvijek vidljiva)**, osnovni opis (~300 znakova), 1 fotografija, poveznice
  web/Instagram/Facebook, sve zarađene oznake (§4.2 — oznake se nikad ne kupuju).
- **PREMIUM (pretplata, mjesečno/godišnje):** galerija s više fotografija, video,
  prošireni opis, kalendar dostupnosti uživo (kad dođe, faza 5/6), cijena po kategoriji
  (za višekategorijske pružatelje), **"Statistika profila"** — pregledi, spremanja u
  favorite, pojavljivanja u pretrazi, outbound klikovi na web/IG/FB (izravno iz §A;
  pružatelju opipljiv dokaz vrijednosti). Kasnije: odgovaranje na recenzije.
- **"Founding partner":** ograničen broj besplatnih premiuma za privlačenje partnera —
  **vremenski ograničeno (12 mj) i imenovano**, ne trajno pravo. Admin dodjeljuje.

### M.2 Istaknuti slotovi (naknadno)

- Max 2 kartice "Istaknuto" na vrhu liste, **samo na općem pregledu kategorije ILI
  regije** — čim postoji ključna riječ ili dodatni filteri (pretraga s namjerom),
  rezultati su 100% organski. Isti proizvod i u "Slični pružatelji" (1 slot).
- **Izlog na naslovnici** ("Izdvojeno ovaj mjesec", 4–6 kartica, mjesečna rotacija) —
  kad landing dobije konačni oblik.
- Sezonske cijene sponzorstva (npr. skuplje u mjesecima vršne aktivnosti) — TEK nakon
  ≥ 12 mjeseci podataka iz §A; do tada flat cjenik.

### M.3 Zarađeno priznanje

**"Vendor mjeseca" se NE prodaje** — dodjeljuje platforma po javnom kriteriju,
besplatno, odvojeno od plaćenog "Izdvojeno". Marketinški alat (mjesečna objava,
povod za kontakt s pružateljima).

### M.4 Model podataka

- `sponsorships` (vendor_id, scope: category_slug × region_slug | homepage | similar,
  slot, active_from, active_to, price) — zaseban entitet, NE stupac u vendors Excelu,
  NE badge. Od Faze 1.
- `subscriptions` (vendor_id, plan: free|premium|founding, active_from, active_to,
  granted_by za founding) — od faze claima (3/4). Premium mogućnosti se čitaju iz
  ovoga, nikad hardkodirano po vendoru.
- Nema posebne stranice "samo sponzori" — vidljivost tamo gdje korisnici već jesu.

---

## 5. Auth (korisnici i pružatelji)

- **ASP.NET Core Identity** kao temelj; tri načina prijave:
  1. **Google OAuth** (najmanji friction),
  2. **Passwordless magic link** (unos emaila → link vrijedi 15 min, `magic_links` tablica),
  3. Email + lozinka (klasika, za one koji to žele).
- **Sesije: httpOnly cookie** na domeni `.wediplan.hr` (frontend `wediplan.hr` i API
  `api.wediplan.hr` dijele cookie; `SameSite=Lax`, `Secure`). NE JWT u localStorage.
  Lokalni razvoj: oba na `localhost` (različiti portovi) — cookie radi.
- **Role:** `couple` (default), `provider`, `admin`. Jedan korisnik može imati i couple
  i provider kontekst, ali podatkovni modeli su odvojeni (favoriti/plan vs. vendor edit).
- Nakon prijave: `favorites` i `budget_plans` migriraju se iz localStorage u bazu
  (merge, ne pregazi) — postojeći Zustand storeovi dobivaju sync sloj.
- Email slanje (magic link, obavijesti): **[ZA ODOBRENJE]** Resend ili Brevo
  (oba imaju besplatan tier dovoljan za start; preporuka Resend zbog jednostavnosti).

---

## 6. Provider claim flow (specifikacija)

1. Pružatelj se registrira/prijavi (bilo koja metoda iz §5), na svom profilu ili stranici
   "Za partnere" klikne **"Ovo je moj profil — preuzmi ga"**.
2. Forma: poruka + opcionalno dokaz (npr. email na domeni web stranice iz profila —
   ako se domena poklapa, claim se označava `evidence = domain_match` i može se brže odobriti).
3. Claim ide u `pending`; pružatelj ODMAH dobiva pristup uređivanju **u draftu**
   (promjene se ne objavljuju javno dok admin ne odobri claim). Načelo: trenutna
   vrijednost za partnera > savršena verifikacija.
4. Admin (vlasnik) u minimalnom admin sučelju odobrava/odbija; kod odobrenja se draft
   objavljuje, `claim_status = claimed`, `owner_user_id` postavljen.
5. Nakon claima pružatelj može: uređivati cijenu/opis/usluge/fotografije, vidjeti
   dnevne preglede profila i broj dodavanja u usporedbe. (Puni CRM i live kalendar
   ostaju "Coming soon" — postojeća badge/fallback logika se ne mijenja.)

**Admin sučelje (minimalno, faza 4):** lista pending claimova, lista pending user
recenzija (moderacija prije objave), gumb za opt-out/unpublish pružatelja. Može biti
zaseban `/admin` dio Next.js aplikacije zaštićen `admin` rolom — ne treba poseban alat.

---

### §L Category-first pregledavanje i paginacija [odluka #12 — ODOBRENO 2026-09-14]

Razlog: katalog naraste na ~3200 pružatelja (~110 po kategoriji). Prikaz "svih" je
nepregledan, a karta bi imala pretjeran broj pinova. Zato se pregledavanje sužava na
JEDNU kategoriju.

Pravila (komponente ih moraju slijediti):
- **Landing** ne izlistava sve pružatelje. Hero = tražilica + **grid kategorija** (29,
  grupiran po budžetskim omotnicama) + odabir regije. Lista i karta se pune **tek nakon
  odabira kategorije** (odluka a).
- **Rezultati** su uvijek u opsegu jedne kategorije (opcionalno + regija). URL ostaje
  `/regija/kategorija` (SEO/shareable — nepromijenjeno).
- **Paginacija** (odluka b, c): `pageSize` default **24**, cap 50 (već u ugovoru);
  UI "Učitaj još" (load-more) + `?page=` linkovi s `rel=next/prev` za crawlere; ukupan
  broj vidljiv ("214 rezultata").
- **Karta** (odluka d): pinovi samo odabrane kategorije; zbog geokodiranja po gradu
  (centroid) dodati **mali deterministički jitter** po slugu oko centroida da se pinovi
  ne slažu jedan na drugi; klasteri i dalje rade.

API posljedica (Faza 1): NOVI **`GET /api/categories?region=`** → `[{slug,name,group,count}]`
(count po SVIM kategorijama §4.3; opcionalno region-scoped). Klijent više nema sve
pružatelje lokalno pa ne može sam brojati. `/api/vendors`: category ostaje opcionalan u
ugovoru (SEO/robusnost), ali frontend ga na landingu ne zove bez kategorije.

**Implementacija (Faza 2 + Zadatak D, 2026-09-16):** landing `/` i `/regija` = grid kategorija
(brojači `/api/categories`, SSR) + karta samo s regijama; `/[regija/]kategorija` = H1 + traka
srodnih kategorija + regije s brojačima (`/api/regions?category=`) + karta s pinovima te
kategorije (`/api/pins`) + lista (SSR prve stranice, "Učitaj još" kao `<a rel="next">`,
`?page=N` kanonski, stranica iza kraja = 404). Tekst-pretraga (`?q=`) je unutar trenutne
kategorije ili, bez nje, preko svih (noindex). Jitter: Vogelova spirala po hashu sluga
(`lib/jitter.ts`) — city 300 m razmak, identične exact koordinate 40 m; klasteri do z13.
Napomena SEO: Google od 2019. ne koristi `rel=next/prev` kao signal — bitni su puzivi linkovi
i SSR sadržaj, što je implementirano.

**Redizajn naslovnice (3a, 2026-09-19, na zahtjev vlasnika):** `/` više nije grid kategorija nego
landing (hero + tražilica → 6 pločica kategorija → najbolje ocijenjeni → karta s regijama); pravilo §L
ostaje: naslovnica ne izlistava sve pružatelje (samo 3 kartice, `pageSize=1` po kategoriji) i karta
nema pinova bez kategorije. Puni grid 29 kategorija živi na `/kategorije` (i `/regija`).

**Geokodiranje (napomena za import):** u trenutnom Excelu 5 redova ima koordinate, 3172
samo grad (→ Nominatim, precision=city, jitter na karti), 1 samo regiju (bez pina).
Geokodiranje je jednokratni korak importa (1 req/s + cache); vlasnik NE popunjava
koordinate ručno.

---

## 7. Faze rada (svaka faza ≈ jedna radna sesija s modelom)

> Za svaku fazu: raditi na `develop` (ili kratkoživućoj `claude/*` grani mergeanoj u
> develop), commit poruke na hrvatskom s `feat:`/`fix:` prefiksom, build mora proći
> (`npm run build` za frontend, `dotnet build` za backend) prije predaje.

**Faza 0 — kostur backenda.** Novi folder `backend/` u repou (monorepo): ASP.NET Core 8
Web API projekt, EF Core + Npgsql, Docker Compose (api + postgres) za lokalni razvoj,
health endpoint, CI-friendly struktura. Definition of done: `docker compose up` digne
API koji vraća prazan `/api/vendors` odgovor u ispravnom obliku iz `API.md`.

**Faza 1 — model + migracije + import.** Entiteti iz §3 (uklj. vendor_categories §4.3,
events + daily_stats §A, prazan sponsorships §M.4), EF migracije, .NET konzolna import
komanda (odluka #4) za Excel s ~3200 pružatelja (idempotentna po slugu, s čišćenjem iz §4,
uklj. dodatne_kategorije, pokrivanje §4.1 i geokodiranje gradova bez koordinata — 1 req/s
+ cache). Kontakti (telefon/email) uvoze se u bazu ali se NE serijaliziraju u javni
`/api/vendors` (odluka #13 / §8-§9). `POST /api/events` + rollup job. NOVI `GET /api/categories`
s brojačima (§L). DoD: baza puna; `/api/vendors` s filtrima (q, region, category, page,
pageSize default 24 cap 50) vraća stvarne podatke identično mock obliku bez kontakata;
`pg_trgm` typeahead za `/api/suggest`; `/api/categories` i `/api/regions` vraćaju brojače.

**Faza 2 — spajanje frontenda.** `rewrites` na .NET API, gašenje mock ruta (ostaju u
repou kao referenca dok sve ne prođe), regresijska provjera: karta, filtri, usporedba,
budžet, profili, sitemap. U ovoj fazi i `lib/analytics.ts` klijent (§A) s eventima
uključenim u postojeće komponente. DoD: frontend na Vercel previewu radi nad pravim
API-jem; eventi se tiho pune u bazu.

**Faza 3 — auth.** §5 u cijelosti + migracija localStorage → account. DoD: prijava
sva tri načina radi, favoriti/plan sinkronizirani, odjava/istek sesije uredni.

**Faza 4 — claim + admin + recenzije korisnika. ✅ IMPLEMENTIRANO 2026-09-17.** §6, `POST /api/reviews`
(auth required, ide u moderaciju), minimalni admin. DoD: cijeli put od registracije
pružatelja do odobrenog claima i objavljene korisničke recenzije prolazi ručni test.
Implementirano: `claims`/`user_reviews`/`vendor_drafts`/`subscriptions` entiteti +
Claims/Provider/Reviews/Admin kontroleri; frontend claim CTA + recenzije na profilu, `/partner`
nadzorna ploča (draft/statistika/objava), `/admin` moderacija; `--make-admin` CLI. Migraciju
`Faza4` generira vlasnik (`dotnet ef migrations add Faza4`). Otvoreno: #19 (stapanje ocjena).

**Faza 5 — slike + produkcijsko očvršćivanje. ✅ IMPLEMENTIRANO (2026-09-17).** R2 storage + upload u provider
dashboardu + varijante/WebP + žig; rate limiting middleware; Cloudflare ispred svega;
backup baze; monitoring (uptime + error log). DoD: checklista u §8 zadovoljena.
Kod: `Media/*`, `PhotosController`, rate limiter + `/api/health` u `Program.cs`, `ops/backup.sh`.
Preostalo za produkciju (ops, ne kod): R2 bucket + env, Cloudflare proxy, uptime monitor, backup cron.

**Faza 6 — lansiranje. ⏳ KOD IMPLEMENTIRAN (2026-09-17); preostaju ops koraci.** Domena, `NEXT_PUBLIC_SITE_URL`, pravne stranice (§9),
Google Search Console, finalna regresija, merge u `main`.
Kod: pravne stranice (`/pravila-privatnosti`, `/uvjeti-koristenja`, `/impressum`), `Footer` + `CookieNotice`,
GDPR opt-out (`OptOutController` + forma na neclaimanom profilu + admin pregled/vraćanje).
Već postojalo: `robots.ts`, `sitemap.ts`, `lib/site.ts`, `.Published()` filtar (`!OptOut`).
**Preostalo (ops, vlasnik):** kupiti domenu + `NEXT_PUBLIC_SITE_URL`, popuniti podatke tvrtke u pravnim
stranicama + pravna provjera, Google Search Console (verifikacija), finalna regresija, **merge `develop` → `main`**.

Redoslijed 0→2 je fiksan; 3 i 4 mogu zamijeniti mjesta ako vlasnik želi ranije claim.

---

## 8. Sigurnost i anti-scraping (sažetak — puni dokument: `zastita-od-scrapinga.md`)

Ugrađeno u arhitekturu od početka:
- Cloudflare ispred frontenda i API-ja (bot fight mode, rate limiting, JS challenge)
- `Microsoft.AspNetCore.RateLimiting`: stroži limit na `/api/vendors` (liste) nego na
  pojedinačne profile; limit po IP-u
- Paginacija uvijek (pageSize ≤ 50, već u ugovoru); NIKAD endpoint "svi pružatelji"
- Kontakt podaci pružatelja (telefon, email) učitavaju se zasebnim pozivom na klik,
  ne u list responseu
- Žig na slikama (v. §2.3); ToS klauzula protiv automatskog prikupljanja
- NE raditi sada: enterprise anti-bot alati, agresivna headless detekcija,
  bilo što što šteti SEO-u ili pristupačnosti

---

## 9. GDPR i pravni minimum (prije lansiranja — faza 6)

- **Opt-out mehanizam:** na svakom unclaimed profilu link "Ovo je moj obrt/firma i ne
  želim biti na stranici" → forma → `opt_out = true`, profil se skida iz javnog prikaza.
  Ovo je ključno jer je 2500 profila prikupljeno bez prijave samih pružatelja.
- Prikazivati samo podatke koje su pružatelji sami javno objavili (web, javni imenici).
  Osobne mobitele fizičkih osoba (obrtnika) ne objavljivati bez claima.
- Stranice: Pravila privatnosti, Uvjeti korištenja (s anti-scraping klauzulom),
  Impressum, cookie notice (minimalan — bez marketinških kolačića na startu).
- Recenzije korisnika: moderacija prije objave (već u §6), mogućnost brisanja na zahtjev.

---

## 10. Upute modelu koji nastavlja rad (pročitati prije koda)

1. **Grane:** sav rad na `develop` ili `claude/*` → develop. `main` se NE dira.
   Ako push iz sandboxa nije moguć, predati git bundle + točne upute za merge
   (ustaljeni workflow ovog projekta).
2. **Izvori istine:** `API.md` (oblici odgovora — .NET ih mora vraćati identično),
   `lib/data.ts` (slugovi kategorija/regija — ne mijenjati bez izričitog zahtjeva),
   ovaj dokument (arhitektura i faze).
3. **Ne redizajnirati postojeći UI** bez zahtjeva — vizualni smjer je odobren.
   UI copy je hrvatski (hr-HR); i18n (engleski) je planiran kasnije, ne sada.
4. **Cjenovna transparentnost je core diferencijator:** cijena uvijek vidljiva, bold,
   accent boja; "na upit" vizualno prigušen; pružatelji izvan budžeta posivljeni,
   nikad skriveni. Ta pravila se ne krše ni u jednoj novoj komponenti.
5. Prije predaje: frontend `npm run build` bez grešaka; backend `dotnet build` +
   migracije primjenjive na praznu bazu; kratki opis promjena i ručnih koraka za vlasnika.
6. Na početku sesije provjeriti stvarno stanje repoa (`git log`, struktura) — ne
   oslanjati se samo na ovaj dokument, moglo se promijeniti.
7. Vlasnik je programer (.NET) — objašnjenja mogu biti tehnička, ali koraci za
   pokretanje/merge uvijek eksplicitni i copy-paste spremni.

---

## 11. Odluke — ✅ SVE ODOBRENE (evidencija)

> ✅ **SVE ODLUKE #1–#19 ODOBRENE (preporučena opcija) — vlasnik, 2026-09-17.**
> Nema više otvorenih odluka. Tablica ispod ostaje kao evidencija izbora.

| # | Odluka | Preporuka / Status |
|---|---|---|
| 1 | Hosting backenda (§2.1) | ✅ ODOBRENO 2026-09-17 — Hetzner VPS + Docker |
| 2 | Pretraga (§2.2) | ✅ ODOBRENO 2026-09-14 — Postgres pg_trgm + FTS, bez Typesensea |
| 3 | Pohrana slika (§2.3) | ✅ ODOBRENO 2026-09-17 — Cloudflare R2 + WebP + žig (Faza 5) |
| 4 | Import alat (§4) | ✅ ODOBRENO 2026-09-14 — .NET konzolna komanda |
| 5 | Email servis (§5) | ✅ ODOBRENO 2026-09-17 — Resend |
| 6 | Redoslijed faza 3↔4 (§7) | ✅ ODOBRENO 2026-09-17 — Auth prije claima |
| 7 | Sjedište vs. pokrivanje + location_precision (§4.1) | ✅ ODOBRENO 2026-09-17 — implementirano u pipelineu |
| 8 | Monetizacija (§M) | ✅ ODOBRENO s dopunama: freemium granica M.1, Founding partner, karta trajno organska |
| 9 | Sustav oznaka: 2 slota, pragovi Top ocijenjen 4.8/20 (§4.2) | ✅ ODOBRENO 2026-09-17 — v1 (pragovi se fino ugađaju na stvarnim podacima) |
| 10 | Više kategorija: M2M + primarna, limit 3 (§4.3) | ✅ ODOBRENO — Excel/import + UI (Zadatak A) implementirani |
| 11 | Analitika: vlastiti first-party, agregatno bez PII (§A) | ✅ ODOBRENO — implementacija Faza 1-2 |
| 12 | Category-first pregledavanje + paginacija (§L) | ✅ ODOBRENO 2026-09-14 (defaulti a-d) — v. §L |
| 13 | GDPR kontakti pri importu: uvezi u bazu, NE izlaži javno (§8/§9) | ✅ ODOBRENO 2026-09-14 |
| 14 | Dopune ugovora za Fazu 2: `/api/pins` (category obavezna, cap 1000), `/api/vendors?ids=` (≤50), `/api/regions?category=`, `/api/budget-matches` (klijent šalje capove), `/api/sitemap`; suggest vendor → profil | ✅ ODOBRENO 2026-09-17 (implementirano 2026-09-16) |
| 15 | Brojači regija po pravilu liste (sjedište ∪ pokrivanje) umjesto samo sjedišta | ✅ ODOBRENO 2026-09-17 |
| 16 | Tekst-pretraga na stranici kategorije traži unutar kategorije (prazno stanje nudi "u svim kategorijama") | ✅ ODOBRENO 2026-09-17 |
| 17 | Rate limit iza proxyja: `Proxy:TrustForwardedFor` (default false; true tek kad je API dostupan samo preko Cloudflarea/Vercela) | ✅ ODOBRENO 2026-09-17 — uključuje se u Fazi 5 |
| 18 | **GDPR incident:** repo je javan, a `data/vendors-live.xlsx` (2295 tel., 1748 email) je u povijesti gita | ✅ RIJEŠENO — novi čisti repo WediPlan2 (`.gitignore` drži Excel izvan gita) |
| 19 | **Stapanje korisničkih ocjena u `vendor.rating`/`reviewCount`** (Faza 4) | Implementirano ODVOJENO (profil prikazuje Wediplan recenzije zasebno); rating iz importa se ne dira. ✅ ODOBRENO 2026-09-17 — ostaje ODVOJENO (Wediplan recenzije zasebno) |

**Faza 4 (claim + admin + korisničke recenzije) — IMPLEMENTIRANA 2026-09-17** (v. STANJE.md).

Preostale otvorene: **nema — sve odluke #1–#19 odobrene 2026-09-17.**
#14–#16 (potvrda), #3 (R2, Faza 5), #5 (Resend ključ), #7, #9.
