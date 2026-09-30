import type { Category, Region, BudgetDistribution, RegionId } from "./types";

/* ------------------------------------------------------------------ */
/* Regions (5 marketing regions — polygons in /public/data/*.geojson) */
/* ------------------------------------------------------------------ */

export const REGIONS: Region[] = [
  { id: "istra", name: "Istra", center: [13.9, 45.15], bounds: [[13.4, 44.75], [14.4, 45.55]] },
  { id: "kvarner", name: "Kvarner", center: [14.7, 44.9], bounds: [[13.9, 44.2], [15.9, 45.7]] },
  { id: "dalmacija", name: "Dalmacija", center: [16.4, 43.6], bounds: [[14.5, 42.35], [18.6, 44.6]] },
  { id: "zagreb", name: "Zagreb i okolica", center: [16.0, 45.8], bounds: [[14.8, 44.9], [17.4, 46.6]] },
  { id: "slavonija", name: "Slavonija", center: [18.0, 45.4], bounds: [[16.8, 44.8], [19.5, 46.0]] },
];

export const REGION_BY_ID = Object.fromEntries(REGIONS.map((r) => [r.id, r])) as Record<RegionId, Region>;

/* ------------------------------------------------------------- */
/* Categories — full owner list (29). Names are editable content */
/* and will live in the .NET backend / CMS; slugs are stable.     */
/* ------------------------------------------------------------- */

export const CATEGORIES: Category[] = [
  { slug: "restorani-i-sale", name: "Restorani i sale", short: "Dvorane", group: "sala" },
  { slug: "foto-i-video", name: "Foto i Video", short: "Fotografi", group: "foto" },
  { slug: "glazba-bendovi", name: "Glazba — bendovi", short: "Bendovi", group: "glazba" },
  { slug: "torte-i-kolaci", name: "Torte i kolači", short: "Torte", group: "catering" },
  { slug: "vjencanice", name: "Vjenčanice i dodaci", short: "Vjenčanice", group: "ostalo" },
  { slug: "frizerski-saloni", name: "Frizerski saloni", short: "Frizeri", group: "ostalo" },
  { slug: "sminka-nokti", name: "Šminka, nokti i trepavice", short: "Šminka", group: "ostalo" },
  { slug: "muska-odijela", name: "Muška odijela i dodaci", short: "Odijela", group: "ostalo" },
  { slug: "nakit-i-prstenje", name: "Nakit i prstenje", short: "Nakit", group: "ostalo" },
  { slug: "pozivnice", name: "Pozivnice, zahvalnice i popis gostiju", short: "Pozivnice", group: "ostalo" },
  { slug: "catering", name: "Catering", group: "catering" },
  { slug: "harmonikasi", name: "Harmonikaši", group: "glazba" },
  { slug: "cvijece-i-dekoracije", name: "Cvijeće, dekoracije i baloni", short: "Cvijeće", group: "ostalo" },
  { slug: "dj", name: "DJ", group: "glazba" },
  { slug: "najam-limuzina", name: "Najam auta, limuzina i oldtimera", short: "Oldtimeri", group: "ostalo" },
  { slug: "dodaci-za-djevojacke", name: "Dodaci za djevojačke i momačke", short: "Djevojačke", group: "ostalo" },
  { slug: "reveri-i-dodaci", name: "Reveri, narukvice, podvezice i dodaci", short: "Reveri", group: "ostalo" },
  { slug: "prijevoz-i-transferi", name: "Prijevoz i transferi", short: "Transferi", group: "ostalo" },
  { slug: "simbolicni-maticar", name: "Simbolični matičar", short: "Matičari", group: "ostalo" },
  { slug: "rasvjeta-i-razglas", name: "Rasvjeta, razglas i efekti", short: "Rasvjeta", group: "ostalo" },
  { slug: "glazba-za-crkvu", name: "Glazba za crkvu", short: "Crkvena glazba", group: "glazba" },
  { slug: "bracna-putovanja", name: "Bračna putovanja", short: "Putovanja", group: "ostalo" },
  { slug: "organizatori", name: "Organizatori vjenčanja", short: "Organizatori", group: "ostalo" },
  { slug: "skole-plesa", name: "Škole plesa", short: "Ples", group: "ostalo" },
  { slug: "konobe-i-prostori", name: "Konobe i prostori za proslavu", short: "Konobe", group: "sala" },
  { slug: "foto-kabine", name: "Audio, foto kabine i selfie mirror", short: "Foto kabine", group: "foto" },
  { slug: "najam-satora", name: "Najam šatora i opreme", short: "Šatori", group: "sala" },
  { slug: "cuvanje-djece", name: "Čuvanje i animacija djece", short: "Animacija djece", group: "ostalo" },
  { slug: "najam-kuce", name: "Najam kuće za proslavu", short: "Kuće", group: "sala" },
];

export const CATEGORY_BY_SLUG = Object.fromEntries(CATEGORIES.map((c) => [c.slug, c]));

/** Chips shown in the explore sidebar (wireframe 2a) — the rest live under "Sve". */
export const FEATURED_CATEGORY_SLUGS = [
  "restorani-i-sale",
  "foto-i-video",
  "glazba-bendovi",
  "dj",
  "catering",
  "cvijece-i-dekoracije",
];

/* ------------------------------------------------------------------- */
/* Pružatelji se NE nalaze u ovom modulu (Faza 2): dolaze s API-ja      */
/* (lib/api). Mock katalog za lokalni rad bez backenda: lib/mock/.       */
/* ------------------------------------------------------------------- */

/* --------------------------------------------------- */
/* Budget distribution defaults (regional, from API).  */
/* --------------------------------------------------- */

export const BUDGET_DEFAULTS: BudgetDistribution[] = [
  { region: "hr", shares: { sala: 0.4, catering: 0.25, foto: 0.15, glazba: 0.1, ostalo: 0.1 } },
  { region: "dalmacija", shares: { sala: 0.42, catering: 0.24, foto: 0.15, glazba: 0.09, ostalo: 0.1 } },
  { region: "istra", shares: { sala: 0.43, catering: 0.24, foto: 0.14, glazba: 0.09, ostalo: 0.1 } },
  { region: "kvarner", shares: { sala: 0.41, catering: 0.25, foto: 0.14, glazba: 0.1, ostalo: 0.1 } },
  { region: "zagreb", shares: { sala: 0.38, catering: 0.26, foto: 0.16, glazba: 0.1, ostalo: 0.1 } },
  { region: "slavonija", shares: { sala: 0.35, catering: 0.27, foto: 0.14, glazba: 0.14, ostalo: 0.1 } },
];

export const GROUP_LABELS: Record<string, string> = {
  sala: "Sala",
  catering: "Catering",
  foto: "Foto",
  glazba: "Glazba",
  ostalo: "Ostalo",
};

/** Oznake država za inozemne pružatelje (§ BiH model). */
export const COUNTRY_LABELS: Record<string, string> = {
  ba: "BiH",
  si: "Slovenija",
};

/** "Split, Dalmacija" | "Sarajevo, BiH" | "BiH" — sjedište za prikaz.
 *  Inozemni (country != "hr") nemaju HR regiju; nikad ne ruši na praznoj regiji. */
export function homeLabel(v: { region: string; city: string; country?: string }): string {
  const place =
    v.country && v.country !== "hr"
      ? COUNTRY_LABELS[v.country] ?? v.country.toUpperCase()
      : REGION_BY_ID[v.region as keyof typeof REGION_BY_ID]?.name ?? "";
  return v.city ? (place ? `${v.city}, ${place}` : v.city) : place;
}
