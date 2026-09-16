"use client";

import dynamic from "next/dynamic";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useRef } from "react";
import { useInfiniteQuery, useQuery } from "@tanstack/react-query";
import { cleanQuery, track } from "@/lib/analytics";
import SearchBar from "./SearchBar";
import VendorCard from "./VendorCard";
import CategoryGrid from "./CategoryGrid";
import { CATEGORIES, CATEGORY_BY_SLUG, REGIONS } from "@/lib/data";
import { api } from "@/lib/api/client";
import type { CategoryWithCount, Paged, PinVendor, RegionId, Vendor } from "@/lib/types";
import { useBudget } from "@/stores";
import { pathFor, type ExploreFilters } from "@/lib/paths";

const CroatiaMap = dynamic(() => import("./CroatiaMap"), {
  ssr: false,
  loading: () => <div className="skel" style={{ position: "absolute", inset: 0, borderRadius: 0 }} />,
});

export type { ExploreFilters };

interface Props {
  filters: ExploreFilters;
  /** SSR: brojači kategorija (landing) */
  initialCategories?: CategoryWithCount[];
  /** SSR: prva prikazana stranica rezultata (crawleri vide kartice u HTML-u) */
  initialPage?: Paged<Vendor>;
}

export default function ExploreShell({ filters, initialCategories, initialPage }: Props) {
  const router = useRouter();
  const openDrawer = useBudget((s) => s.openDrawer);
  const go = (next: ExploreFilters) => router.push(pathFor(next));

  const { region, category, q } = filters;
  const startPage = Math.max(1, filters.page ?? 1);
  const browsing = !category && !q; // §L: bez kategorije i teksta → grid, bez liste i pinova
  const cat = category ? CATEGORY_BY_SLUG[category] : undefined;
  const regionName = region ? REGIONS.find((r) => r.id === region)?.name : undefined;

  /* ---------------- podaci ---------------- */

  const categoriesQ = useQuery({
    queryKey: ["categories", region ?? ""],
    queryFn: ({ signal }) => api.categories(region, signal),
    enabled: browsing,
    initialData: browsing ? initialCategories : undefined,
  });

  const regionsQ = useQuery({
    queryKey: ["regions", category ?? ""],
    queryFn: ({ signal }) => api.regions(category, signal),
  });

  const listQ = useInfiniteQuery({
    queryKey: ["vendors", region ?? "", category ?? "", q ?? "", startPage],
    queryFn: ({ pageParam, signal }) =>
      api.vendors({ region, category, q, page: pageParam }, signal),
    initialPageParam: startPage,
    getNextPageParam: (last) =>
      last.page * last.pageSize < last.total ? last.page + 1 : undefined,
    enabled: !browsing,
    initialData: !browsing && initialPage
      ? { pages: [initialPage], pageParams: [startPage] }
      : undefined,
  });

  const pinsQ = useQuery({
    queryKey: ["pins", category ?? "", region ?? "", q ?? ""],
    queryFn: ({ signal }) => api.pins({ category: category!, region, q }, signal),
    enabled: !!category,
    placeholderData: (prev) => prev, // promjena regije ne prazni kartu dok stižu pinovi
  });

  const items = listQ.data?.pages.flatMap((p) => p.items) ?? [];
  const total = listQ.data?.pages[0]?.total;
  const shownTo = (startPage - 1) * (listQ.data?.pages[0]?.pageSize ?? 24) + items.length;

  // karta: kategorija → svi pinovi kategorije (pins endpoint);
  // samo tekst → pinovi učitanih rezultata; landing → samo regije
  const mapVendors: PinVendor[] = category ? pinsQ.data?.items ?? [] : q ? items : [];
  const withoutPin =
    category && total !== undefined && pinsQ.data ? Math.max(0, total - pinsQ.data.total) : 0;

  /* ---------------- analitika (§A) ---------------- */

  // jedna "pretraga" = jedan skup filtara u načinu rezultata; šalje se kad je poznat broj
  // rezultata (izvještaj "pretrage bez rezultata"). Stranica/"Učitaj još" se ne broje ponovno.
  const searchKey = browsing ? "" : `${region ?? ""}|${category ?? ""}|${q ?? ""}`;
  const trackedSearch = useRef("");
  useEffect(() => {
    if (!searchKey || total === undefined || trackedSearch.current === searchKey) return;
    trackedSearch.current = searchKey;
    track("search_performed", { q: cleanQuery(q), category, region, results: total });
  }, [searchKey, total, q, category, region]);

  const onMapRegion = (id: RegionId) => {
    track("map_region_clicked", { region: id, category });
    go({ category, q, region: id });
  };

  /* ---------------- dijelovi ---------------- */

  const regionList = (
    <>
      <button
        className={`region-item${!region ? " active" : ""}`}
        onClick={() => go({ category, q })}
      >
        <span>Cijela Hrvatska</span>
      </button>
      {(regionsQ.data ?? REGIONS.map((r) => ({ ...r, count: undefined as number | undefined }))).map((r) => (
        <button
          key={r.id}
          className={`region-item${region === r.id ? " active" : ""}`}
          onClick={() => go({ category, q, region: region === r.id ? undefined : r.id })}
        >
          <span>{r.name}</span>
          <span className="count">{r.count ?? ""}</span>
        </button>
      ))}
    </>
  );

  const map = (
    <div className={`map-wrap${browsing ? " browse-map" : ""}`}>
      <CroatiaMap
        vendors={mapVendors}
        selectedRegion={region}
        onRegionClick={onMapRegion}
      />
      {browsing ? (
        <div className="map-overlay" aria-hidden>
          Odaberite kategoriju — pružatelji će se pojaviti na karti
        </div>
      ) : (
        <span className="map-hint">
          {pinsQ.data && pinsQ.data.total > pinsQ.data.items.length
            ? `karta prikazuje ${pinsQ.data.items.length} od ${pinsQ.data.total} — suzite regiju`
            : "kliknite regiju ili pin — lista i karta su povezane"}
        </span>
      )}
      <button className="btn map-fab" onClick={openDrawer}>
        🧮 Budžet
      </button>
    </div>
  );

  /* ---------------- landing (category-first) ---------------- */

  if (browsing) {
    return (
      <main>
        <div className="container">
          <section className="hero">
            <h1>
              Pronađite <em>sve</em> za vjenčanje {regionName ? `— ${regionName}` : "u Hrvatskoj"}
            </h1>
            <SearchBar key={`s-${region ?? ""}`} initialRegion={region} />
          </section>

          <section className="browse" aria-labelledby="browse-title">
            <div className="browse-head">
              <h2 id="browse-title">Kategorije</h2>
              <div className="region-chips" role="group" aria-label="Regija">
                <button className={`chip${!region ? " active" : ""}`} onClick={() => go({})}>
                  Cijela Hrvatska
                </button>
                {(regionsQ.data ?? REGIONS).map((r) => (
                  <button
                    key={r.id}
                    className={`chip${region === r.id ? " active" : ""}`}
                    onClick={() => go({ region: region === r.id ? undefined : r.id })}
                  >
                    {r.name}
                    {"count" in r && typeof r.count === "number" && r.count > 0 ? (
                      <span className="chip-n">{r.count}</span>
                    ) : null}
                  </button>
                ))}
              </div>
            </div>
            {categoriesQ.isError ? (
              <p className="muted">Brojači trenutno nisu dostupni — kategorije i dalje rade.</p>
            ) : null}
            <CategoryGrid
              categories={categoriesQ.data}
              regionName={regionName}
              hrefFor={(slug) => pathFor({ region, category: slug })}
            />
          </section>

          <section className="explore single" aria-label="Karta Hrvatske">
            {map}
          </section>
        </div>
      </main>
    );
  }

  /* ---------------- rezultati (jedna kategorija ili tekst) ---------------- */

  const siblings = cat ? CATEGORIES.filter((c) => c.group === cat.group) : [];
  const heading = [cat?.name ?? `„${q}”`, regionName].filter(Boolean).join(" · ");
  const params = listQ.data?.pageParams ?? [];
  const nextPage = listQ.hasNextPage ? (params[params.length - 1] as number) + 1 : undefined;

  return (
    <main>
      <div className="container">
        <section className="hero compact">
          <h1>{heading}</h1>
          <SearchBar
            key={`s-${region ?? ""}-${category ?? ""}-${q ?? ""}`}
            initialQ={q}
            initialRegion={region}
            category={category}
          />
        </section>

        <nav className="catbar" aria-label="Kategorije">
          <Link className="back" href={pathFor({ region })}>
            ← Sve kategorije
          </Link>
          {cat ? (
            <div className="chips">
              {siblings.map((c) => (
                <Link
                  key={c.slug}
                  href={pathFor({ region, category: c.slug })}
                  className={`chip${c.slug === category ? " active" : ""}`}
                  aria-current={c.slug === category ? "page" : undefined}
                >
                  {c.short ?? c.name}
                </Link>
              ))}
            </div>
          ) : (
            <span className="muted">pretraga po svim kategorijama</span>
          )}
        </nav>

        <section className="explore">
          <aside className="sidebar">
            <p className="lead">Regija:</p>
            {regionList}
          </aside>
          {map}
        </section>

        <section aria-live="polite">
          <div className="results-head">
            <h2>{total !== undefined ? `${total} ${total === 1 ? "rezultat" : "rezultata"}` : "Rezultati"}</h2>
            <span className="meta">
              cijena uvijek vidljiva
              {withoutPin > 0 && ` · ${withoutPin} bez točne lokacije (nisu na karti)`}
            </span>
          </div>

          {startPage > 1 && (
            <p className="pager-prev">
              <Link rel="prev" href={pathFor({ ...filters, page: startPage - 1 })}>
                ← Prethodna stranica
              </Link>{" "}
              · <Link href={pathFor({ ...filters, page: undefined })}>prva stranica</Link>
            </p>
          )}

          <div className="results-grid">
            {listQ.isLoading &&
              Array.from({ length: 6 }).map((_, i) => <div key={i} className="skel" />)}
            {items.map((v) => (
              <VendorCard key={v.id} vendor={v} />
            ))}
            {listQ.isFetchingNextPage &&
              Array.from({ length: 3 }).map((_, i) => <div key={`n${i}`} className="skel" />)}

            {listQ.isError && items.length === 0 && (
              <div className="empty">
                <h3>Podaci trenutno nisu dostupni</h3>
                <p>Pokušajte ponovno za trenutak.</p>
                <div className="actions">
                  <button className="btn btn-primary btn-sm" onClick={() => listQ.refetch()}>
                    Pokušaj ponovno
                  </button>
                </div>
              </div>
            )}

            {listQ.isSuccess && items.length === 0 && (
              <div className="empty">
                <h3>Nema pružatelja za ovaj odabir</h3>
                <p>Pokušajte proširiti regiju ili ukloniti filtar.</p>
                <div className="actions">
                  {region && (
                    <button className="btn btn-sm" onClick={() => go({ category, q })}>
                      Cijela Hrvatska
                    </button>
                  )}
                  {q && category && (
                    <button className="btn btn-sm" onClick={() => go({ region, q })}>
                      Traži „{q}” u svim kategorijama
                    </button>
                  )}
                  {q && (
                    <button className="btn btn-sm" onClick={() => go({ region, category })}>
                      Očisti pretragu
                    </button>
                  )}
                  <Link className="btn btn-sm" href={pathFor({ region })}>
                    Sve kategorije
                  </Link>
                </div>
              </div>
            )}
          </div>

          {nextPage !== undefined && (
            <div className="load-more">
              {/* pravi link (rel=next) za crawlere; klik učitava ispod bez navigacije */}
              <a
                className="btn"
                rel="next"
                href={pathFor({ ...filters, page: nextPage })}
                aria-busy={listQ.isFetchingNextPage}
                onClick={(e) => {
                  e.preventDefault();
                  if (!listQ.isFetchingNextPage) listQ.fetchNextPage();
                }}
              >
                {listQ.isFetchingNextPage ? "Učitavam…" : "Učitaj još"}
              </a>
              {total !== undefined && (
                <span className="muted">
                  prikazano {shownTo} od {total}
                </span>
              )}
            </div>
          )}
        </section>
      </div>
    </main>
  );
}
