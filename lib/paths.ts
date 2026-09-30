import type { RegionId } from "./types";

export interface ExploreFilters {
  region?: RegionId;
  category?: string;
  q?: string;
  /** početna stranica liste (?page=N; SEO/crawleri) — 1 ako izostavljeno */
  page?: number;
}

/** Kanonski URL filtra: /regija/kategorija?q=&page= (slugovi = ugovor, §L). */
export function pathFor(f: ExploreFilters): string {
  const path = "/" + [f.region, f.category].filter(Boolean).join("/");
  const sp = new URLSearchParams();
  if (f.q) sp.set("q", f.q);
  if (f.page && f.page > 1) sp.set("page", String(f.page));
  const qs = sp.toString();
  return qs ? `${path}?${qs}` : path;
}


/**
 * "Sve kategorije" (grid svih 29) — od redizajna naslovnice (3a) živi na /kategorije,
 * jer je "/" landing. Uz regiju ostaje /regija (isti grid, sužen na regiju).
 */
export function browsePath(region?: RegionId): string {
  return region ? `/${region}` : "/kategorije";
}

/** Broj pružatelja po stranici rezultata — tvrdi limit, UI nikad ne prikazuje više (paginacija, bez "Učitaj još"). */
export const PAGE_SIZE = 12;
