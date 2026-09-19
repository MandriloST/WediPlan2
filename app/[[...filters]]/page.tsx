import { notFound } from "next/navigation";
import type { Metadata } from "next";
import ExploreShell from "@/components/ExploreShell";
import LandingShell from "@/components/LandingShell";
import { TOP_RATED_CATEGORIES } from "@/lib/landing";
import { pathFor, PAGE_SIZE, type ExploreFilters } from "@/lib/paths";
import { CATEGORY_BY_SLUG, REGION_BY_ID } from "@/lib/data";
import { getCategories, getVendors } from "@/lib/api/server";
import type { CategoryWithCount, Paged, RegionId, Vendor } from "@/lib/types";

interface Props {
  params: { filters?: string[] };
  searchParams: { q?: string; page?: string };
}

function parse(params: Props["params"], searchParams: Props["searchParams"]): ExploreFilters | null {
  const segs = params.filters ?? [];
  const q = typeof searchParams.q === "string" ? searchParams.q.trim().slice(0, 80) : "";
  const f: ExploreFilters = { q: q || undefined };
  if (segs.length > 2) return null;

  if (segs[0]) {
    if (segs[0] in REGION_BY_ID) f.region = segs[0] as RegionId;
    else if (CATEGORY_BY_SLUG[segs[0]]) f.category = segs[0];
    else return null;
  }
  if (segs[1]) {
    if (!f.region || !CATEGORY_BY_SLUG[segs[1]]) return null;
    f.category = segs[1];
  }
  if (searchParams.page !== undefined) {
    const n = Number(searchParams.page);
    if (!Number.isInteger(n) || n < 1 || n > 500) return null;
    if (n > 1) f.page = n;
  }
  return f;
}

export function generateMetadata({ params, searchParams }: Props): Metadata {
  const f = parse(params, searchParams);
  if (!f) return {};
  const regionName = f.region ? REGION_BY_ID[f.region].name : undefined;
  const catName = f.category ? CATEGORY_BY_SLUG[f.category].name : undefined;
  const parts = [catName, regionName].filter(Boolean);
  const base = parts.length ? parts.join(" · ") : undefined;
  const title = base
    ? `${base}${f.page ? ` — stranica ${f.page}` : ""} — Wediplan`
    : undefined;
  return {
    ...(title ? { title } : {}),
    ...(catName
      ? {
          description: `${catName}${regionName ? ` — ${regionName}` : " u Hrvatskoj"}: usporedite cijene, ocjene i lokacije na karti. Cijena uvijek vidljiva.`,
        }
      : {}),
    // tekst-pretrage su beskonačan prostor URL-ova → ne indeksirati, ali pratiti linkove
    ...(f.q
      ? { robots: { index: false, follow: true } }
      : { alternates: { canonical: pathFor({ region: f.region, category: f.category, page: f.page }) } }),
  };
}

/** Naslovnica (3a): kategorije za pločice + najbolji po ocjeni. Greške API-ja ne ruše stranicu. */
async function renderLanding() {
  const [cats, ...tops] = await Promise.allSettled([
    getCategories(),
    ...TOP_RATED_CATEGORIES.map((category) => getVendors({ category, pageSize: 1 })),
  ]);
  if (cats.status === "rejected") console.error("[landing] kategorije nisu dohvaćene:", cats.reason);
  const topRated = tops.flatMap((r) => (r.status === "fulfilled" ? r.value.items.slice(0, 1) : []));
  return (
    <LandingShell
      initialCategories={cats.status === "fulfilled" ? (cats.value as CategoryWithCount[]) : undefined}
      topRated={topRated as Vendor[]}
    />
  );
}

export default async function ExplorePage({ params, searchParams }: Props) {
  const filters = parse(params, searchParams);
  if (!filters) notFound();

  // "/" bez teksta i stranice = naslovnica; /regija ostaje grid kategorija (ExploreShell)
  if (!filters.region && !filters.category && !filters.q && !filters.page) return renderLanding();

  const browsing = !filters.category && !filters.q;
  let initialCategories: CategoryWithCount[] | undefined;
  let initialPage: Paged<Vendor> | undefined;

  // SSR prvog prikaza (SEO + brži prvi prikaz). Greška API-ja NE ruši stranicu —
  // klijent (TanStack Query) pokušava ponovno i prikazuje stanje greške.
  try {
    if (browsing) initialCategories = await getCategories(filters.region);
    else
      initialPage = await getVendors({
        region: filters.region,
        category: filters.category,
        q: filters.q,
        page: filters.page,
        pageSize: PAGE_SIZE,
      });
  } catch (e) {
    console.error("[explore] SSR dohvat nije uspio:", e);
  }

  // ?page=N iza zadnje stranice → pravi 404 (ne "soft 404" za crawlere)
  if (initialPage && (filters.page ?? 1) > 1 && initialPage.items.length === 0 && initialPage.total > 0)
    notFound();

  return (
    <ExploreShell filters={filters} initialCategories={initialCategories} initialPage={initialPage} />
  );
}
