import "server-only";
import type {
  CategoryWithCount,
  Paged,
  SitemapEntry,
  Vendor,
  VendorProfileData,
} from "@/lib/types";
import { vendorListQuery, type VendorListParams } from "./client";

/**
 * Serverski pristup podacima (server komponente, sitemap, metadata).
 *
 * API_URL (server-only env, npr. http://localhost:5080 ili https://api.wediplan.hr):
 *  - postavljen  → fetch na .NET (apsolutni URL; Next data cache s revalidate),
 *  - nije        → izravni poziv mock funkcija (lib/mock), bez HTTP-a.
 * Isti prekidač koristi next.config.mjs za rewrites, pa su klijent i server uvijek
 * na istom izvoru.
 */
export const API_URL = process.env.API_URL?.replace(/\/$/, "") || "";
export const usingRealApi = API_URL !== "";

/** Koliko dugo (s) server kešira odgovore. Liste kratko, profili/sitemap dulje. */
const TTL = { list: 60, profile: 300, sitemap: 3600 } as const;

class NotFound extends Error {}

async function apiGet<T>(path: string, revalidate: number): Promise<T> {
  const res = await fetch(`${API_URL}${path}`, {
    headers: { Accept: "application/json" },
    next: { revalidate },
  });
  if (res.status === 404) throw new NotFound(path);
  if (!res.ok) throw new Error(`API ${res.status}: ${path}`);
  return res.json() as Promise<T>;
}

export async function getVendors(p: VendorListParams): Promise<Paged<Vendor>> {
  if (usingRealApi) return apiGet(`/api/vendors?${vendorListQuery(p)}`, TTL.list);
  const { queryVendors } = await import("@/lib/mock/search");
  return queryVendors(p);
}

export async function getCategories(region?: string): Promise<CategoryWithCount[]> {
  if (usingRealApi)
    return apiGet(
      region ? `/api/categories?region=${encodeURIComponent(region)}` : "/api/categories",
      TTL.list
    );
  const { categoriesWithCounts } = await import("@/lib/mock/search");
  return categoriesWithCounts(region);
}

/** null = pružatelj ne postoji / skriven / opt-out (→ notFound()). */
export async function getProfile(slug: string): Promise<VendorProfileData | null> {
  if (usingRealApi) {
    try {
      return await apiGet<VendorProfileData>(`/api/vendors/${encodeURIComponent(slug)}`, TTL.profile);
    } catch (e) {
      if (e instanceof NotFound) return null;
      throw e;
    }
  }
  const { getProfile: mock } = await import("@/lib/mock/profile");
  return mock(slug);
}

/**
 * "Slično u kategoriji": ista primarna kategorija, prvo ista regija, pa ostatak HR.
 * Dva mala upita (pageSize ≤ 4) umjesto posebnog endpointa. Greška → prazno (sporedni blok).
 */
export async function getSimilar(vendor: Vendor, limit = 3): Promise<Vendor[]> {
  try {
    const seen = new Set([vendor.id]);
    const out: Vendor[] = [];
    const take = (items: Vendor[]) => {
      for (const v of items) {
        if (out.length >= limit) break;
        if (seen.has(v.id)) continue;
        seen.add(v.id);
        out.push(v);
      }
    };
    if (vendor.region)
      take((await getVendors({ category: vendor.category, region: vendor.region, pageSize: limit + 1 })).items);
    if (out.length < limit)
      take((await getVendors({ category: vendor.category, pageSize: limit + out.length + 1 })).items);
    return out;
  } catch {
    return [];
  }
}

export async function getSitemapEntries(): Promise<SitemapEntry[]> {
  if (usingRealApi) return apiGet("/api/sitemap", TTL.sitemap);
  const { sitemapEntries } = await import("@/lib/mock/search");
  return sitemapEntries();
}
