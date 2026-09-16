import type { Vendor } from "./types";

/** Minimalni oblik potreban za pravila kategorija (Vendor i PinVendor ga zadovoljavaju). */
export type Categorized = Pick<Vendor, "category" | "categories">;

/**
 * Sve kategorije pružatelja (§4.3). Izvor istine za listinge i usporedbu.
 * Fallback na primarnu ako `categories` nije postavljen (API ga izostavlja kad je samo primarna).
 * NAPOMENA: sliku, breadcrumb, budžetsku grupu i "Slične" i dalje određuje `vendor.category`
 * (primarna) — ne koristiti ovu funkciju za to.
 */
export function vendorCategories(v: Categorized): string[] {
  return v.categories && v.categories.length ? v.categories : [v.category];
}

/** Dodatne (ne-primarne) kategorije — za diskretan prikaz "· također: …" na profilu. */
export function extraCategories(v: Categorized): string[] {
  return vendorCategories(v).filter((c) => c !== v.category);
}

/** Pojavljuje li se pružatelj u danoj kategoriji (bilo primarnoj ili dodatnoj). */
export function hasCategory(v: Categorized, slug: string): boolean {
  return vendorCategories(v).includes(slug);
}

/* ----------------------- Usporedba: kompatibilnost (§4.3) ----------------------- */

/**
 * Meta-podaci odabranih u usporedbi (id → kategorije). Od Faze 2 klijent više nema
 * cijeli katalog, pa compare store pamti kategorije u trenutku dodavanja
 * (i osvježava ih s API-ja — v. CompareTray).
 */
export type CompareCats = Record<string, string[] | undefined>;

/**
 * Kategorije zajedničke SVIM odabranima (presjek). ID bez poznatih kategorija
 * (npr. stari localStorage prije osvježavanja) se preskače — blaže, ne blokira.
 */
export function compareCommonCategories(ids: string[], cats: CompareCats): Set<string> | null {
  let common: string[] | null = null;
  for (const id of ids) {
    const c = cats[id];
    if (!c) continue;
    common = common === null ? c : common.filter((x) => c.includes(x));
  }
  return common === null ? null : new Set(common);
}

/**
 * Smije li se pružatelj dodati u usporedbu? Prazna lista ⇒ da; već odabran ⇒ da
 * (dopusti odznačavanje); inače mora dijeliti ≥ 1 kategoriju sa zajedničkim skupom.
 */
export function canAddToCompare(
  candidate: Categorized & { id: string },
  ids: string[],
  cats: CompareCats
): boolean {
  if (ids.length === 0 || ids.includes(candidate.id)) return true;
  const common = compareCommonCategories(ids, cats);
  if (common === null) return true; // ništa poznato o odabranima → ne blokiraj
  return vendorCategories(candidate).some((c) => common.has(c));
}

/** Jedinstvena poruka za onemogućeni checkbox usporedbe. */
export const COMPARE_INCOMPATIBLE_HINT =
  "Za usporedbu odaberite pružatelje iste kategorije";
