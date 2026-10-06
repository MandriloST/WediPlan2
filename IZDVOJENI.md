# IZDVOJENI.md — sekcija „Izdvajamo“ na naslovnici

Sekcija stoji između „Najbolje ocijenjeni“ i „Istražite Hrvatsku“: **1 veliki, 2 srednja, 3 mala** pružatelja.
Tko je izdvojen i od kada do kada određuje jedna datoteka: **`data/featured.json`**.

## Kako promijeniti izdvojene (5 minuta)

1. Otvori `data/featured.json`.
2. Svaki pružatelj je jedan unos u popisu `placements`:

   ```json
   {
     "slug": "lazareti-complex",
     "position": 1,
     "from": "2026-10-06",
     "to": "2026-12-31",
     "kind": "editorial",
     "note": "Početni odabir — besplatno"
   }
   ```

   | Polje | Što znači |
   |---|---|
   | `slug` | Slug pružatelja — isti kao u adresi profila: `/pruzatelj/`**`lazareti-complex`** |
   | `position` | 1 = veliki, 2–3 = srednji, 4–6 = mali |
   | `from` | Prvi dan prikaza, oblik `GGGG-MM-DD` |
   | `to` | Zadnji dan prikaza (uključivo), ili `null` = bez kraja |
   | `kind` | `"editorial"` = naš besplatni odabir (oznaka **Izdvojeno**), `"sponsored"` = plaćeno (oznaka **Sponzorirano**) |
   | `note` | Tvoja bilješka, ne prikazuje se na stranici |

3. Provjeri:

   ```bash
   npm run featured:check              # tko je izdvojen danas
   npm run featured:check 2027-01-15   # tko će biti izdvojen na taj dan
   ```

   Ispiše greške (krivi datum, nepoznat slug…) i točno tko ide na koje mjesto.
4. Commit + push. Na Vercelu se primijeni nakon deploya.

**Datumi se mijenjaju sami.** Ne moraš ništa raditi na dan početka ili isteka: naslovnica svaki put
gleda današnji datum (hrvatsko vrijeme). Možeš unaprijed upisati raspored za cijelu sezonu.

## Pravila (dobro znati)

- **Rupe se ne ostavljaju.** Ako su danas aktivna samo 4 unosa, prikaže se 1 veliki + 2 srednja + 1 mali.
  `position` je zapravo redoslijed: niži broj = istaknutije mjesto.
- **Najviše 6.** Ako se datumi preklapaju i aktivno ih je više, prikaže se 6 s najmanjom pozicijom.
- **Dva unosa s istom pozicijom** → prvi je onaj s ranijim `from`.
- **Nitko aktivan** → sekcija se ne prikazuje (stranica izgleda kao prije).
- **Neispravan unos** ili **pružatelj skriven / opt-out / ne postoji** → taj unos se preskače, ostali rade. Naslovnica se nikad ne ruši zbog ove datoteke.
- **Opis** na velikoj kartici dolazi iz profila pružatelja („O nama“, stupac `opis` u Excelu). Ako ga nema, kartica je bez opisa.
- **Slike**: ako pružatelj ima svoje fotografije, koristi se prva; inače široka zadana slika kategorije.

## Kad krene naplata

Plaćeno isticanje mora biti **jasno označeno** (EU pravila o zaštiti potrošača za rangiranja i oglase —
provjeriti s pravnikom). Zato:

- plaćenim unosima obavezno stavi `"kind": "sponsored"` → kartica dobije oznaku **Sponzorirano**,
  a napomena uz naslov sekcije postane „odabir Wediplana i sponzorirani partneri“;
- tekstovi oznaka su u `lib/landing.ts` (`featuredBadge`, `sponsoredBadge`, `featuredNote*`).

## Prijedlog sustava za kasnije (nije implementirano)

Kad izdvajanje postane plaćeno, JSON datoteka više nije dovoljna. Predloženi koraci, redom:

1. **Tablica u bazi** `featured_placements` (umjesto JSON-a): `id, vendor_id, position, starts_on, ends_on, kind,
   price_eur, note, created_by, created_at`. Javni endpoint `GET /api/featured` vraća današnje — isti oblik kao
   `FeaturedItem` u `lib/featured.ts`, pa se frontend ne mijenja osim `lib/featured-server.ts`.
2. **Admin ekran** u postojećem `AdminPanel`: kalendar pozicija po tjednima, upozorenje na preklapanja,
   „dupliciraj za sljedeći mjesec“. Promjene idu u postojeći dnevnik promjena (audit, Zadatak 14).
3. **Statistika za partnere**: prikazi i klikovi izdvojenih kartica (novi event u first-party analitici —
   dodaje se na dva mjesta: `lib/analytics.ts` i whitelist u `EventsController`). Bez toga je teško prodati
   sljedeći mjesec.
4. **Samoposluga partnera**: partner u nadzornoj ploči bira slobodan termin i poziciju, plaća (Stripe/CorvusPay),
   termin se rezervira. Cijena po poziciji (veliki > srednji > mali) i po sezoni (svibanj–rujan skuplje).
5. **Pravila poštenja**: najviše N tjedana zaredom za istog pružatelja, rotacija ako je više plaćenih za istu
   poziciju, `editorial` mjesta ostaju za kvalitetne nove pružatelje.
