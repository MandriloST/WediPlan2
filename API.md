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
```json
{ "vendor": {}, "about": "…", "services": ["…"],
  "importedReviews": [{ "author": "Marija i Ivan", "rating": 5, "text": "…", "source": "Google recenzije", "year": 2025 }] }
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

## Kasnije (Coming soon)
- `GET /api/vendors/{id}/availability?month=YYYY-MM` → `{ "days": { "2026-09-05": "free|busy" } }` — do tada frontend koristi deterministički mock iz `lib/availability.ts` (ista logika na profilu i u usporedbi)
- `POST /api/plan` (sync plana uz auth), `POST /api/reviews` (registrirani korisnici)
