"use client";

import dynamic from "next/dynamic";
import { useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { CATEGORIES, GROUP_LABELS } from "@/lib/data";
import { GROUP_ORDER } from "@/lib/budget";
import { api } from "@/lib/api/client";
import { pathFor } from "@/lib/paths";
import { useBudget } from "@/stores";
import { track } from "@/lib/analytics";

const CroatiaMap = dynamic(() => import("./CroatiaMap"), { ssr: false });

/** Mobilni tab "Karta" — category-first (§L): pinovi tek nakon odabira kategorije. */
export default function MapPageShell({ category }: { category?: string }) {
  const router = useRouter();
  const openDrawer = useBudget((s) => s.openDrawer);
  const { data, isFetching } = useQuery({
    queryKey: ["pins", category ?? "", "", ""],
    queryFn: ({ signal }) => api.pins({ category: category! }, signal),
    enabled: !!category,
    placeholderData: (prev) => prev,
  });

  return (
    <main
      className="map-wrap"
      style={{
        position: "fixed",
        inset: "62px 0 calc(var(--tab-h) + env(safe-area-inset-bottom)) 0",
        borderRadius: 0,
        border: 0,
        minHeight: 0,
      }}
    >
      <CroatiaMap
        vendors={category ? data?.items ?? [] : []}
        onRegionClick={(id) => {
          track("map_region_clicked", { region: id, category });
          router.push(pathFor({ region: id, category }));
        }}
      />
      <div className="map-catpick">
        <label htmlFor="map-cat" className="sr-only">
          Kategorija
        </label>
        <select
          id="map-cat"
          value={category ?? ""}
          onChange={(e) =>
            router.replace(e.target.value ? `/karta?kategorija=${e.target.value}` : "/karta")
          }
        >
          <option value="">Odaberite kategoriju…</option>
          {GROUP_ORDER.map((g) => (
            <optgroup key={g} label={GROUP_LABELS[g]}>
              {CATEGORIES.filter((c) => c.group === g).map((c) => (
                <option key={c.slug} value={c.slug}>
                  {c.name}
                </option>
              ))}
            </optgroup>
          ))}
        </select>
        {category && data && (
          <span className="map-catpick-n" aria-live="polite">
            {isFetching ? "…" : `${data.items.length} na karti`}
          </span>
        )}
      </div>
      {!category && (
        <div className="map-overlay" aria-hidden>
          Odaberite kategoriju — pružatelji će se pojaviti na karti
        </div>
      )}
      <button className="btn map-fab" onClick={openDrawer}>
        🧮 Budžet
      </button>
    </main>
  );
}
