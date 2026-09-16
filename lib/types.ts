export type RegionId = "istra" | "kvarner" | "dalmacija" | "zagreb" | "slavonija";

/** Budget groups drive the calculator distribution and per-category caps. */
export type BudgetGroup = "sala" | "catering" | "foto" | "glazba" | "ostalo";

export interface Region {
  id: RegionId;
  name: string;
  /** [lng, lat] map focus point */
  center: [number, number];
  /** [[west, south], [east, north]] for map fitBounds */
  bounds: [[number, number], [number, number]];
}

export interface Category {
  slug: string;
  name: string;
  short?: string; // label for chips when the full name is long
  group: BudgetGroup;
}

export type PriceModel =
  | { kind: "from"; from: number } // "od 850 €"
  | { kind: "perPerson"; from: number; to: number } // "55–80 €/os."
  | { kind: "onRequest" }; // pružatelj ne objavljuje cijenu — "cijena na upit"

export interface Vendor {
  id: string;
  slug: string;
  name: string;
  category: string; // Category.slug
  /** SVE kategorije [primarna, ...dodatne] (§4.3) — izostanak = samo primarna. Listinzi/brojači/usporedba čitaju ovo; slika/budžet/breadcrumb čitaju category. */
  categories?: string[]; // Category.slug[]
  /** regija SJEDIŠTA (gdje je pružatelj baziran) */
  region: RegionId;              // "" (prazno) za inozemne — v. country
  /** Država sjedišta: izostavljeno = "hr"; "ba" (BiH), "si" (Slovenija).
   *  Inozemni (BiH fotografi s pokrivanjem HR) nemaju HR regiju sjedišta. */
  country?: "ba" | "si" | string;
  /** grad sjedišta; "" = poznata samo regija */
  city: string;
  /** null = koordinate nepoznate → bez pina na karti (nikad ne izmišljamo točku) */
  lng: number | null;
  lat: number | null;
  /** exact = koordinate; city = grad bez koordinata (čeka geokodiranje); region = samo regija */
  locationPrecision?: "exact" | "city" | "region";
  /** POKRIVANJE — regije u kojima pružatelj RADI uz svoju; "hr" = cijela Hrvatska */
  coverage?: RegionId[] | "hr";
  /** slobodni tekst o pokrivanju, npr. "radi u Splitu, Zadru i Šibeniku" */
  coverageNote?: string;
  price: PriceModel;
  rating: number;
  reviewCount: number;
  /** izvor prenesene ocjene (npr. "Google recenzije") — obavezno uz prenesenu ocjenu */
  ratingSource?: string;
  /** platform verification — "✓ provjereno" */
  verified: boolean;
  /** vendor keeps a live availability calendar — "✓ kalendar uživo" */
  liveCalendar: boolean;
  styleTags: string[];
  /** javne društvene poveznice (portfolio) — normalizirani URL-ovi, prikazuju se kao ikone */
  social?: { instagram?: string; facebook?: string };
  /** faza 3/4 (claim): "claimed" aktivira oznaku "✓ Verificirani profil" — importi ga zasad ne postavljaju */
  claimStatus?: "unclaimed" | "claimed";
  /** imena datoteka u public/images/vendors/<slug>/ — puni ih import/sync skripta */
  photos?: string[];
}

export interface RegionWithCount extends Region {
  count: number;
}

export interface BudgetDistribution {
  region: RegionId | "hr";
  /** shares per group, sums to 1 */
  shares: Record<BudgetGroup, number>;
}

export interface BudgetPlan {
  guests: number;
  region: RegionId;
  total: number;
  /** derived caps in €, per budget group */
  caps: Record<BudgetGroup, number>;
}

export interface VendorQuery {
  q?: string;
  region?: RegionId;
  category?: string;
  date?: string; // ISO yyyy-mm-dd, optional
  page?: number;
  pageSize?: number;
}

export interface Paged<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

/** Feature #4 — dvojaka recenzija. Prenesene recenzije skuplja Wediplan od
 *  pružatelja prije lansiranja (screenshotovi/izvori), uz vidljivu oznaku izvora. */
export interface ImportedReview {
  author: string;
  rating: number;
  text: string;
  source: string; // npr. "Google recenzije"
  year: number;
}

export interface VendorProfileData {
  vendor: Vendor;
  about: string;
  services: string[];
  importedReviews: ImportedReview[];
}

/* ------------------------------------------------------------------ */
/* Faza 2 — dopune ugovora (API.md)                                    */
/* ------------------------------------------------------------------ */

/** GET /api/pins — minimalni oblik za kartu (podskup Vendora; Vendor ga zadovoljava). */
export type PinVendor = Pick<
  Vendor,
  "id" | "slug" | "name" | "category" | "categories" | "city" | "lng" | "lat" |
  "locationPrecision" | "price" | "rating" | "reviewCount" | "photos"
>;

export interface PinsResult {
  items: PinVendor[];
  /** ukupno pružatelja s koordinatama za upit (može biti > items.length ako je rezano capom) */
  total: number;
}

/** GET /api/categories?region= */
export interface CategoryWithCount extends Category {
  count: number;
}

/** GET /api/suggest?q= */
export interface Suggestion {
  type: "category" | "region" | "vendor" | "city";
  label: string;
  sub?: string;
  href: string;
}

/** GET /api/budget-matches — koliko pružatelja stane u capove plana (po grupi i ukupno). */
export interface BudgetMatches {
  region: RegionId;
  matches: number;
  groups: Record<BudgetGroup, number>;
}

/** GET /api/sitemap — samo slugovi i vrijeme izmjene (bez podataka pružatelja). */
export interface SitemapEntry {
  slug: string;
  updatedAt?: string;
}
