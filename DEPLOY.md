# Deploy na Vercel — vodič

## Prije prvog deploya (lokalna provjera, ~5 min)

```bash
npm run import:vendors -- putanja/do/tvog.xlsx   # ako mijenjaš podatke
npm run scaffold:images && npm run sync:images    # ako dodaješ slike
npm run build                                     # MORA proći bez grešaka
npm start                                         # http://localhost:3000 — klikni kroz stranicu
```

Provjeri prije pusha: `git status` ne smije pokazivati `.next/` datoteke, a slike i `data/*.json` moraju biti commitani (Vercel builda iz gita — što nije u gitu, ne postoji na produkciji).

## Prvi deploy (~10 min)

1. **vercel.com** → Sign up with GitHub (besplatan Hobby plan je dovoljan za start).
2. **Add New… → Project** → Import `MandriloST/WediPlan2`.
3. Vercel sam prepozna Next.js — **ništa ne mijenjaj** (build command, output, install su automatski). Env varijable za sada nisu potrebne.
4. **Deploy.** Prvi build traje ~2 min. Dobivaš URL oblika `wediplan-xxxx.vercel.app`.

## Workflow grana (preporuka)

- **Production branch: `main`** (Vercel default). Svaki push/merge u `main` = nova produkcija.
- Svaki push na **`develop`** automatski dobiva **Preview URL** — savršeno za pokazati promjenu prije nego ode uživo.
- Tok: radiš na `develop` → provjeriš preview → `git checkout main && git merge develop && git push` → produkcija.

## Nakon prvog deploya — checklista

- [ ] Karta se učitava i pinovi rade (klik na regiju, popup)
- [ ] Slike pružatelja vidljive (kartice + profili)
- [ ] `https://<url>/sitemap.xml` i `/robots.txt` rade
- [ ] Mobitel: donja navigacija, "Dodaj na početni zaslon" (PWA install)
- [ ] Usporedba i budžet rade (localStorage)
- [ ] U Vercel projekt → **Settings → Environment Variables** dodaj `NEXT_PUBLIC_SITE_URL` = tvoj konačni URL (ili kasnije domena) → Redeploy. (Do tada se koristi VERCEL_URL — radi, ali sitemap će pokazivati privremeni URL.)

## Vlastita domena (kad kupiš, npr. wediplan.hr)

Vercel projekt → Settings → Domains → Add → slijedi DNS upute (A/CNAME zapis kod registrara). SSL je automatski. Zatim ažuriraj `NEXT_PUBLIC_SITE_URL`.

## Faza 2 — spajanje na .NET API (env `API_URL`)

| Okruženje | `API_URL` | Rezultat |
|---|---|---|
| Lokalno bez backenda | nije postavljen | mock rute (`app/api/*`) nad `data/vendors.json` |
| Lokalno s backendom | `http://localhost:5080` u `.env.local` | pravi podaci iz Postgresa |
| Vercel preview/produkcija | **tek kad je backend hostan** (odluka #1), npr. `https://api.wediplan.hr` | pravi podaci |

- `API_URL` je server-only (NIJE `NEXT_PUBLIC_`) i čita se **pri buildu** (rewrites) i pri
  izvođenju (server komponente) → nakon promjene u Vercelu napravi **Redeploy**.
- Build ne ovisi o dostupnosti API-ja: profili se renderiraju na prvi zahtjev (ISR 5 min),
  sitemap bez API-ja sadrži samo kategorije/regije.
- Dok backend nije hostan, **ne postavljaj** `API_URL` na Vercelu — preview ostaje na mocku.

## Couple podaci (favoriti/plan) — sinkronizacija (kraj Faze 3)

Prijavljeni korisnik: favoriti i budžetski plan žive u bazi (`favorites`, `budget_plans`).
Gost: sve u localStorage (kao dosad). Pri prijavi frontend (`lib/sync.ts`) POST-a
`/api/favorites/merge` — spaja lokalno u account (unija favorita; plan se ne pregazi). Nakon
toga `components/AccountSync.tsx` mirrora svaku promjenu na server, a pri boot-u (refresh)
učita server-stanje. Nema dodatne konfiguracije — radi čim je auth postavljen i migracija primijenjena.

## Auth (Faza 3) — konfiguracija

Nakon migracije (`dotnet ef database update`) auth radi ODMAH s dva načina (email+lozinka,
magic link). Bez Resend ključa e-mailovi (magic/verify/reset linkovi) ispisuju se u KONZOLU
servera (dev) — dovoljno za lokalni test cijelog toka.

Konfiguracija (appsettings ili env; env NADJAČAVA i NIKAD ne ide u git):
```
App__PublicUrl        = http://localhost:3000     (frontend; odredište linkova u mailovima)
Email__ResendApiKey   = re_...                     (prazno → dev konzola)
Email__From           = Wediplan <no-reply@tvoja-domena>
Google__ClientId      = ...apps.googleusercontent.com   (prazno → Google gumb skriven)
Google__ClientSecret  = ...
Auth__CookieDomain    = .wediplan.hr               (samo produkcija; dev prazno)
```

**Resend:** registriraj se na resend.com, verificiraj domenu (ili koristi test `onboarding@resend.dev`),
kreiraj API ključ → `Email__ResendApiKey`. Bez toga sve radi, samo se mailovi ispisuju u konzolu.

**Google OAuth:** Google Cloud Console → OAuth consent + Credentials → OAuth client (Web).
Authorized redirect URI: `http://localhost:5080/signin-google` (dev) i
`https://api.wediplan.hr/signin-google` (prod). Client ID/Secret → env. Dok ovo ne postaviš,
prijava Googleom je skrivena, ostala dva načina rade.

**Cookie:** dev radi na localhost bez ičega. Produkcija: frontend i API na istoj baznoj domeni
(`wediplan.hr` + `api.wediplan.hr`), postavi `Auth__CookieDomain=.wediplan.hr`, oboje preko HTTPS.

## Faza 4 (claim + admin + korisničke recenzije) — konfiguracija i test

**1. Migracija** (nove tablice: `claims`, `user_reviews`, `vendor_drafts`, `subscriptions`):
```bash
cd backend/Wediplan.Api
dotnet ef migrations add Faza4     # generira se iz izmijenjenog modela (AppDbContext)
dotnet ef database update
```
(Sandbox nema NuGet pa migracija nije generirana ondje — pokreće se ovdje, kao i za Faze 1/3.)

**2. Admin** (jednokratno, nakon što se registriraš u aplikaciji tim e-mailom):
```bash
dotnet run -- --make-admin tvoj-email@primjer.hr
```
Zatim se **odjavi i ponovno prijavi** da rola `admin` uđe u sesiju (cookie). Admin panel: `/admin`.

**3. Ručni test cijelog toka (DoD):**
- Registriraj korisnika A (e-mail+lozinka), potvrdi e-mail (link u konzoli servera ako nema Resenda).
- Otvori bilo koji profil `/pruzatelj/<slug>` → “Ovo je moj profil — preuzmi ga” → pošalji zahtjev.
  Korisnik A dobiva rolu `provider` i pristup `/partner` (uređivanje **skice** — nije još javno).
- U `/partner` uredi opis/cijenu/usluge → “Spremi skicu”.
- Kao **admin** otvori `/admin` → “Zahtjevi za preuzimanje” → **Odobri**. Profil je sad `claimed`,
  skica je objavljena, korisnik A je vlasnik (može “Spremi i objavi” izravno).
- Registriraj korisnika B → na istom profilu “Napiši recenziju” (zvjezdice + tekst) → šalje se u moderaciju.
- Kao admin `/admin` → “Recenzije za provjeru” → **Objavi**. Recenzija se pojavljuje na profilu
  (“Wediplan recenzije”). Time je DoD Faze 4 ispunjen.

**Napomena:** objavljene korisničke recenzije zasad **ne** mijenjaju ocjenu/broj recenzija na kartici
(oni ostaju iz importa) — prikazuju se zasebno. Stapanje je zasebna odluka (PLAN §11 #19).

**Bez backenda (mock):** claim/recenzije/admin traže .NET (kao i auth) — u čistom mock načinu
korisnik nije prijavljen pa se te akcije ni ne nude; landing/karta/usporedba/budžet rade kao i dosad.

## Geokodiranje pri importu (koordinate gradova)

Import geokodira grad → koordinate preko Nominatima (OpenStreetMap). Zahtijeva izlaz na
`https://nominatim.openstreetmap.org` (rate limit 1 req/s; User-Agent je već postavljen —
zamijeni kontakt e-mail u `Import/Geocoder.cs`).

**Popravak 2026-09-16:** raniji upit je koristio naše interne "regije" ("Dalmacija",
"Zagreb i okolica", "Kvarner"), koje OSM ne poznaje, pa je za Split/Zagreb/Rijeku i sve gradove
tih regija vraćao prazno i trajno keširao kao `null`. Sada se regija preslikava u SLUŽBENU
županiju (Split → Splitsko-dalmatinska županija), uz fallback na "grad, Hrvatska" i strukturirani
`city=` upit. Složeni nazivi ("Split / Zagreb", "Zagreb (Sesvete)") se čiste na prvi grad.

**Ako ti pinovi za Split/Zagreb ne rade nakon update-a:** stari `geocode-cache.json` je te
gradove imao spremljene kao `null`. Null-ovi su u ovom commitu uklonjeni iz cachea, pa ih sljedeći
import pokušava ponovno. Ako radiš sa svojim starijim cacheom, pokreni jednom:
```
dotnet run -- --import data/vendors-live.xlsx --geocode-retry
```
`--geocode-retry` ponovno pokušava SAMO ključeve koji su prije bili `null` (pozitivni pogodci se
ne diraju, mreža se štedi). Prvi import ~3200 gradova traje (1 req/s + fallback upiti); rezultati
se keširaju pa su idući importi brzi.

## Analitika (Zadatak C)

- Bez `API_URL` eventi idu u mock rutu i odbacuju se (204). Za pregled što se šalje:
  `ANALYTICS_DEBUG=1 npm run dev` → svaki batch se ispisuje u terminal kao `[events] …`.
- S backendom eventi idu u tablicu `events`; dnevni agregat: `dotnet run -- --rollup`
  (bez datuma = jučer, UTC). Na serveru ga pokreni noćnim cronom/systemd timerom.
- Uključen Do Not Track / Global Privacy Control u pregledniku → ništa se ne šalje
  (to je namjerno; za lokalni test isključi DNT).
- Service worker je podignut na **v2**: API odgovori se više ne cachiraju (osim šifrarnika
  za offline). Kod postojećih posjetitelja stari cache se briše sam pri aktivaciji.

## Potencijalni problemi i rješenja

| Problem | Uzrok | Rješenje |
|---|---|---|
| Slika radi lokalno (Windows), 404 na Vercelu | Linux je case-sensitive: folder `Villa-Lav` ≠ slug `villa-lav` | Imena foldera moraju biti točno slug; `npm run sync:images` prijavljuje krivo nazvane kao "siročad" — ne ignoriraj to upozorenje |
| Build padne: "lockfile out of sync" | `package.json` mijenjan bez `npm install` | Lokalno `npm install`, commitaj `package-lock.json` |
| Stara verzija stranice nakon deploya | Service worker cache kod korisnika | SW je network-first pa se rješava sam na sljedeći posjet; kod sebe: DevTools → Application → Service Workers → Update/Unregister. Ako mijenjaš `public/sw.js`, digni verziju (`wediplan-shell-v2`) |
| Karta spora ili tile-ovi ne rade | Javni OSM server je best-effort i nije za komercijalnu produkciju | Za demo OK. Za produkciju: MapTiler (free tier 100k tileova/mj) → env `NEXT_PUBLIC_TILE_URL=https://api.maptiler.com/maps/streets-v2/{z}/{x}/{y}.png?key=KLJUČ` |
| Upozorenje o broju optimiziranih slika | Hobby plan: 1000 izvornih slika | Sada nebitno (~25 slika). Kod ~300 vendora: Vercel Pro ili Bunny Optimizer (vidi bilješke o skaliranju) |
| Font warning u build logu | Kozmetika (inline optimizacija fontova) | Ignorirati — na Vercelu obično nestane jer ima mrežu |
| Stranice rade, ali podaci su stari/mock | `API_URL` nije postavljen ili nije napravljen redeploy | Postavi env pa Redeploy (rewrites se čitaju pri buildu) |
| Liste prazne, u logu `API 5xx` / `fetch failed` | .NET nedostupan s Vercela | Provjeri `https://<api>/health`; firewall mora pustiti Vercel |
| Push na GitHub ne pokreće deploy | GitHub integracija | Vercel → Settings → Git → provjeri da je repo povezan |

## Što NE treba raditi

- Ne postavljaj `output: 'export'` — API rute i on-demand stranice trebaju server.
- Ne dodavaj `vercel.json` — defaulti su ispravni za ovaj projekt.
- Ne commitaj `.env` datoteke (za sada ih ni nemamo; tajne idu u Vercel env UI).

---

## Faza 5 — slike + produkcijsko očvršćivanje (2026-09-17)

### 1. Pohrana slika (odluka #3 — Cloudflare R2)
Backend bira pohranu prema konfiguraciji: ako je `Storage:R2:Bucket` postavljen → R2 (S3 API),
inače lokalno (`wwwroot/uploads`, servira se na `/uploads`). Dev radi bez ikakve konfiguracije.

Env / `appsettings` (produkcija):
```
Storage__PublicBaseUrl   = https://cdn.wediplan.hr      (javna R2/CDN domena, bez završnog /)
Storage__R2__Bucket      = wediplan-media
Storage__R2__AccountId   = <cloudflare-account-id>       (ili Storage__R2__ServiceUrl za pun endpoint)
Storage__R2__AccessKey   = <r2-access-key>
Storage__R2__SecretKey   = <r2-secret-key>
# opcionalni žig na slikama:
Storage__WatermarkText     = WediPlan
Storage__WatermarkFontPath = /opt/wediplan/fonts/Inter-Bold.ttf   (ako nije zadano → bez žiga)
```
Frontend (Vercel): `NEXT_PUBLIC_UPLOADS_BASE=https://cdn.wediplan.hr` (da `next/image` dopusti domenu).

R2 postavljanje: kreiraj bucket → poveži javnu domenu (R2 public bucket ili custom domenu
`cdn.wediplan.hr` preko Cloudflarea) → generiraj S3 API token (Access/Secret). Objekti se pišu
pod ključem `vendors/{vendorId}/{guid}.webp` (+ `_thumb.webp`).

> **Licenca:** obrada slika koristi **SixLabors ImageSharp** (Six Labors Split License —
> besplatno za OSS/male; komercijalna licenca iznad praga prihoda). Provjeriti prije
> komercijalnog lansiranja; alternativa je Magick.NET (Apache 2.0) ako se poželi zamijeniti.

### 2. Rate limiting (§8)
Ugrađeni `Microsoft.AspNetCore.RateLimiting`: globalno 300/min po IP-u, liste (`/api/vendors`,
`/api/pins`, `/api/suggest`) 60/min. Iza Cloudflarea/Vercela postavi `Proxy__TrustForwardedFor=true`
(odluka #17) da limiter vidi stvarni IP iz `X-Forwarded-For`. **Ne uključuj** dok API nije dostupan
isključivo preko proxyja — inače se IP može lažirati.

### 3. Cloudflare ispred API-ja
Usmjeri `api.wediplan.hr` preko Cloudflarea (proxy ON): bot fight mode, rate limiting pravila,
opcionalno JS challenge na `/api/vendors`. Tek tada `Proxy__TrustForwardedFor=true`.

### 4. Backup baze i slika, restore i provjera (Zadatak 12)

Tri skripte u `ops/` (sve `set -euo pipefail`, provjerene `shellcheck`-om i stvarno pokrenute nad Postgresom 16):

| Skripta | Što radi | Kad |
|---|---|---|
| `ops/backup.sh` | `pg_dump -Fc` → (age enkripcija) → (rclone kopija izvan servera) → lokalna rotacija | dnevno, 03:00 |
| `ops/backup-photos.sh` | slike (R2 ili lokalni folder) → odvojeni remote; obrisano/prepisano ide u arhivu po datumu | dnevno, 03:30 |
| `ops/restore-test.sh` | vraća backup u **privremenu** bazu, provjeri sadržaj, obriše je | **prije svake migracije** + barem mjesečno (v. „Restore“) |

```
0 3  * * *  . /etc/wediplan-backup.env; /opt/wediplan/ops/backup.sh        >> /var/log/wediplan-backup.log 2>&1
30 3 * * *  . /etc/wediplan-backup.env; /opt/wediplan/ops/backup-photos.sh >> /var/log/wediplan-photos-backup.log 2>&1
```
Skripte **izlaze s kodom ≠ 0** kad nešto padne (backup.sh: `1` = dump/konfiguracija, `2` = lokalni backup OK ali upload nije).
Zato cron treba javljati greške (npr. `MAILTO=` ili `chronic`/healthchecks.io ping nakon uspješnog prolaza).

#### Instalacija na serveru
```bash
apt install postgresql-client age rclone      # pg_dump mora biti iste ili novije verzije od Postgres servera (16)
```
Ako Postgres radi u Dockeru na istom serveru: u `docker-compose` objavi port samo na localhost (`127.0.0.1:5432:5432`)
i u env-u postavi `PGHOST=127.0.0.1` — **nikad** ne izlaži Postgres na javni IP.

#### Env datoteka `/etc/wediplan-backup.env` (`chmod 600`, vlasnik root)
```bash
set -a
# veza na bazu — libpq format (NE Npgsql "Host=…;Port=…"):
WEDIPLAN_DB="postgresql://wediplan_backup:LOZINKA@127.0.0.1:5432/wediplan"   # ili PGHOST/PGUSER/PGPASSWORD/PGDATABASE
BACKUP_DIR=/var/backups/wediplan
KEEP_DAYS=14
BACKUP_AGE_RECIPIENT="age1…radni age1…rezervni"      # javni ključevi (razmak ili zarez)
BACKUP_RCLONE_REMOTE=r2-backup:wediplan-backups/db
PHOTOS_MODE=r2                                       # ili: local
PHOTOS_R2_REMOTE=r2-media:wediplan-media             # mode=r2: izvor (bucket iz Storage__R2__Bucket)
PHOTOS_LOCAL_DIR=/opt/wediplan/api/wwwroot/uploads   # mode=local: izvor
PHOTOS_BACKUP_REMOTE=r2-backup:wediplan-backups/photos
set +a
```
Preporuka: za backup napravi posebnog DB korisnika samo s pravom čitanja (`GRANT pg_read_all_data TO wediplan_backup;`,
Postgres ≥ 14) — `pg_dump` mu je dovoljan, a ne može mijenjati podatke.

#### age ključevi (enkripcija — backup sadrži osobne podatke)
```bash
# NA SVOM RAČUNALU, ne na serveru:
age-keygen -o wediplan-backup-radni.key      # ispiše "Public key: age1…"  → to ide u BACKUP_AGE_RECIPIENT
age-keygen -o wediplan-backup-rezervni.key   # drugi ključ (npr. u sefu / kod drugog povjerljivog čovjeka)
```
- **Privatni ključ (`*.key`) nikad na produkcijski server** — na serveru je samo javni ključ; tko provali server ne može čitati stare backupe.
- Privatni ključ čuvaj u password manageru **i** offline kopiju. **Izgubiš li sve privatne ključeve, backupi su nečitljivi.**
- Dva ključa u `BACKUP_AGE_RECIPIENT`: bilo koji od njih otključava backup.
- Bez `BACKUP_AGE_RECIPIENT` skripta radi, ali ispiše upozorenje i backup **nije** enkriptiran.

#### rclone + Cloudflare R2 (EU)
1. Kreiraj **zaseban bucket za backupe** (npr. `wediplan-backups`) s **EU jurisdikcijom** (Cloudflare → R2 → Create bucket →
   Jurisdiction: European Union). Jurisdikcija se poslije **ne može promijeniti**. Ne koristi isti bucket kao za slike.
2. Napravi **zaseban API token** (R2 → Manage API tokens → *Object Read & Write*) ograničen **samo na backup bucket** — ne koristi
   aplikacijski token (`Storage__R2__*`).
3. `rclone config` → novi remote (ili ručno u `~/.config/rclone/rclone.conf`; pokreće se kao korisnik koji izvršava cron):
```ini
[r2-backup]
type = s3
provider = Cloudflare
access_key_id = <ACCESS_KEY_ID>
secret_access_key = <SECRET_ACCESS_KEY>
endpoint = https://<ACCOUNT_ID>.eu.r2.cloudflarestorage.com
acl = private
no_check_bucket = true
```
   `.eu.` u endpointu je **obavezno** za EU bucket (bez toga je bucket nedostupan); `no_check_bucket = true` je potreban kad token
   ima samo prava na objekte (inače rclone pokuša kreirati bucket i dobije 403). Provjera: `rclone lsd r2-backup:` / `rclone ls r2-backup:wediplan-backups`.
4. **Retencija izvan servera radi lifecycle pravilo na bucketu** (skripte tamo ništa ne brišu): R2 → bucket → Settings → *Object lifecycle rules*
   → *Add rule* (točan naziv izbornika može se razlikovati): prefiks `db/` → delete nakon **14 dana** (isti rok kao `KEEP_DAYS`); prefiks `photos/archive/` → delete nakon **30 dana**.
   **NEMOJ** postaviti pravilo na `photos/current/` (to je živo zrcalo slika). Pravila se primjenjuju unutar ~24 h.
   Opcionalno: *Bucket locks* (retencija koja sprječava slučajno brisanje) — pročitaj ograničenja u Cloudflare dokumentaciji prije uključivanja.

#### Slike: kako radi `backup-photos.sh`
`rclone sync` izvor → `…/photos/current/`. Datoteke koje su na izvoru **obrisane ili prepisane** ne nestaju iz backupa nego se presele u
`…/photos/archive/YYYY-MM-DD/` — slučajno brisanje (ili napad) na izvoru ne briše i backup. `--max-delete` (default 100, `PHOTOS_MAX_DELETE`)
ograničava broj brisanja po prolazu: kad se dosegne, rclone stane i skripta javi grešku. Vraćanje slike: `rclone copy r2-backup:wediplan-backups/photos/archive/2026-09-30/vendors/<id>/ ./oporavak/`.
Probni prolaz bez promjena: `DRY_RUN=1 ops/backup-photos.sh`.

#### Restore (ručno i provjera)
```bash
# Provjera da se backup vraća (PRIVREMENA baza, produkcija se ne dira; PG* = server s pravom CREATEDB):
export PGHOST=127.0.0.1 PGUSER=postgres PGPASSWORD=…
BACKUP_AGE_IDENTITY=~/wediplan-backup-radni.key ops/restore-test.sh /var/backups/wediplan/wediplan-20260930-030001.dump.age
#   ispiše brojeve redaka (vendors, users, user_reviews, vendor_photos) i zadnju EF migraciju; exit ≠ 0 ako išta ne valja.
#   RESTORE_MIN_VENDORS=0 isključuje provjeru "barem 1 pružatelj"; KEEP_TEST_DB=1 ostavlja privremenu bazu za ručni pregled.

# Pravi restore (npr. nova baza nakon gubitka servera):
createdb wediplan
age -d -i ~/wediplan-backup-radni.key wediplan-….dump.age | pg_restore --no-owner --no-privileges -d wediplan   # enkriptirani
pg_restore --no-owner --no-privileges -d wediplan wediplan-….dump                                               # nekriptirani
# stari format iz prve verzije skripte (wediplan-*.sql.gz):  gunzip -c wediplan-….sql.gz | psql wediplan
```
Dekriptirani podaci idu ravno u `pg_restore` (cjevovod) — ne zapisuju se na disk.

**Koliko često provjeravati restore:** enkriptirani backup može provjeriti samo netko tko ima **privatni** age ključ, a njega namjerno nema
na serveru. Zato: (1) **prije svake migracije** (runbook niže) i (2) **barem jednom mjesečno** ručno — povuci zadnji backup s R2
(`rclone copy r2-backup:wediplan-backups/db/ . --max-age 2d`) na svoje računalo i pokreni `restore-test.sh` protiv lokalnog/dev Postgresa.
Povratni signal da se backup uopće stvara daju ti `exit` kodovi + ping u monitoring (v. gore).

*Opcionalno — automatski tjedni test na serveru:* generiraj **treći, namjenski** par ključeva (`age-keygen -o restore-test.key`), javni ključ dodaj u
`BACKUP_AGE_RECIPIENT`, a privatni stavi na server (`chmod 600`) samo za `restore-test.sh`. Kompromis: tko preuzme server može dekriptirati i
**stare** backupe iz R2 (ne samo trenutnu bazu, do koje ionako ima pristup) — uključi to samo ako ti je praktičnost važnija. Cron:
`0 5 * * 1  . /etc/wediplan-backup.env; BACKUP_AGE_IDENTITY=/root/restore-test.key /opt/wediplan/ops/restore-test.sh "$(ls -t /var/backups/wediplan/wediplan-*.dump* | head -1)" >> /var/log/wediplan-restore-test.log 2>&1`
(`PGUSER` mora imati pravo `CREATEDB`; za to ne koristi backup korisnika iz gornjeg savjeta).

#### Lokalni razvoj (Docker, iz `backend/`)
```bash
cd backend
docker compose exec -T postgres pg_dump -U wediplan -d wediplan -Fc > wediplan-$(date +%Y%m%d-%H%M).dump
docker compose exec -T postgres createdb -U wediplan wediplan_restore_test
docker compose exec -T postgres pg_restore -U wediplan -d wediplan_restore_test --no-owner < wediplan-XXXX.dump
docker compose exec -T postgres psql -U wediplan -d wediplan_restore_test -c "select count(*) from vendors;"
docker compose exec -T postgres dropdb -U wediplan wediplan_restore_test
```
(Dev Postgres sluša na `localhost:5433`: `PGHOST=localhost PGPORT=5433 PGUSER=wediplan PGPASSWORD=wediplan ops/backup.sh` radi i s hosta ako imaš `pg_dump`.)

#### Runbook: PRIJE svake migracije / deploya koji dira bazu
1. `ops/backup.sh` (ručno, **svježi** backup) — mora završiti s exit 0.
2. `ops/restore-test.sh <taj backup>` — mora ispisati `OK` i očekivane brojeve.
3. Tek tada: `dotnet ef database update` / deploy.
4. Ako migracija pođe po zlu: vrati iz tog backupa (v. „Pravi restore“), ne popravljaj bazu ručno.

#### GDPR: backupi i brisanje osobnih podataka
- Backupi sadrže osobne podatke (e-mailovi korisnika, kontakti pružatelja). Zato: enkripcija (age), EU pohrana (R2 EU jurisdikcija), ograničen pristup (`chmod 600/700`), zaseban token.
- **Brisanja se ne primjenjuju retroaktivno na postojeće backupe** — backupi istječu sami nakon roka (lokalno `KEEP_DAYS`, na R2 lifecycle pravilo; predloženo 14 dana).
  Tu činjenicu i rokove navedi u politici privatnosti (vlasnik; pravna provjera).
- **Nakon restorea iz backupa ponovno primijeni brisanja** izvršena od trenutka tog backupa: brisanja računa i opt-out zahtjeve.
  Izvor: dnevnik u `audit_log` (akcije `account_deleted`, `optout`) nakon što se uvede Zadatak 14; do tada log aplikacije
  (`account deleted: user=…`, `GDPR opt-out: vendor=…` u `journalctl -u wediplan-api`). Zato **čuvaj log brisanja barem onoliko koliko i backupe**.
- Arhiva slika (`photos/archive/`) zadržava obrisane fotografije do isteka lifecycle pravila (predloženo 30 dana) — uskladi s politikom privatnosti.

### 5. Monitoring
Health endpoint: `GET /api/health` → `200 {status:ok}` kad je baza dostupna, `503` inače.
Priključi vanjski uptime monitor (npr. UptimeRobot/BetterStack) na `https://api.wediplan.hr/api/health`
i na frontend. Error log: pratiti stderr .NET procesa (systemd `journalctl -u wediplan-api`).

### 6. Migracije
Faza 5 **ne uvodi novu migraciju** (tablica `vendor_photos` postoji od Faze 1). Potrebno je samo
`dotnet restore` (novi paketi ImageSharp + AWSSDK.S3) i `dotnet build`.

## Plan prioriteti 2, Zadatak 5 — claim e-mail verifikacija (2026-09-23)

**1. Migracija** (nova tablica `claim_verification_tokens`):
```bash
cd backend/Wediplan.Api
dotnet ef migrations add ClaimVerification
dotnet ef database update
```
(Sandbox u kojem je kod pisan nema pristup NuGet-u — isto ograničenje kao Faze 1/3/4 — pa
migracija NIJE generirana ondje. Kod je pažljivo ručno pregledan, ali **`dotnet build` i
`dotnet test` je potrebno pokrenuti ovdje, PRIJE mergea**, v. napomenu u STANJE.md ove sesije.)

**2. Nova konfiguracija** (appsettings ili env; opcionalna — bez nje default je `true`):
```
Claims__AutoApproveOnEmailVerify = true    (default true; postavi na false da vratiš na "jak
                                             dokaz + admin klik" bez ikakve izmjene koda)
```

**3. Ručni test toka:**
- Kao korisnik A zatraži claim na nekom profilu (kao dosad).
- Na `ClaimPanel`-u klikni "Potvrdi vlasništvo e-mailom" → ako profil ima `Vendor.Email`
  (interni, iz importa), mail (konzola ako nema Resenda) sadrži poveznicu na
  `/partner/potvrda-vlasnistva?token=…`.
- Otvori tu poveznicu (prijavljen kao isti korisnik A) → gumb/auto-potvrda → uz
  `Claims:AutoApproveOnEmailVerify=true` (default) profil je ODMAH `claimed`, korisnik A vlasnik.
- Postavi `Claims__AutoApproveOnEmailVerify=false`, ponovi s drugim profilom → nakon potvrde
  status ostaje `pending`, ali `/admin` prikazuje bedž "✓ e-mail potvrđen" — admin i dalje klikne
  Odobri.
- Profil bez `Vendor.Email` → gumb za e-mail potvrdu prikazuje objašnjenje da odobrava admin
  (fallback na dosadašnji `domain_match`/ručni pregled).

## Plan prioriteti 2, Zadatak 7 — obavijesti partnerima (2026-09-23)

Bez nove migracije i bez nove konfiguracije — koristi isti `IEmailSender`/`App:PublicUrl` kao
auth mailovi (v. gore). Nova klasa `PartnerEmails` (`Auth/PartnerEmails.cs`) šalje:
- claim odobren (i ručno preko admina i auto-approve iz Zadatka 5) → vlasniku, link na `/partner`;
- claim odbijen → korisniku, neutralan tekst;
- recenzija objavljena → vlasniku profila (**samo ako je profil claiman** — neclaimani profili
  nemaju koga obavijestiti).

Slanje je best-effort (try/catch oko `IEmailSender.SendAsync`, greška se samo logira) — admin
akcija (approve/reject claim, approve review) uvijek vraća 200 bez obzira je li mail uspio.

**Ručni test:** bez Resend ključa, odobri/odbij claim ili objavi recenziju u `/admin` → mail (s
ispravnim imenom pružatelja i poveznicom na `/partner`) se ispisuje u konzolu servera.

## Plan prioriteti 2, Zadatak 8 — monitoring: Sentry (2026-09-23)

Isti "aktivno samo s ključem" obrazac kao Resend — bez DSN-a nula promjene ponašanja (backend
i frontend), CI/dev ostaju netaknuti. `/api/health` ostaje za **uptime** (vanjski servis poput
UptimeRobot ili BetterStack ping-a taj endpoint); Sentry je za **greške** — komplementarni, ne
zamjenjuju jedno drugo.

**Backend:**
```
Sentry__Dsn = https://xxxx@oXXXXXX.ingest.sentry.io/XXXXXXX   (env, NIKAD u appsettings u gitu)
SENTRY_RELEASE = <git sha>                                     (opcionalno, npr. iz CI-ja)
```
Bez `Sentry:Dsn` (default, prazan string u `appsettings.json`) — `Sentry.AspNetCore` paket je
učitan ali `UseSentry()` se nikad poziva. `dotnet restore` treba povući novi paket prije builda.

**Frontend:**
```
NEXT_PUBLIC_SENTRY_DSN = https://xxxx@oXXXXXX.ingest.sentry.io/XXXXXXX   (Vercel env)
SENTRY_ORG / SENTRY_PROJECT / SENTRY_AUTH_TOKEN                          (opcionalno — upload
                                                                           source mapova pri buildu;
                                                                           bez njih build i dalje
                                                                           prolazi, samo se sourcemap
                                                                           upload tiho preskače)
```
**Napomena o konvenciji fajlova** (otkriveno pri implementaciji, razlikuje se od starijeg
"sentry.client/server/edge.config.ts" obrasca koji se često spominje u starijim vodičima):
instalirana verzija `@sentry/nextjs` (11.x) je taj obrazac **napustila** — SDK sad eksplicitno
upozorava i traži brisanje tih fajlova. Umjesto njih: `instrumentation-client.ts` (klijent) i
`instrumentation.ts` (server+edge, preko Next.js-evog vlastitog instrumentation hooka). Za Next 14
(verzija u ovom projektu) `withSentryConfig` u `next.config.mjs` automatski uključuje
`experimental.instrumentationHook` — ništa dodatno nije potrebno ručno postaviti. Import u
`next.config.mjs` mora biti iz `@sentry/nextjs/config` (ne iz golog `@sentry/nextjs`) — Node-ov
ESM/CJS interop ne prepoznaje `withSentryConfig` kao named export s glavnog paketa u kontekstu
učitavanja `next.config.mjs` (build inače puca s "Named export not found").

**Ručni test (vlasnik, s pravim DSN-om):**
- Backend: privremeno baci `throw new Exception("test-sentry")` u bilo koju rutu → event stiže u
  Sentry projekt unutar par sekundi.
- Frontend: privremeno baci grešku u klijentskoj komponenti (npr. `onClick={() => { throw new
  Error("test-sentry-client") }}`) → event stiže u isti ili zaseban Sentry projekt.
- Bez DSN-a (obje strane): build/test identični kao prije ovog zadatka — potvrdi da
  `dotnet test` i `npm run build` prolaze i BEZ ijedne Sentry env varijable postavljene.

**Budući, neobavezan korak** (ne blokira): upload source mapova + release marking u
`.github/workflows/ci.yml` (Sentry CLI akcija) za čitljive stack traceove u produkciji.
