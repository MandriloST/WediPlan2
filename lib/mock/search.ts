import "server-only";
import { CATEGORIES, CATEGORY_BY_SLUG, REGIONS } from "@/lib/data";
import type {
  CategoryWithCount,
  Paged,
  PinsResult,
  PinVendor,
  RegionWithCount,
  SitemapEntry,
  Suggestion,
  Vendor,
  VendorQuery,
} from "@/lib/types";
import { hasCategory, vendorCategories } from "@/lib/categories";
import { VENDORS } from "./vendors";

/**
 * MOCK implementacija API ugovora (API.md) nad data/vendors.json.
 * Aktivna samo kad API_URL NIJE postavljen. Svaka funkcija je zrcalo .NET kontrolera —
 * mijenjati ih u paru (ugovor je API.md).
 */

export const DEFAULT_PAGE_SIZE = 24; // §L
export const MAX_PAGE_SIZE = 50; // §8 anti-scraping
export const MAX_IDS = 50;
export const MAX_PINS = 1000;

const norm = (s: string) =>
  s
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/g, "d");

/** Pravilo regije za liste: sjedište ILI pokriva cijelu HR ILI pokriva tu regiju. */
export function inRegion(v: Vendor, region: string): boolean {
  return (
    v.region === region ||
    v.coverage === "hr" ||
    (Array.isArray(v.coverage) && (v.coverage as string[]).includes(region))
  );
}

function matchesQ(v: Vendor, q: string): boolean {
  const needle = norm(q);
  const cat = CATEGORY_BY_SLUG[v.category];
  const hay = norm(`${v.name} ${v.city} ${cat?.name ?? ""} ${cat?.short ?? ""} ${v.styleTags.join(" ")}`);
  return hay.includes(needle);
}

function filtered(q: { q?: string; region?: string; category?: string }): Vendor[] {
  let items = VENDORS.slice();
  if (q.region) items = items.filter((v) => inRegion(v, q.region!));
  if (q.category) items = items.filter((v) => hasCategory(v, q.category!));
  if (q.q) items = items.filter((v) => matchesQ(v, q.q!));
  return items;
}

const byRank = (a: Vendor, b: Vendor) =>
  b.rating - a.rating || b.reviewCount - a.reviewCount || a.id.localeCompare(b.id);

/** GET /api/vendors — uz `ids` vraća točno te pružatelje (max 50), ostali filtri se ignoriraju. */
export function queryVendors(
  q: Omit<VendorQuery, "region"> & { region?: string; ids?: string[] }
): Paged<Vendor> {
  if (q.ids) {
    const want = new Set(q.ids.slice(0, MAX_IDS));
    const items = VENDORS.filter((v) => want.has(v.id));
    return { items, total: items.length, page: 1, pageSize: items.length };
  }
  const items = filtered(q).sort(byRank);
  const page = Math.max(1, Math.floor(q.page ?? 1));
  const pageSize = Math.min(MAX_PAGE_SIZE, Math.max(1, Math.floor(q.pageSize ?? DEFAULT_PAGE_SIZE)));
  return {
    items: items.slice((page - 1) * pageSize, page * pageSize),
    total: items.length,
    page,
    pageSize,
  };
}

/** GET /api/pins?category=&region=&q= — category OBAVEZNA (category-first §L). */
export function queryPins(q: { category: string; region?: string; q?: string }): PinsResult {
  const all = filtered(q)
    .filter((v) => v.lat != null && v.lng != null)
    .sort(byRank);
  const items: PinVendor[] = all.slice(0, MAX_PINS).map((v) => ({
    id: v.id,
    slug: v.slug,
    name: v.name,
    category: v.category,
    categories: v.categories && v.categories.length > 1 ? v.categories : undefined,
    city: v.city,
    lng: v.lng,
    lat: v.lat,
    locationPrecision: v.locationPrecision,
    price: v.price,
    rating: v.rating,
    reviewCount: v.reviewCount,
    photos: v.photos && v.photos.length ? v.photos.slice(0, 1) : undefined,
  }));
  return { items, total: all.length };
}

/** Typeahead: kategorije, regije, gradovi, pružatelji (pružatelj vodi ravno na profil). */
export function suggest(input: string, limit = 7): Suggestion[] {
  const needle = norm(input.trim());
  if (!needle) return [];
  const out: Suggestion[] = [];

  for (const c of CATEGORIES) {
    if (norm(c.name).includes(needle) || (c.short && norm(c.short).includes(needle)))
      out.push({ type: "category", label: c.name, sub: "kategorija", href: `/${c.slug}` });
  }
  for (const r of REGIONS) {
    if (norm(r.name).includes(needle))
      out.push({ type: "region", label: r.name, sub: "regija", href: `/${r.id}` });
  }
  const cities = new Set<string>();
  for (const v of VENDORS) {
    if (v.city && norm(v.city).includes(needle) && !cities.has(v.city) && cities.size < 5) {
      cities.add(v.city);
      out.push({ type: "city", label: v.city, sub: "grad", href: `/?q=${encodeURIComponent(v.city)}` });
    }
  }
  for (const v of VENDORS.filter((x) => norm(x.name).includes(needle)).sort(byRank).slice(0, 7)) {
    const cat = CATEGORY_BY_SLUG[v.category];
    out.push({
      type: "vendor",
      label: v.name,
      sub: [cat?.short ?? cat?.name, v.city].filter(Boolean).join(" · "),
      href: `/pruzatelj/${v.slug}`,
    });
  }
  return out.slice(0, limit);
}

/** GET /api/regions?category= — brojači po pravilu liste (sjedište ∪ pokrivanje). */
export function regionsWithCounts(category?: string): RegionWithCount[] {
  const base = category ? VENDORS.filter((v) => hasCategory(v, category)) : VENDORS;
  return REGIONS.map((r) => ({ ...r, count: base.filter((v) => inRegion(v, r.id)).length }));
}

/** GET /api/categories?region= — brojači po SVIM kategorijama (§4.3). */
export function categoriesWithCounts(region?: string): CategoryWithCount[] {
  const counts: Record<string, number> = {};
  for (const v of VENDORS) {
    if (region && !inRegion(v, region)) continue;
    for (const c of vendorCategories(v)) counts[c] = (counts[c] ?? 0) + 1;
  }
  return CATEGORIES.map((c) => ({ ...c, count: counts[c.slug] ?? 0 }));
}

/** GET /api/sitemap */
export function sitemapEntries(): SitemapEntry[] {
  return VENDORS.map((v) => ({ slug: v.slug }));
}
