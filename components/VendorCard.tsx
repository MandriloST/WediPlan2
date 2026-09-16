"use client";

import Image from "next/image";
import Link from "next/link";
import { CATEGORY_BY_SLUG } from "@/lib/data";
import { formatPrice, formatRating, euro, isOnRequest } from "@/lib/format";
import { estimateCost, isOverBudget, vendorGroup } from "@/lib/budget";
import type { Vendor } from "@/lib/types";
import { useMemo } from "react";
import { catsOf, useBudget, useCompare, useFavorites } from "@/stores";
import { coverImage } from "@/lib/images";
import { track } from "@/lib/analytics";
import { vendorBadges } from "@/lib/badges";
import { GROUP_LABELS, COUNTRY_LABELS } from "@/lib/data";

/** "Split" | "Sarajevo (BiH)" | "pokriva regiju" — kratko za karticu. */
function homeLabelShort(v: Vendor): string {
  const country = v.country && v.country !== "hr" ? COUNTRY_LABELS[v.country] ?? v.country.toUpperCase() : "";
  if (!v.city) return country || "pokriva regiju";
  return country ? `${v.city} (${country})` : v.city;
}
import { canAddToCompare, COMPARE_INCOMPATIBLE_HINT } from "@/lib/categories";

export default function VendorCard({ vendor }: { vendor: Vendor }) {
  const { ids, meta, toggle } = useCompare();
  const cats = useMemo(() => catsOf(meta), [meta]);
  const favorites = useFavorites();
  const plan = useBudget((s) => s.plan);
  const over = isOverBudget(vendor, plan);
  const cat = CATEGORY_BY_SLUG[vendor.category];
  const checked = ids.includes(vendor.id);
  const compareDisabled = !checked && !canAddToCompare(vendor, ids, cats);
  const fav = favorites.ids.includes(vendor.id);
  const img = coverImage(vendor);

  return (
    <article className={`vcard${over ? " over" : ""}`}>
      <div className="thumb-img">
        <Link href={`/pruzatelj/${vendor.slug}`} style={{ position: 'relative', width: '86px', height: '86px', display: 'block' }}>
            <Image src={img.src} alt={vendor.name} fill sizes="86px" style={{ objectFit: "cover" }} />
         </Link>
      </div>
      <div className="info">
        <div>
          <Link href={`/pruzatelj/${vendor.slug}`} className="name" style={{ color: "inherit" }}>
            {vendor.name}
          </Link>{" "}
          <span className="city">· {homeLabelShort(vendor)}</span>
        </div>
        <div className="row2">
          <span className={isOnRequest(vendor.price) ? "price-upit" : "price"}>{formatPrice(vendor.price)}</span>{" "}
          {vendor.reviewCount > 0 && (
            <span className="rating">
              · <span className="star">★</span> {formatRating(vendor.rating)}
              <span className="city"> ({vendor.reviewCount})</span>
            </span>
          )}
        </div>
        <div className="badges">
          {vendorBadges(vendor).map((b) => (
            <span key={b.id} className={`badge ${b.className}`} title={b.tooltip}>
              {b.label}
            </span>
          ))}
          {vendor.liveCalendar ? (
            <span className="badge live">✓ kalendar uživo</span>
          ) : (
            <span className="badge">na upit</span>
          )}
          {over && plan && (
            <span className="badge over-cap">
              izvan budžeta — {GROUP_LABELS[vendorGroup(vendor)]} ≤ {euro(plan.caps[vendorGroup(vendor)])}
            </span>
          )}
          {!over && cat?.short && <span className="badge">{cat.short}</span>}
        </div>
        <label
          className={`compare-box${compareDisabled ? " disabled" : ""}`}
          title={compareDisabled ? COMPARE_INCOMPATIBLE_HINT : undefined}
        >
          <input
            type="checkbox"
            checked={checked}
            disabled={compareDisabled}
            onChange={() => toggle(vendor)}
          />
          usporedi
        </label>
      </div>
      <button
        className={`fav${fav ? " on" : ""}`}
        aria-label={fav ? "Ukloni iz favorita" : "Dodaj u favorite"}
        aria-pressed={fav}
        onClick={() => {
          if (!fav) track("favorite_added", { slug: vendor.slug, category: vendor.category });
          favorites.toggle(vendor.id);
        }}
      >
        {fav ? "♥" : "♡"}
      </button>
    </article>
  );
}
