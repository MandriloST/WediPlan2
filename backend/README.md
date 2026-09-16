# wediplan backend — ASP.NET Core 8 Web API

Implementira ugovor iz `../API.md` (oblici odgovora identični Next.js mocku u `app/api/*`).
Arhitektura i faze: `../PLAN-ARHITEKTURA.md`. Stanje rada: `../STANJE.md`.

## Struktura
```
backend/
├─ Wediplan.sln
├─ docker-compose.yml        # api (:5080) + postgres 16 (:5433 na hostu)
└─ Wediplan.Api/
   ├─ Program.cs             # DI, JSON politika (camelCase, bez null polja), CORS
   ├─ Contracts/             # DTO-ovi — 1:1 zrcalo API.md / lib/types.ts
   ├─ Controllers/           # VendorsController, HealthController
   ├─ Data/AppDbContext.cs   # EF Core (entiteti dolaze u Fazi 1)
   └─ Dockerfile
```

## Pokretanje — Docker (preporučeno)
```bash
cd backend
docker compose up --build
```
- API: http://localhost:5080 · Postgres: `localhost:5433` (db/user/pass: `wediplan`)

## Pokretanje — bez Dockera
Treba .NET 8 SDK + Postgres (prilagodi `ConnectionStrings:Default` u `appsettings.json`;
API radi i bez baze — `/health` tada javlja `db: unavailable`).
```bash
cd backend/Wediplan.Api
dotnet run   # http://localhost:5080 (launchSettings)
```

## Provjera (Definition of done Faze 0)
```bash
curl http://localhost:5080/health
# → {"status":"ok","db":"ok","version":"1.0.0"}

curl "http://localhost:5080/api/vendors"
# → {"items":[],"total":0,"page":1,"pageSize":24}

curl "http://localhost:5080/api/vendors?page=2&pageSize=999"
# → {"items":[],"total":0,"page":2,"pageSize":50}   (pageSize clamp ≤ 50)
```

## Konvencije
- JSON: camelCase, null polja se izostavljaju (postavljeno globalno u `Program.cs`) —
  ne mijenjati, frontend ovisi o tome.
- `pageSize ≤ 50` — anti-scraping pravilo, ne dizati.
- Commit poruke hrvatski, `feat:`/`fix:` prefiks; rad na `develop`.

---

## Faza 1 — B1 (model + read endpointi) — dodano 2026-09-14

Entiteti (`Domain/Entities.cs`), EF konfiguracija (`Data/AppDbContext.cs`), šifrarnik
kategorija/regija/budžeta (`Data/Catalog.cs`, vjeran port `lib/data.ts`/`lib/budget.ts`),
mapper bez kontakata (`Data/VendorMapper.cs`) i kontroleri:
`/api/vendors` (filtri q/region+coverage/category-M2M/page, pageSize 24 cap 50, sort
rating→reviewCount→id), `/api/vendors/{slug}`, `/api/categories?region=` (brojači po svim
kategorijama), `/api/regions`, `/api/suggest` (šifrarnik + pg_trgm), `/api/budget-defaults`.
Kontakti (telefon/email/web) se uvoze u bazu ali se NE vraćaju u API (§8-§9).

### Kreiranje baze i migracija (na vašem stroju — treba .NET 8 SDK + Postgres)
```bash
# 1) digni Postgres (compose iz backend/ digne ga na :5433)
docker compose up -d postgres

# 2) EF alat (jednom)
dotnet tool install -g dotnet-ef            # ako već nije

# 3) generiraj i primijeni migraciju
cd Wediplan.Api
export WEDIPLAN_DB="Host=localhost;Port=5433;Database=wediplan;Username=wediplan;Password=wediplan"
dotnet ef migrations add InitFaza1
dotnet ef database update

# 4) pokreni API
dotnet run   # http://localhost:5080
```
Alternativa bez EF-a (brzi pregled/test sheme): `db/schema.sql` je referentna shema koja
zrcali EF model (učitaj s `psql -f db/schema.sql`). Izvor istine ostaje EF migracija —
usporedite generiranu migraciju sa `db/schema.sql`.

### Provjera (DoD B1) — baza je prazna dok ne odradite import (B2)
```bash
curl "http://localhost:5080/api/vendors?category=foto-i-video&region=dalmacija&pageSize=24"
#   → {"items":[...bez telefona/emaila...],"total":N,"page":1,"pageSize":24}
curl "http://localhost:5080/api/categories?region=dalmacija"   # brojači po kategorijama
curl "http://localhost:5080/api/regions"                        # brojači po regijama
curl "http://localhost:5080/api/suggest?q=foto"                 # typeahead
curl "http://localhost:5080/api/vendors/<slug>"                 # profil
```

⚠️ **Verifikacija u sandboxu:** `.NET build`/`dotnet ef` se NE mogu izvršiti u okruženju u
kojem je kod pisan (nema pristupa nuget.org). Umjesto toga, cijela shema i SVI upiti koje
kontroleri generiraju (M2M brojači, coverage filter, q ILIKE, sort/paginacija, pg_trgm
typeahead, tsvector, oblik VendorDto JSON-a) verificirani su nad pravim Postgresom 16 i
uzorkom od 80 stvarnih pružatelja. `dotnet build`, migracija i pokretanje su na vama.

---

## Faza 1 — B2 (import + analitika) — dodano 2026-09-14

**Import komanda** (`Import/`): čita Excel (ClosedXML), primjenjuje pravila iz
`scripts/import-vendors.mjs` (norm/slugify/kategorije/regije/coverage/precision/cijena/
social/dodatne kategorije), **otporan na greške** (uveze valjane, preskoči neispravne uz
`import-report.txt`), **idempotentan po slugu**, geokodira gradove bez koordinata
(Nominatim, ~1/s + trajni cache `geocode-cache.json`). Kontakti se uvoze u bazu ali se ne
izlažu u API.

```bash
cd backend/Wediplan.Api
export WEDIPLAN_DB="Host=localhost;Port=5433;Database=wediplan;Username=wediplan;Password=wediplan"

# 1) NAJPRIJE dry-run — vidi što će se uvesti/preskočiti, bez pisanja i bez mreže:
dotnet run -- --import /put/do/vendors-live.xlsx --dry-run

# 2) pravi uvoz (geokodira ~662 grada, ~11 min prvi put, poslije cache):
dotnet run -- --import /put/do/vendors-live.xlsx
#   (bez geokodiranja: dodaj --no-geocode)
```
⚠️ U `Import/Geocoder.cs` zamijenite kontakt-email u User-Agentu (Nominatim politika).

**Analitika (§A):** `POST /api/events` (batch ≤20, whitelist, tihi 204, IP se ne pohranjuje).
Dnevni rollup: `dotnet run -- --rollup [YYYY-MM-DD]` (default: jučer) → `daily_stats`.
Postavi noćni cron/systemd timer da ga zove.

### Verifikacija B2 (sandbox)
`.NET`/Nominatim se NE mogu pokrenuti u okruženju gdje je kod pisan (nema nuget/mreže).
Umjesto toga pravila importa portana su i puštena na CIJELI stvarni Excel (3178), rezultat
(3091 uvezeno, 82 preskočeno) učitan u pravi Postgres 16 i provjereni SVI read-upiti
(kategorije, category+coverage, regije, suggest, M2M spajanje) te rollup. `dotnet build`,
geokodiranje i pokretanje su na vama; `--dry-run` daje isti izvještaj na vašem stroju.
