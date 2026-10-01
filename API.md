# API ugovor (za ASP.NET Core Web API)

Mock implementacija živi u `app/api/*` + `lib/mock/*` — .NET servis vraća identične oblike.

**Faza 2 (spajanje):** frontend uvijek zove relativne `/api/*`. Server-only env `API_URL`
(npr. `http://localhost:5080`) uključuje `beforeFiles` rewrite u `next.config.mjs` → svi
`/api/*` idu na .NET (mock rute se tada ne izvršavaju); server komponente dohvaćaju izravno s
`API_URL` (`lib/api/server.ts`). Bez `API_URL` rade mock rute. Komponente NIKAD ne zovu
`fetch` izravno — samo `lib/api/client.ts`.

## GET /api/vendors
Query: `q, region, category, date, page (1+), pageSize (≤50), ids`

`ids` (Faza 2): `uuid,uuid,…` (max 50) — vraća točno te objavljene pružatelje (usporedba,
favoriti); ostali filtri se ignoriraju; nepoznati/skriveni se izostavljaju (klijent ih tada
briše iz localStoragea). Odgovor: `{ items, total: n, page: 1, pageSize: n }`.
`q` je ograničen na 80 znakova; `%`/`_` iz unosa se escapeaju.
Napomene (§L, §8-§9): `pageSize` default 24, hard cap 50; frontend na landingu ne poziva
bez `category` (category-first). Kontakti (`phone`, `email`, `web`) se NE vraćaju u listi —
učitavaju se zasebnim pozivom na klik (kasnija faza).
Inozemni pružatelji (BiH/SI): `country: "ba"|"si"` (izostavljeno za HR), `region: ""` —
pronalaze se preko coverage-a, pin ide po koordinatama.
```json
{ "items": [], "total": 53, "page": 1, "pageSize": 24 }
```
Vendor:
```json
{
  "id": "v1", "slug": "foto-studio-anic", "name": "Foto studio Anić",
  "category": "foto-i-video", "region": "dalmacija", "city": "Split",
  "lng": 16.44, "lat": 43.51,
  "locationPrecision": "exact",
  "coverage": ["dalmacija", "kvarner"],
  "coverageNote": "radi u Splitu, Zadru i Šibeniku",
  "price": { "kind": "from", "from": 850 },
  "rating": 4.8, "reviewCount": 31,
  "verified": true, "liveCalendar": false,
  "styleTags": ["boho", "film"], "photo": null,
  "social": { "instagram": "https://instagram.com/foto.anic", "facebook": "https://facebook.com/fotoanic" }
}
```
(`price` alternativa: `{ "kind": "perPerson", "from": 55, "to": 80 }`)

Lokacija i pokrivanje (sjedište ≠ područje rada):
- `city`, `lng`, `lat` opisuju SJEDIŠTE. `lng`/`lat` mogu biti `null` (koordinate nepoznate) — klijent tada NE crta pin; `city` može biti `""` (poznata samo regija).
- `locationPrecision`: `"exact"` (koordinate) | `"city"` (grad bez koordinata — čeka geokodiranje) | `"region"` (samo regija). Opcionalno (stariji zapisi ga nemaju → tretirati kao `"exact"`).
- `coverage`: regije u kojima pružatelj radi UZ svoju — polje slugova ili `"hr"` (cijela Hrvatska). Opcionalno; izostanak = radi samo u `region`.
- Filtar `region=X` vraća pružatelje gdje `region == X` ILI `coverage` sadrži `X` ILI `coverage == "hr"`. Pin ostaje samo na sjedištu (jedan pružatelj = jedan pin).
- Kategorije dvorana (`restorani-i-sale`, `konobe-i-prostori`, `najam-kuce`) uvijek imaju `city` + koordinate — import to garantira.

Društvene poveznice:
- `social` (opcionalno): `{ instagram?, facebook? }`, normalizirani `https://` URL-ovi — JAVNO, prikazuju se kao ikone na profilu. Za razliku od `web`/`telefon`/`email` koji ostaju interni ("Direktan kontakt: zasad ne").

Oznake (badgevi):
- `claimStatus` (opcionalno): `"unclaimed" | "claimed"` — importi ga zasad ne šalju; `"claimed"` (faza claima) aktivira oznaku "✓ Verificirani profil".
- Oznake se NE šalju kroz API — klijent ih izvodi iz `claimStatus`/`verified`/`rating`/`reviewCount` (pravila i pragovi: `lib/badges.ts`, javno objašnjeno na `/oznake`). .NET preuzima ista pravila bude li ih ikad računao server-side.

## GET /api/regions?category=
Brojači po **pravilu liste** (sjedište ∪ pokrivanje — Faza 2; zbroj > ukupno je očekivan), tako
da broj uz regiju odgovara broju rezultata nakon klika. Opcionalni `category` sužava brojače.
```json
[{ "id": "dalmacija", "name": "Dalmacija", "center": [16.4, 43.6],
   "bounds": [[14.5, 42.35], [18.6, 44.6]], "count": 18 }]
```

## GET /api/categories?region=
Brojači po SVIM kategorijama (§4.3 — zbroj > broj pružatelja je očekivan). Opcionalni
`region` sužava brojače na regiju (sjedište ili pokrivanje). Za category-first landing (§L).
```json
[{ "slug": "foto-i-video", "name": "Foto i Video", "group": "foto", "count": 112 }]
```

## GET /api/suggest?q=
```json
[{ "type": "category", "label": "Foto i Video", "sub": "kategorija", "href": "/foto-i-video" },
 { "type": "vendor", "label": "Foto studio Anić", "sub": "Fotografi · Split", "href": "/pruzatelj/foto-studio-anic" }]
```
(`type`: category | region | city | vendor). Faza 2: `vendor` vodi ravno na profil.

## GET /api/pins?category=&region=&q=
Karta (Faza 2, §L odluka d). **`category` obavezna** (inače 400 — nikad "svi pružatelji", §8).
Samo pružatelji s koordinatama, rangirani kao lista, **cap 1000**; `total` = svi s koordinatama
(ako je `total > items.length`, klijent prikazuje "suzite regiju"). Koordinate su istinite —
jitter oko centroida grada radi klijent (`lib/jitter.ts`).
```json
{ "items": [{ "id": "…", "slug": "…", "name": "…", "category": "foto-i-video",
  "categories": ["foto-i-video", "foto-kabine"], "city": "Split", "lng": 16.44, "lat": 43.51,
  "locationPrecision": "city", "price": { "kind": "from", "from": 850 },
  "rating": 4.8, "reviewCount": 31, "photos": ["01.jpg"] }], "total": 112 }
```
(`categories` samo kad > 1; `photos` najviše 1 — naslovna.)

## GET /api/budget-matches?region=&guests=&sala=&catering=&foto=&glazba=&ostalo=
Kalkulator budžeta (Faza 2). Klijent šalje capove koje je sam izračunao (`computeCaps`), pa
zaokruživanje postoji na jednom mjestu. Regija po pravilu liste; grupa = primarna kategorija;
procjena troška: `from` → from, `perPerson` → from × guests, `onRequest` → 0 (ne isključuje se).
`region` obavezna (inače 400).
```json
{ "region": "dalmacija", "matches": 412, "groups": { "sala": 60, "catering": 41, "foto": 97, "glazba": 55, "ostalo": 159 } }
```

## GET /api/sitemap
Samo slugovi objavljenih pružatelja + zadnja izmjena (za `sitemap.xml`, osvježava se 1×/h).
```json
[{ "slug": "foto-studio-anic", "updatedAt": "2026-09-16T08:00:00Z" }]
```

## GET /api/budget-defaults?region=
```json
{ "region": "dalmacija", "shares": { "sala": 0.42, "catering": 0.24, "foto": 0.15, "glazba": 0.09, "ostalo": 0.1 } }
```

## GET /api/vendors/{slug}
Profil pružatelja. `about: ""` i `services: []` kad nisu uneseni — frontend tada prikazuje
zadani tekst kategorije (`lib/profile.ts withProfileDefaults`). 404 za nepostojeće/skrivene.
`userReviews` (Faza 4) su OBJAVLJENE recenzije korisnika platforme ("što korisnici kažu");
izostavljeno kad ih nema. `importedReviews` su prenesene ("što oni kažu"): **odbijene** (admin nije prihvatio dokaz) se ne vraćaju,
a `verified: true` znači da je admin provjerio izvor (frontend prikazuje bedž "provjereno"); `verified` je `false` za neprovjerene (Zadatak 16).
```json
{ "vendor": {}, "about": "…", "services": ["…"],
  "importedReviews": [{ "author": "Marija i Ivan", "rating": 5, "text": "…", "source": "Google recenzije", "year": 2025, "verified": true }],
  "userReviews": [{ "id": "…", "author": "Ana", "rating": 5, "text": "…", "createdAt": "2026-09-17T08:00:00Z" }] }
```

## Auth (Faza 3, §5)

Sesija = httpOnly cookie `wediplan.session` (Identity aplikacijski cookie, SameSite=Lax, Secure u
produkciji). Klijent NE vidi token; svi pozivi idu s `credentials: "include"`. Odgovori na
"zatraži link/reset" NE otkrivaju postoji li email (zaštita od enumeracije).

- `POST /api/auth/register` `{email,password,displayName?}` → `{message}`; šalje verifikacijski mail.
- `POST /api/auth/login` `{email,password}` → `MeDto` + postavlja cookie; 401 `invalid_credentials`
  ili `email_not_confirmed`; 429 `locked_out` (8 promašaja → 15 min).
- `POST /api/auth/logout` → `{message}`; briše cookie.
- `POST /api/auth/magic/request` `{email}` → `{message}` (link vrijedi 15 min; max 3/15 min po emailu).
- `POST /api/auth/magic/consume` `{token}` → `MeDto` + cookie (kreira korisnika pri prvom korištenju,
  email time potvrđen).
- `POST /api/auth/verify-email` `{token}` → `MeDto` + cookie (24 h rok).
- `POST /api/auth/password/forgot` `{email}` → `{message}` (Identity reset token, 1 h).
- `POST /api/auth/password/reset` `{email,token,password}` → `{message}`.
- `GET  /api/auth/providers` → `{password:true, magicLink:true, google:bool}` (google true samo ako
  su ključevi postavljeni).
- `GET  /api/auth/google?returnTo=/…` → redirect na Google → `/api/auth/google/callback` → cookie +
  redirect na frontend (samo relativni returnTo; open-redirect blokiran).
- `GET  /api/me` → `MeDto` ili 401.

### Couple podaci (favoriti/plan) — sve traže sesiju ([Authorize])
- `GET /api/favorites` → `{ favoriteIds: string[], plan: {guests,region,total}|null }`.
  favoriteIds su vendor Guid-ovi (frontend njima dohvaća pune Vendore preko `?ids=`).
- `PUT /api/favorites/{vendorId}` → 204 (dodaj, idempotentno; 409 `too_many_favorites` iznad 200).
- `DELETE /api/favorites/{vendorId}` → 204 (makni, idempotentno).
- `PUT /api/favorites/plan` `{guests,region,total}` → 204 (spremi/zamijeni plan).
- `DELETE /api/favorites/plan` → 204.
- `POST /api/favorites/merge` `{favoriteIds?, plan?}` → `{favoriteIds, plan}` — UNIJA favorita;
  plan se postavlja SAMO ako korisnik još nema plan ("merge, ne pregazi"). Zove se nakon prijave.

Favoriti NEMAJU FK na vendors: ako pružatelj nestane, zapis je bezopasan i filtrira se pri čitanju.

`MeDto`: `{ id, email, displayName?, emailConfirmed, roles[] }` (role: couple|provider|admin).
Kontakti/tokeni/hashevi se NIKAD ne vraćaju.

### Brisanje računa (§9, Plan prioriteti #2)
- `DELETE /api/account` `{confirm:"OBRISI"}` (sesija) → `200 {ok:true}` / `400 confirmation_required`
  (confirm mora biti točno `"OBRISI"`) / `401` (nema sesije).
  Briše `AppUser` (+ Identity role/login/token — kaskadno na razini baze) te `Favorite`, `BudgetPlan`,
  `Claim`, `UserReview` (isto kaskadno — svi imaju pravi FK ON DELETE CASCADE prema korisniku).
  Eksplicitno briše `EmailVerificationToken` (po `UserId`) i `MagicLink` (po emailu) — ta dva NEMAJU
  cascade FK. `Vendor.OwnerUserId` se postavlja na `null` za sve profile tog korisnika — **profil
  pružatelja ostaje javan** (poslovni, ne osobni podatak), samo gubi vlasnika. Odjavljuje sesiju.

## POST /api/events
First-party analitika (§A). Batch max 20, whitelist `event_name`, tihi **204**.
IP se koristi samo za rate limit (ne pohranjuje se); bez PII. Iza Vercel rewritea stvarni IP je
u `X-Forwarded-For` — čita se samo uz `Proxy:TrustForwardedFor=true` (`Infrastructure/ClientIp.cs`). `session_hash` s klijenta.
```json
[{ "name": "search_performed", "sessionHash": "ab12…", "page": "/",
   "props": { "category": "foto-i-video", "region": "dalmacija" } }]
```
Whitelist v1: page_view, search_performed, vendor_viewed, map_region_clicked,
map_pin_clicked, compare_added, compare_viewed, budget_calculated, outbound_click,
favorite_added. Rollup: `dotnet run -- --rollup [YYYY-MM-DD]` → daily_stats (dan po UTC-u).

`props` mora biti JSON **objekt** ≤ 1000 znakova (inače se odbacuje, event ostaje). Dimenzije
za `daily_stats`: `category`, `region`, `slug` (rezano na 64/32/120 znakova u rollupu).

Što klijent šalje (`lib/analytics.ts`, Zadatak C):

| Event | Kada | props |
|---|---|---|
| `page_view` | svaka promjena putanje | `region?`, `category?` (iz putanje) |
| `search_performed` | jednom po skupu filtara u načinu rezultata, kad je poznat broj | `category?`, `region?`, `q?` (očišćen), `results` |
| `vendor_viewed` | otvaranje profila | `slug`, `category`, `region` |
| `map_region_clicked` | klik na regiju na karti | `region`, `category?` |
| `map_pin_clicked` | klik na pin | `slug`, `category` |
| `compare_added` | dodavanje u usporedbu (kartica, profil, popup) | `slug`, `category` |
| `compare_viewed` | stranica usporedbe s ≥ 2, jednom po skupu | `slugs[]`, `category` |
| `budget_calculated` | "Prikaži N…" ili klik na cap u planu | `guests`, `region` |
| `outbound_click` | klik na Instagram/Facebook na profilu (šalje se odmah) | `slug`, `category`, `target` |
| `favorite_added` | dodavanje u favorite (ne uklanjanje) | `slug`, `category` |

Privatnost: `page` je samo pathname (bez query stringa); `q` je lowercase, ≤ 40 znakova i
**izostavlja se** ako sadrži `@` ili niz od 6+ znamenki; DNT/GPC → ništa se ne šalje;
bez kolačića (`credentials: "omit"`); `sessionHash` živi u sessionStorage taba.

## Claim, recenzije i admin (Faza 4, §6)

Sve rute traže sesiju (cookie). Admin rute dodatno traže rolu `admin`
(dodjela: `dotnet run -- --make-admin <email>`).

### Preuzimanje profila (provider claim)
- `POST /api/claims` `{vendorSlug, message?}` → `ClaimDto`. Kreira `pending` claim, korisniku
  dodjeljuje rolu `provider` i seeda draft iz žive verzije; 404 `vendor_not_found`,
  409 `already_claimed`. Ponovni poziv istog korisnika za istog pružatelja vraća postojeći
  (idempotentno); odbijeni se može ponovno zatražiti.
  `evidence`: `"domain_match"` kad se domena e-maila korisnika poklapa s web-domenom profila.
- `GET /api/claims/mine` → `ClaimDto[]` (svi zahtjevi korisnika).
- **`POST /api/claims/{id}/send-verification`** (2026-09 · Plan prioriteti 2, Zadatak 5) →
  `{ sentTo }` (maskirana adresa, npr. `"t***@domena.hr"` — puna `vendor.Email` se nikad ne
  vraća). Šalje jednokratni token (24h) na `vendor.Email` (interni, iz importa — jak dokaz
  vlasništva jer profil javno ne izlaže tu adresu). 404 ako claim nije korisnikov ili ne
  postoji; 409 `already_decided` ako claim više nije pending; 400 `no_email_on_file` ako
  pružatelj nema email na profilu (fallback ostaje `domain_match`/admin); 429
  `too_many_requests` (anti-zloupotreba: max 3 tokena/24h, min. 2 min razmak — vlastita provjera,
  neovisna o i strožija od opće "writes" rate-limit politike, §Zadatak 9, ispod).
- **`POST /api/claims/verify`** `{token}` (2026-09 · Zadatak 5) → `{status: "approved"|"verified"}`.
  Potvrđuje token; postavlja `evidence="email_verified"`. Ako je claim još pending **i**
  `Claims:AutoApproveOnEmailVerify` nije eksplicitno `false` (default `true`) → auto-odobrava
  (ista logika kao admin approve, `decidedBy=null`) i vraća `"approved"`; inače samo bilježi
  dokaz i vraća `"verified"` (čeka admina). 400 `invalid_token` (nepostojeći/istekao/potrošen);
  403 `not_your_claim` (token pripada tuđem claimu).

`ClaimDto`: `{ id, vendorSlug, vendorName, status: pending|approved|rejected, evidence, createdAt }`.
`evidence`: `"domain_match" | "email_verified" | ""`.

### Nadzorna ploča partnera
- `GET /api/provider/vendors` → `ProviderVendorDto[]` — pružatelji koje korisnik posjeduje ili
  za koje ima ne-odbijen claim; svaki nosi `draft`, `stats` (30 dana) i `myStatus`
  (`pending|owner|rejected`), `canPublish` (true samo za odobrenog vlasnika).
- `PUT /api/provider/vendors/{slug}/draft` `VendorDraftDto` → 204. Sprema draft (pending ili
  vlasnik); 403 ako nema pravo, 400 `invalid_price`/`invalid_price_kind`.
- `POST /api/provider/vendors/{slug}/publish` → 204. Objavljuje draft u živu verziju —
  **samo odobreni vlasnik** (pending objavu radi admin pri odobrenju claima). 403/400 `no_draft`.

`VendorDraftDto`: `{ about?, services[], price: {kind,from?,to?}, styleTags[] }`.
`ProviderStats`: `{ views30, compares30, favorites30 }` (iz `daily_stats`, §A).

### Korisničke recenzije
- `POST /api/reviews` `{vendorSlug, rating (1–5), text}` → `{status:"pending", message}`.
  Ide u moderaciju; jedna recenzija po (korisnik, pružatelj) — 409 `already_reviewed`;
  400 `own_vendor` (vlasnik ne recenzira sebe); 404 `vendor_not_found`;
  **403 `email_not_confirmed`** (2026-09 · Plan prioriteti #3 — recenzirati smije samo korisnik s
  potvrđenim emailom; throwaway/nepotvrđeni računi su blokirani i prije provjere postoji li vendor).

### Admin (rola admin)
- `GET /api/admin/claims?status=pending` → `AdminClaimDto[]`.
- `POST /api/admin/claims/{id}/approve` → objavi draft, `claim_status=claimed`, postavi vlasnika,
  ostale pending zahtjeve za istog pružatelja odbaci. `POST …/reject`.
- `GET /api/admin/reviews?status=pending` → `AdminReviewDto[]` (`status`: `pending | published | rejected`). Uz osnovna polja vraća evidenciju odluke
  (Zadatak 16): `decidedAt?`, `deciderEmail?` (izostavljen ako je račun admina obrisan), `rejectReason?`.
- `POST /api/admin/reviews/{id}/approve` (→ `published`) · `POST …/reject` s **neobaveznim** tijelom `{"reason":"…"}` (max 500 znakova → `400 reason_too_long`).
  Obje bilježe `DecidedBy`/`DecidedAt`; razlog je **interni** — ne prikazuje se javno ni autoru. Bez tijela radi kao prije.
- `GET /api/admin/vendors/{slug}/provenance` → `AdminProvenanceDto` — porijeklo podataka i privola pružatelja (Zadatak 17; **admin-interno, nikad u javnom API-ju**; `404` za nepoznat slug).
  `AdminProvenanceDto { dataSource?, dataCollectedAt?, consentStatus, consentRequestedAt?, consentAt?, consentChannel?, consentScope[], consentNote?, googlePlaceId? }`.
  Vrijednosti: `dataSource` `google_maps|web|instagram|facebook|partner|preporuka|drugo`; `consentStatus` `unknown|requested|granted|refused`; `consentChannel` `email|instagram|facebook|telefon|osobno|claim`; `consentScope` podskup od `data|photos|reviews`.
- `PUT /api/admin/vendors/{slug}/provenance` — ručna korekcija istim oblikom; **PUNA zamjena svih polja** (null/izostavljeno briše). Greške (400): `invalid_data_source`, `invalid_consent_status`, `invalid_consent_channel`,
  `invalid_consent_scope`, `note_too_long` (>1000), `place_id_too_long` (>300). `consentStatus = "refused"` uz to postavlja `OptOut = true` (odbijena privola = profil se skriva); `OptOut` se ovdje nikad ne briše (vraćanje: `restore-optout`).
  Datumi bez zone se tumače kao UTC. Izmjena ide u dnevnik promjena (bilješka o privoli bez sadržaja).
- `GET /api/admin/consent-summary` → `{ total, unknown, requested, granted, refused }` — brojevi pružatelja po statusu privole (praćenje kampanje kontaktiranja).
- **Claim = privola (Zadatak 17):** odobrenje claima (ručno ili auto nakon potvrde e-maila) postavlja `consentStatus = granted`, `consentChannel = claim`, `consentAt = sada`, `consentScope = [data, photos, reviews]` —
  osim ako je privola već `refused` (ne poništava se tiho). Nijedno od ovih polja ne ulazi u javni `VendorDto` ni u `data/vendors.json`.
- `GET /api/admin/photos?status=unreviewed&limit=60` → `AdminPhotoDto[]` — fotografije pružatelja za pregled (`status`: `unreviewed | approved | flagged`, inače `400 invalid_status`;
  `limit` 1–200). Poredak: najstarije prvo, slike bez datuma (iz vremena prije evidencije) na kraj.
  `AdminPhotoDto { id, vendorSlug, vendorName, url, thumbUrl, isCover, source (partner|import), moderationStatus, createdAt?, rightsConfirmedAt?, uploaderEmail?, reviewedAt?, reviewerEmail?, moderationNote? }`.
- `POST /api/admin/photos/{id}/approve` (`unreviewed → approved`, samo evidencija) · `POST …/flag` tijelo `{"note":"…"}` **obavezno** (`400 note_required`, max 1000 → `400 note_too_long`) → sakriva sliku;
  razlog vidi vlasnik profila, a ako je profil preuzet, dobiva i e-mail (best-effort) · `POST …/unflag` (`flagged → approved`, briše napomenu).
  Dozvoljeni prijelazi: `unreviewed→approved|flagged`, `approved→flagged`, `flagged→approved`; sve ostalo `409 invalid_transition`. Svaka akcija bilježi `reviewedBy`/`reviewedAt`.
- `POST /api/admin/photos/approve-batch` tijelo `{"ids":["…"]}` (max 60 → `400 too_many_ids`; neispravan id → `400 invalid_ids`) → `{approved, skipped}`; odobrava samo još `unreviewed`, ostale preskače.
- `GET /api/admin/imported-reviews?status=unverified&limit=100` → `AdminImportedReviewDto[]` — uvezene recenzije ("što oni kažu") za provjeru
  (`status`: `unverified | verified | rejected`, inače `400 invalid_status`; `limit` 1–500). `AdminImportedReviewDto { id, vendorSlug, vendorName, author, rating, text, source, year, verificationStatus, verifiedAt?, verifierEmail?, evidenceNote? }`.
- `POST /api/admin/imported-reviews/{id}/verify` · `POST …/reject` — tijelo (neobavezno) `{"evidenceNote":"…"}` (max 1000 → `400 note_too_long`; izostavljeno = ostaje prijašnja napomena,
  `""` = briše napomenu). Postavljaju `VerificationStatus`, `VerifiedByUserId`, `VerifiedAt`; admin smije promijeniti odluku (verified ↔ rejected). Odluka **preživljava ponovni Excel uvoz**.
- `POST /api/admin/vendors/{slug}/unpublish` · `POST …/publish` → toggla `is_published` (§9).
- `GET /api/admin/audit?slug=&entityType=&limit=100` → `AdminAuditEntryDto[]` — dnevnik promjena (GDPR, Zadatak 14), najnovije prvo,
  `limit` 1–500 (default 100). Sa `slug`: zapisi o tom pružatelju + o njegovim slikama, recenzijama i claimovima (uključujući
  OBRISANE — podređeni zapisi nose `note = "vendorId:<guid>"`); nepoznat slug → `404`. Bez `slug`: zadnji zapisi svih entiteta.
  `entityType` (`vendor | vendor_photo | imported_review | user_review | claim | user`) dodatno sužava.
  `AdminAuditEntryDto { id, occurredAt, actorType (admin|partner|user|public|import|system), actorEmail?, entityType, entityId, action, changes?, source? }`.
  `action`: `create | update | delete | optout | optout_restored | account_deleted | owner_unlinked`.
  `changes` je **JSON kao tekst**: izmjena `{"PriceFrom":{"old":800,"new":950}}`, maskirano polje `{"Phone":{"changed":true}}`,
  novi entitet `{"Name":"…","Phone":{"set":true}}` (nazivi = C# imena svojstava). Kontakti pružatelja, tekstovi recenzija i poruka uz claim
  bilježe se **bez vrijednosti**; dugi tekstovi skraćeni na 1000 znakova. `actorEmail` je izostavljen za javne/sistemske radnje i za obrisane korisnike.

**Obavijesti partnerima** (2026-09 · Plan prioriteti 2, Zadatak 7): `approve claim`, `reject claim`
i `approve review` (samo ako je profil claiman, tj. `vendor.OwnerUserId` postoji) šalju vlasniku
HR mail nakon uspješnog spremanja (`PartnerEmails`, isti `IEmailSender` kao auth mailovi — dev
konzola bez Resend ključa). Slanje je **best-effort**: pad (npr. neispravan Resend ključ) se samo
logira, admin akcija i dalje vraća 200 — odgovor korisniku ovih endpointa se ne mijenja.

Napomena: objavljene korisničke recenzije zasad NE mijenjaju `vendor.rating`/`reviewCount`
(oni ostaju iz importa). Stapanje ocjena je zasebna odluka (v. PLAN §11 #19).

## Napomena uz Zadatak 13 (shema za audit, moderaciju i privolu)
Migracija `AuditIModeracija` dodaje stupce i tablicu `audit_log` isključivo **interno**: **nijedan javni DTO ni ruta se ne mijenja**
(`VendorDto`, `VendorProfileDto`, `ImportedReviewDto`, `ProviderPhotoDto`, `AdminReviewDto`… ostaju isti). Polja o porijeklu i privoli
pružatelja (`consent_*`, `data_source`, `google_place_id`) nikad ne ulaze u javni API. Nove rute i polja u DTO-ovima dolaze tek
u Zadacima 14–17 i bit će opisani ovdje kad se uvedu.

## Kasnije (Coming soon)
- `GET /api/vendors/{id}/availability?month=YYYY-MM` → `{ "days": { "2026-09-05": "free|busy" } }` — do tada frontend koristi deterministički mock iz `lib/availability.ts` (ista logika na profilu i u usporedbi)
- Fotografije u draftu (Faza 5, R2), premium mogućnosti iz `subscriptions` (§M.1)

---

## Faza 5 — fotografije pružatelja + health

Sve rute fotografija traže prijavu i **odobrenog vlasnika** (claimed + owner), inače `403`.
Bazna ruta: `/api/provider/vendors/{slug}/photos`.

| Metoda | Ruta | Tijelo | Odgovor |
|---|---|---|---|
| POST | `…/photos` | multipart: polje `file` (slika, ≤10 MB) + **obavezno** `rightsConfirmed=true` | `200 ProviderPhoto` |
| DELETE | `…/photos/{id}` | — | `204` |
| PUT | `…/photos/order` | `{ orderedIds: string[], coverId: string\|null }` | `204` |

`ProviderPhoto = { id, url, thumbUrl, isCover, sortOrder, moderationStatus, moderationNote? }` (Zadatak 15). `url`/`thumbUrl` su apsolutni
(R2/CDN u produkciji, `/uploads/…` u dev-u). Prva uploadana fotografija automatski je naslovna.
Greške (400): `rights_not_confirmed` (nije poslano `rightsConfirmed=true` — provjera je PRIJE obrade slike), `no_file`, `file_too_large`, `not_an_image`, `invalid_image`, `too_many_photos`.

**Post-moderacija (Zadatak 15):** uploadana slika je **javna odmah** sa statusom `unreviewed`; admin naknadno vodi evidenciju. `moderationStatus`:
`unreviewed` | `approved` (pregledano, u redu) | `flagged` (skriveno). Server pri uploadu bilježi `rightsConfirmedAt` (potvrda da partner ima pravo objaviti fotografiju), `uploadedByUserId`, `createdAt`.
`flagged` slika se **ne vraća** u javnom API-ju (`/api/vendors`, `/api/vendors/{slug}`, `/api/pins`); naslovna je prva javna po `sortOrder`, pa kad je skrivena naslovna, javno se vidi sljedeća.
Vlasnik je i dalje vidi u `GET /api/provider/vendors` s `moderationStatus: "flagged"` i razlogom `moderationNote`; može je ukloniti. `unreviewed`/`approved` se vlasniku ne prikazuju kao razlika.

Upload prolazi kroz obradu: auto-orijentacija → smanjivanje (glavna ≤1600px, thumb ≤400px) →
WebP → opcionalni tekstualni žig. `GET /api/provider/vendors` sada vraća i `photos: ProviderPhoto[]`.

**Health:** `GET /api/health` (bez autentikacije) → `200 {status:"ok"}` / `503 {status:"db_down"}`.

**Rate limiting** (2026-09 · Plan prioriteti 2, Zadatak 9 — očvršćeno): globalno 300/min po IP-u;
liste (`/api/vendors`, `/api/pins`, `/api/suggest`) 60/min; **pisanja** (`POST /api/reviews`,
`/api/claims`, `/api/claims/{id}/send-verification`, `/api/claims/verify`, `/api/optout`) 20/min,
sliding-window, particija po **korisniku** ako je prijavljen (inače po IP-u); **auth**
(`/api/auth/*` osim `logout`) 10/min, sliding-window, particija po IP-u — komplementarno Identity
lockoutu (8 promašaja/15 min). Svaki `429` nosi `Retry-After` zaglavlje.

---

## Faza 6 — GDPR opt-out (§9)

| Metoda | Ruta | Auth | Tijelo | Odgovor |
|---|---|---|---|---|
| POST | `/api/optout` | javno (rate-limited) | `{ slug, reason?, contact? }` | `200 {ok:true}` / `404 vendor_not_found` |
| GET | `/api/admin/optouts` | admin | — | `AdminOptOut[]` |
| POST | `/api/admin/vendors/{slug}/restore-optout` | admin | — | `200 {ok:true}` |

`POST /api/optout` postavlja `Vendor.OptOut = true` **odmah** → `.Published()` filtar isključuje profil
iz svih javnih upita (lista, profil, karta, sitemap, budget-matches). Reverzibilno preko admina.
Razlog/kontakt se **ne perzistiraju** (minimizacija podataka) — bilježe se u aplikacijski log.
`AdminOptOut = { slug, name, category, isPublished }`.
