"use client";

import dynamic from "next/dynamic";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import SearchBar from "./SearchBar";
import VendorCard from "./VendorCard";
import { REGIONS } from "@/lib/data";
import { api } from "@/lib/api/client";
import { track } from "@/lib/analytics";
import { useBudget } from "@/stores";
import CategoryTile, { providersLabel } from "./CategoryTile";
import { HERO_IMAGE, LANDING_CATEGORIES, LANDING_TEXT as T } from "@/lib/landing";
import type { CategoryWithCount, RegionId, Vendor } from "@/lib/types";

const CroatiaMap = dynamic(() => import("./CroatiaMap"), {
  ssr: false,
  loading: () => <div className="skel" style={{ position: "absolute", inset: 0, borderRadius: 0 }} />,
});

interface Props {
  /** SSR: brojači kategorija (za pločice) */
  initialCategories?: CategoryWithCount[];
  /** SSR: najbolje ocijenjeni (po jedan iz lib/landing TOP_RATED_CATEGORIES) */
  topRated: Vendor[];
}

/**
 * Naslovnica — redizajn prema wireframeu 3a (bez sekcija stil / brojke / inspiracija /
 * newsletter, dodaju se nakon MVP-a). Hero s tražilicom → 6 foto-kategorija →
 * najbolje ocijenjeni → karta Hrvatske s regijama (primarni cilj #3).
 * Usporedba (#1) i budžet (#2) su nenametljivo dostupni iz heroa.
 */
export default function LandingShell({ initialCategories, topRated }: Props) {
  const router = useRouter();
  const openDrawer = useBudget((s) => s.openDrawer);

  const categoriesQ = useQuery({
    queryKey: ["categories", ""],
    queryFn: ({ signal }) => api.categories(undefined, signal),
    initialData: initialCategories,
  });
  const regionsQ = useQuery({
    queryKey: ["regions", ""],
    queryFn: ({ signal }) => api.regions(undefined, signal),
  });

  const counts = new Map(categoriesQ.data?.map((c) => [c.slug, c.count]));
  const regions = regionsQ.data ?? REGIONS.map((r) => ({ ...r, count: undefined as number | undefined }));

  const onMapRegion = (id: RegionId) => {
    track("map_region_clicked", { region: id });
    router.push(`/${id}`);
  };

  const heroStyle = HERO_IMAGE
    ? {
        backgroundImage: `linear-gradient(rgba(12, 28, 48, 0.42), rgba(12, 28, 48, 0.58)), url(${HERO_IMAGE})`,
      }
    : undefined;

  return (
    <main className="landing">
      {/* ---------------- hero ---------------- */}
      <section className={`lp-hero${HERO_IMAGE ? " has-photo" : ""}`} style={heroStyle}>
        <div className="container lp-hero-in">
          <h1>{T.heroTitle}</h1>
          <p className="lp-lead">{T.heroLead}</p>
          <SearchBar />
          <ul className="lp-perks">
            {/* <li>
              <span aria-hidden>€</span> {T.perkPrice}
            </li> */}
            <li>
              <Link href="/usporedba">
                <span aria-hidden>⇄</span> {T.perkCompare}
              </Link>
            </li>
            <li>
              <button type="button" onClick={openDrawer}>
                <span aria-hidden>🧮</span> {T.perkBudget}
              </button>
            </li>
          </ul>
        </div>
      </section>

      <div className="container">
        {/* ---------------- kategorije ---------------- */}
        <section className="lp-sec" aria-labelledby="lp-cats-title">
          <div className="lp-head">
            <h2 id="lp-cats-title">{T.categoriesTitle}</h2>
            <Link href="/kategorije" className="lp-more">
              {T.categoriesMore}
            </Link>
          </div>
          <ul className="ctiles">
            {LANDING_CATEGORIES.map((c, i) => (
              <li key={c.slug}>
                <CategoryTile
                  slug={c.slug}
                  label={c.label}
                  href={`/${c.slug}`}
                  count={counts.get(c.slug)}
                  priority={i < 3}
                />
              </li>
            ))}
          </ul>
        </section>

        {/* ---------------- najbolje ocijenjeni ---------------- */}
        {topRated.length > 0 && (
          <section className="lp-sec" aria-labelledby="lp-top-title">
            <div className="lp-head">
              <h2 id="lp-top-title">{T.topTitle}</h2>
              <span className="muted lp-note">{T.topNote}</span>
            </div>
            <div className="results-grid lp-top">
              {topRated.map((v) => (
                <VendorCard key={v.id} vendor={v} />
              ))}
            </div>
          </section>
        )}

        {/* ---------------- karta ---------------- */}
        <section className="lp-sec lp-map-sec" aria-labelledby="lp-map-title">
          <div className="lp-head">
            <h2 id="lp-map-title">{T.mapTitle}</h2>
          </div>
          <p className="muted lp-sub">
            {T.mapLead}
          </p>
          <div className="lp-map">
            <div className="map-wrap browse-map">
              <CroatiaMap vendors={[]} onRegionClick={onMapRegion} />
              <span className="map-hint">kliknite regiju</span>
            </div>
            <nav className="lp-regions" aria-label="Regije">
              {regions.map((r) => (
                <Link key={r.id} href={`/${r.id}`} className="region-item">
                  <span>{r.name}</span>
                  <span className="count">
                    {typeof r.count === "number" ? providersLabel(r.count) : ""}
                  </span>
                </Link>
              ))}
              <Link href="/kategorije" className="region-item lp-all">
                <span>{T.mapAll}</span>
              </Link>
            </nav>
          </div>
        </section>
      </div>
    </main>
  );
}
