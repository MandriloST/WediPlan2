"use client";

import CategoryTile from "./CategoryTile";
import { CATEGORIES } from "@/lib/data";
import { GROUP_ORDER } from "@/lib/budget";
import type { BudgetGroup, CategoryWithCount } from "@/lib/types";

/** Naslovi budžetskih omotnica u gridu (UI copy; grupe = lib/data CATEGORIES.group). */
const GROUP_TITLES: Record<BudgetGroup, string> = {
  sala: "Prostor za proslavu",
  catering: "Hrana i torte",
  foto: "Foto i video",
  glazba: "Glazba",
  ostalo: "Sve ostalo za vaš dan",
};

interface Props {
  /** brojači s API-ja; undefined = još se učitava (grid se odmah crta iz šifrarnika) */
  categories?: CategoryWithCount[];
  hrefFor: (slug: string) => string;
  regionName?: string;
}

/**
 * Grid svih kategorija (/kategorije, /regija — §L): 29 foto-pločica (iste kao na naslovnici)
 * grupiranih po budžetskim omotnicama. Kategorije bez pružatelja ostaju vidljive (prigušene) — stranice
 * postoje zbog SEO-a i dijeljenja linkova, a prazno stanje tamo nudi širenje regije.
 */
export default function CategoryGrid({ categories, hrefFor, regionName }: Props) {
  const counts = new Map(categories?.map((c) => [c.slug, c.count]));
  const loading = !categories;

  return (
    <div className="cat-groups">
      {GROUP_ORDER.map((g) => {
        const items = CATEGORIES.filter((c) => c.group === g);
        const groupTotal = items.reduce((n, c) => n + (counts.get(c.slug) ?? 0), 0);
        return (
          <section key={g} className={`cat-group g-${g}`} aria-labelledby={`cg-${g}`}>
            <h3 id={`cg-${g}`}>{GROUP_TITLES[g]}</h3>
            <ul className="ctiles">
              {items.map((c) => {
                const n = counts.get(c.slug);
                const empty = !loading && !n;
                return (
                  <li key={c.slug}>
                    <CategoryTile
                      slug={c.slug}
                      label={c.name}
                      href={hrefFor(c.slug)}
                      count={loading ? undefined : n ?? 0}
                      empty={empty}
                      title={
                        empty
                          ? `Još nema pružatelja${regionName ? ` u regiji ${regionName}` : ""}`
                          : c.name
                      }
                    />
                  </li>
                );
              })}
            </ul>
            {!loading && groupTotal === 0 && regionName && (
              <p className="cat-group-note">U regiji {regionName} za ovu skupinu još nema pružatelja.</p>
            )}
          </section>
        );
      })}
    </div>
  );
}
