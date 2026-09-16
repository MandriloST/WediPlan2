# Wediplan — MVP (Landing + Search + Compare + Budget)

Marketplace za planiranje vjenčanja u Hrvatskoj. Implementacija odobrenog **hibridnog smjera** (wireframe `2a` desktop + `2b` mobilna PWA): pretraga odmah + interaktivna karta Hrvatske kao hero, usporedba i kalkulator budžeta neintruzivno dostupni.

## Pokretanje

```bash
npm install
npm run dev        # http://localhost:3000
npm run build && npm start   # produkcija (service worker aktivan samo u produkciji)
```

## Što je implementirano

- **Istraži (`/`)** — naslov, search bar s typeaheadom (pružatelji, kategorije, gradovi), regija, opcionalni datum; lista regija s brojačima; chipovi kategorija; **MapLibre karta HR** s poligonima regija (klik = filter + zoom), **cijene na pinovima**, klasteri, popup kartica s „usporedi”.
- **URL-driven stanje** — `/dalmacija/foto-i-video?q=…` (shareable/SEO); regija ⇄ karta sinkronizirano.
- **Rezultati** — kartice s uvijek vidljivom cijenom, ocjenom, bedževima (`✓ provjereno`, `✓ kalendar uživo` / `na upit`), checkbox „usporedi”, favoriti ♡; skeleton loading; empty state s prijedlozima.
- **Usporedba (`/usporedba`)** — tablica 2–4: cijena (istaknuta), recenzije, dostupnost za odabrani datum (mock dok ne stigne live kalendar), stil; horizontalni scroll na mobitelu; max 4 uz zamjenu najstarijeg + toast.
- **Budžet** — drawer na desktopu (plutajući gumb na karti) i vlastiti tab na mobitelu (`/budzet`): gosti, regija, slider, regionalna raspodjela, **„Vaš plan”** s capovima po kategoriji i brojem opcija; pružatelji iznad capa **posivljeni, nikad skriveni**.
- **Mobilna PWA** — donja tab navigacija (5 tabova, ≥44px), manifest, ikone, service worker (shell cache + SWR za API); plan/favoriti/usporedba u localStorage → rade offline.
- **Podaci** — svih 29 kategorija vlasnika (nazivi promjenjivi, slugovi stabilni), 5 regija sa stvarnim GeoJSON granicama (županije spojene u marketinške regije), ~55 mock pružatelja.

## Struktura

```
app/                  App Router stranice + /api/* (mock, zrcali .NET ugovor — vidi API.md)
components/           Header, SearchBar, CroatiaMap, VendorCard, CompareTray, BudgetCalculator…
lib/                  types, data (mock seed), search, budget, format
stores/               Zustand + persist: compare, budget plan, favorites, datum
public/data/          croatia-regions.geojson (5 regija)
public/sw.js          service worker
```


## Unos stvarnih podataka (zamjena mocka)

1. Otvori `data/vendors-template.xlsx` (list **Upute** ima cijeli postupak; **Pružatelji** i **Recenzije** se popunjavaju, primjeri s „PRIMJER” se preskaču).
2. Koordinate: Google Maps → desni klik na lokaciju → klik na koordinate → zalijepi (skripta tolerira i obrnuti redoslijed).
3. Pokreni uvoz s validacijom:
   ```bash
   npm run import:vendors -- putanja/do/tvoje-datoteke.xlsx
   ```
   Greške se ispisuju s brojem retka; dok ih ima, ništa se ne piše. Uspješan uvoz generira `data/vendors.json` + `data/profiles.json` koje frontend čita (mock je samo početni sadržaj tih datoteka).
4. `npm run dev` za pregled → `git commit data/` → podaci su na stranici.

Kolone `web/telefon/email` su interna evidencija — import ih **ne** objavljuje (Direktan kontakt: zasad ne). Za prenesene ocjene i recenzije `izvor` je obavezan; prikazuje se uz ocjenu na profilu. Isti Excel kasnije postaje seed za .NET bazu — ugovor je isti (API.md).


## Slike pružatelja

Konvencija (ista danas u repou i sutra na Bunny CDN-u):
```
public/images/vendors/<slug>/01.jpg   ← stvarne slike, max 3 (02.jpg, 03.jpg), UZ IZRIČITU DOZVOLU
public/images/defaults/<kategorija-slug>.jpg  ← default kategorije kad vendor nema slika (npr. foto-i-video.jpg; 29 kom, mora biti .jpg)
```
1. `npm run scaffold:images` — napravi folder za SVAKOG pružatelja iz data/vendors.json (ne dira postojeće). Svaki prazan folder sadrži `STAVI_SLIKE_OVDJE.txt` s podsjetnikom (slobodno obriši tu datoteku).
2. Ubaci `01.jpg` (glavna slika), po želji `02.jpg`, `03.jpg` — jpg/png/webp, preporuka max ~1600 px širine.
3. `npm run sync:images` — upiše `photos` u data/vendors.json (import to radi i sam). Skripta upozorava na tipfelere u imenu foldera i višak slika.
3. `next/image` sam radi sve veličine i formate (na Vercelu automatski). Bez stvarnih slika prikazuje se default njegove kategorije (alt: „ilustracija”); privremene generirane defaulte zamijeni pravim fotografijama istog imena. `sync:images` upozorava ako kategoriji u upotrebi fali default.

**Selidba na Bunny kasnije:** preslikaj `public/images/` strukturu u storage zonu i postavi `NEXT_PUBLIC_IMAGE_BASE=https://<zona>.b-cdn.net` (env na Vercelu) — `next.config.mjs` automatski dodaje remotePattern, kod se ne mijenja.

## Prelazak na .NET backend

Frontend zove isključivo `/api/*` preko fetch/TanStack Query. Zamjena: postavi Next.js `rewrites` prema ASP.NET Core servisu koji implementira ugovor iz `API.md` — komponente se ne mijenjaju. Za instant search u produkciji preporuka iz handoffa: Typesense/Meilisearch iza `/api/suggest` i `/api/vendors?q=`.

## Coming soon (pripremljeno u UI-ju)

- Live kalendar dostupnosti → badge/fallback logika već postoji (`liveCalendar` flag).
- Vendor CRM, prijava (e-mail + Google) → placeholderi u Profilu.
- i18n (en za destination weddings) → hr-HR formati centralizirani u `lib/format.ts`.
