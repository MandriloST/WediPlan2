/**
 * "Izdvajamo" — izdvojeni pružatelji na naslovnici (čista logika, bez Next/React ovisnosti).
 *
 * Izvor: data/featured.json (ručno uređivanje — upute u IZDVOJENI.md). Ovaj modul NE uvozi
 * ništa osim tipova, tako da ga može izravno pokrenuti i `npm run featured:check`
 * (Node ≥ 22.18 izvodi .ts bez dodatnih alata).
 *
 * Pravila:
 *  - datumi su "YYYY-MM-DD" po hrvatskom vremenu (Europe/Zagreb), OBA kraja uključena;
 *    `to` izostavljen ili null = bez kraja
 *  - prikazuje se najviše 6: redom po `position` (pa po ranijem `from`) → 1 veliki, 2 srednja, 3 mala.
 *    Rupe se ne ostavljaju: ako je aktivno samo 4, ide 1 + 2 + 1.
 *  - isti slug dvaput aktivan → ostaje onaj s manjom pozicijom
 *  - neispravan unos se preskače (i prijavljuje u logu / featured:check), nikad ne ruši naslovnicu
 *  - kind "editorial" = naš besplatni odabir, "sponsored" = plaćeno isticanje (drugačija oznaka na kartici)
 */
import type { Vendor } from "./types";

export type FeaturedKind = "editorial" | "sponsored";

export interface FeaturedPlacement {
  slug: string;
  /** 1 = veliki, 2–3 = srednji, 4–6 = mali (redoslijed prioriteta; v. pravila gore) */
  position: number;
  /** "YYYY-MM-DD", uključivo */
  from: string;
  /** "YYYY-MM-DD", uključivo; izostavljen/null = bez kraja */
  to?: string | null;
  kind: FeaturedKind;
  /** interna bilješka (ne prikazuje se) */
  note?: string;
}

export interface FeaturedFile {
  placements: FeaturedPlacement[];
}

/** Ono što naslovnica dobije za svaki aktivni unos (profil je provjeren: postoji i nije skriven). */
export interface FeaturedItem {
  vendor: Vendor;
  about: string;
  kind: FeaturedKind;
}

export const FEATURED_MAX = 6;
export const FEATURED_TIMEZONE = "Europe/Zagreb";

const DATE_RE = /^\d{4}-\d{2}-\d{2}$/;

/** Današnji datum u Hrvatskoj kao "YYYY-MM-DD" (en-CA daje upravo taj oblik). */
export function todayInZagreb(now: Date = new Date()): string {
  return new Intl.DateTimeFormat("en-CA", {
    timeZone: FEATURED_TIMEZONE,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).format(now);
}

function isRealDate(s: string): boolean {
  if (!DATE_RE.test(s)) return false;
  const [y, m, d] = s.split("-").map(Number);
  const dt = new Date(Date.UTC(y, m - 1, d));
  return dt.getUTCFullYear() === y && dt.getUTCMonth() === m - 1 && dt.getUTCDate() === d;
}

/** Popis grešaka za jedan unos (prazno = ispravan). Poruke su na hrvatskom jer ih čita vlasnik. */
export function validatePlacement(p: unknown): string[] {
  const errors: string[] = [];
  if (!p || typeof p !== "object") return ["unos nije objekt"];
  const x = p as Record<string, unknown>;
  if (typeof x.slug !== "string" || x.slug.trim() === "") errors.push("nedostaje slug");
  if (typeof x.position !== "number" || !Number.isInteger(x.position) || x.position < 1)
    errors.push("position mora biti cijeli broj ≥ 1");
  if (typeof x.from !== "string" || !isRealDate(x.from)) errors.push(`from mora biti datum YYYY-MM-DD (dobiveno: ${String(x.from)})`);
  if (x.to !== undefined && x.to !== null) {
    if (typeof x.to !== "string" || !isRealDate(x.to)) errors.push(`to mora biti datum YYYY-MM-DD ili null (dobiveno: ${String(x.to)})`);
    else if (typeof x.from === "string" && isRealDate(x.from) && x.to < x.from) errors.push("to je prije from");
  }
  if (x.kind !== "editorial" && x.kind !== "sponsored") errors.push(`kind mora biti "editorial" ili "sponsored" (dobiveno: ${String(x.kind)})`);
  return errors;
}

export function isActiveOn(p: FeaturedPlacement, today: string): boolean {
  return p.from <= today && (p.to == null || today <= p.to);
}

/**
 * Aktivni unosi za zadani dan, sortirani i ograničeni na FEATURED_MAX.
 * `onInvalid` (neobavezno) dobije svaki preskočeni neispravan unos — za log.
 */
export function activePlacements(
  list: unknown,
  today: string,
  onInvalid?: (index: number, errors: string[]) => void
): FeaturedPlacement[] {
  if (!Array.isArray(list)) return [];
  const valid: FeaturedPlacement[] = [];
  list.forEach((p, i) => {
    const errors = validatePlacement(p);
    if (errors.length) onInvalid?.(i, errors);
    else valid.push({ ...(p as FeaturedPlacement), slug: (p as FeaturedPlacement).slug.trim() });
  });
  const sorted = valid
    .filter((p) => isActiveOn(p, today))
    .sort((a, b) => a.position - b.position || a.from.localeCompare(b.from));
  const seen = new Set<string>();
  const out: FeaturedPlacement[] = [];
  for (const p of sorted) {
    if (seen.has(p.slug)) continue;
    seen.add(p.slug);
    out.push(p);
    if (out.length >= FEATURED_MAX) break;
  }
  return out;
}

/** 1 veliki, zatim do 2 srednja, zatim do 3 mala — redom kako su došli. */
export function splitTiers<T>(items: T[]): { hero?: T; medium: T[]; small: T[] } {
  return { hero: items[0], medium: items.slice(1, 3), small: items.slice(3, FEATURED_MAX) };
}
