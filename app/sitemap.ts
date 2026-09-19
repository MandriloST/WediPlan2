import type { MetadataRoute } from "next";
import { CATEGORIES, REGIONS } from "@/lib/data";
import { getSitemapEntries } from "@/lib/api/server";
import { SITE_URL } from "@/lib/site";

// Osvježava se svakih sat vremena. Ako API nije dostupan (npr. build prije nego je
// backend hostan), sitemap i dalje sadrži sve stranice kategorija/regija.
export const revalidate = 3600;

export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const now = new Date();
  const urls: MetadataRoute.Sitemap = [
    { url: SITE_URL, lastModified: now, priority: 1 },
    { url: `${SITE_URL}/kategorije`, lastModified: now, priority: 0.9 },
    { url: `${SITE_URL}/budzet`, lastModified: now, priority: 0.6 },
  ];
  for (const r of REGIONS) urls.push({ url: `${SITE_URL}/${r.id}`, lastModified: now, priority: 0.8 });
  for (const c of CATEGORIES) urls.push({ url: `${SITE_URL}/${c.slug}`, lastModified: now, priority: 0.7 });
  for (const r of REGIONS)
    for (const c of CATEGORIES)
      urls.push({ url: `${SITE_URL}/${r.id}/${c.slug}`, lastModified: now, priority: 0.5 });

  try {
    for (const v of await getSitemapEntries())
      urls.push({
        url: `${SITE_URL}/pruzatelj/${v.slug}`,
        lastModified: v.updatedAt ? new Date(v.updatedAt) : now,
        priority: 0.6,
      });
  } catch (e) {
    console.error("[sitemap] API nedostupan — profili izostavljeni:", e);
  }
  return urls;
}
