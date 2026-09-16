import type {
  BudgetMatches,
  CategoryWithCount,
  Paged,
  PinsResult,
  RegionWithCount,
  Suggestion,
  Vendor,
} from "@/lib/types";

/**
 * Klijentski pristup API-ju (Faza 2). Uvijek RELATIVNI `/api/*` URL-ovi:
 *  - s postavljenim API_URL → next.config.mjs ih prepisuje na .NET (isti origin,
 *    bez CORS-a; kolačići za Fazu 3 rade automatski),
 *  - bez API_URL → poslužuju ih mock rute u app/api/* (lokalni rad bez backenda).
 * Komponente NIKAD ne zovu fetch izravno — samo ove funkcije (jedno mjesto za ugovor).
 */

export class ApiError extends Error {
  constructor(public status: number, path: string) {
    super(`API ${status}: ${path}`);
  }
}

async function get<T>(path: string, signal?: AbortSignal): Promise<T> {
  const res = await fetch(path, { signal, headers: { Accept: "application/json" } });
  if (!res.ok) throw new ApiError(res.status, path);
  return res.json() as Promise<T>;
}

export interface VendorListParams {
  q?: string;
  region?: string;
  category?: string;
  page?: number;
  pageSize?: number;
}

export function vendorListQuery(p: VendorListParams): string {
  const sp = new URLSearchParams();
  if (p.q) sp.set("q", p.q);
  if (p.region) sp.set("region", p.region);
  if (p.category) sp.set("category", p.category);
  if (p.page && p.page > 1) sp.set("page", String(p.page));
  if (p.pageSize) sp.set("pageSize", String(p.pageSize));
  return sp.toString();
}

export const api = {
  vendors: (p: VendorListParams, signal?: AbortSignal) =>
    get<Paged<Vendor>>(`/api/vendors?${vendorListQuery(p)}`, signal),

  pins: (p: { category: string; region?: string; q?: string }, signal?: AbortSignal) => {
    const sp = new URLSearchParams({ category: p.category });
    if (p.region) sp.set("region", p.region);
    if (p.q) sp.set("q", p.q);
    return get<PinsResult>(`/api/pins?${sp}`, signal);
  },

  regions: (category?: string, signal?: AbortSignal) =>
    get<RegionWithCount[]>(
      category ? `/api/regions?category=${encodeURIComponent(category)}` : "/api/regions",
      signal
    ),

  categories: (region?: string, signal?: AbortSignal) =>
    get<CategoryWithCount[]>(
      region ? `/api/categories?region=${encodeURIComponent(region)}` : "/api/categories",
      signal
    ),

  suggest: (q: string, signal?: AbortSignal) =>
    get<Suggestion[]>(`/api/suggest?q=${encodeURIComponent(q)}`, signal),

  budgetMatches: (params: URLSearchParams, signal?: AbortSignal) =>
    get<BudgetMatches>(`/api/budget-matches?${params}`, signal),

  /** Pružatelji po ID-ju (usporedba, favoriti). API prima max 50 po pozivu → dijelimo. */
  vendorsByIds: async (ids: string[], signal?: AbortSignal): Promise<Vendor[]> => {
    if (ids.length === 0) return [];
    const chunks: string[][] = [];
    for (let i = 0; i < ids.length; i += 50) chunks.push(ids.slice(i, i + 50));
    const pages = await Promise.all(
      chunks.map((c) =>
        get<Paged<Vendor>>(`/api/vendors?ids=${c.map(encodeURIComponent).join(",")}`, signal)
      )
    );
    return pages.flatMap((p) => p.items);
  },
};
