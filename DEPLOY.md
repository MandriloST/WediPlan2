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

### 4. Backup baze
`ops/backup.sh` (pg_dump + gzip + rotacija). Cron primjer:
```
0 3 * * * /opt/wediplan/ops/backup.sh >> /var/log/wediplan-backup.log 2>&1
```
Povremeno kopirati backupe izvan servera (isti R2, odvojeni prefix, ili rclone drugdje).

### 5. Monitoring
Health endpoint: `GET /api/health` → `200 {status:ok}` kad je baza dostupna, `503` inače.
Priključi vanjski uptime monitor (npr. UptimeRobot/BetterStack) na `https://api.wediplan.hr/api/health`
i na frontend. Error log: pratiti stderr .NET procesa (systemd `journalctl -u wediplan-api`).

### 6. Migracije
Faza 5 **ne uvodi novu migraciju** (tablica `vendor_photos` postoji od Faze 1). Potrebno je samo
`dotnet restore` (novi paketi ImageSharp + AWSSDK.S3) i `dotnet build`.
