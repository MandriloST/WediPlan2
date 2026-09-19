"use client";

import CategoryTile from "./CategoryTile";
import { CATEGORIES } from "@/lib/data";
import { GROUP_ORDER } from "@/lib/budget";
import type { CategoryWithCount } from "@/lib/types";

interface Props {
  /** brojači s API-ja; undefined = još se učitava (grid se odmah crta iz šifrarnika) */
  categories?: CategoryWithCount[];
  hrefFor: (slug: string) => string;
  regionName?: string;
}

/** Redoslijed pločica: po budžetskim omotnicama (prostor → hrana → foto → glazba → ostalo), bez naslova skupina. */
const ORDERED = GROUP_ORDER.flatMap((g) => CATEGORIES.filter((c) => c.group === g));

/**
 * Grid svih kategorija (/kategorije, /regija — §L): 29 foto-pločica (iste kao na naslovnici)
 * u jednoj neprekinutoj mreži (6 u redu na laptopu, responzivno niže). Kategorije bez pružatelja
 * ostaju vidljive (prigušene) — stranice postoje zbog SEO-a i dijeljenja linkova.
 */
export default function CategoryGrid({ categories, hrefFor, regionName }: Props) {
  const counts = new Map(categories?.map((c) => [c.slug, c.count]));
  const loading = !categories;

  return (
    <ul className="ctiles">
      {ORDERED.map((c) => {
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
              title={empty ? `Još nema pružatelja${regionName ? ` u regiji ${regionName}` : ""}` : c.name}
            />
          </li>
        );
      })}
    </ul>
  );
}
