"use client";

import Image from "next/image";
import Link from "next/link";
import { useMemo } from "react";
import VendorCard from "./VendorCard";
import { CATEGORY_BY_SLUG } from "@/lib/data";
import { formatPrice, formatRating, isOnRequest } from "@/lib/format";
import { vendorImages } from "@/lib/images";
import { vendorBadges } from "@/lib/badges";
import { track } from "@/lib/analytics";
import { canAddToCompare, COMPARE_INCOMPATIBLE_HINT } from "@/lib/categories";
import { catsOf, useCompare, useFavorites } from "@/stores";
import { splitTiers, type FeaturedItem, type FeaturedKind } from "@/lib/featured";
import { LANDING_TEXT as T } from "@/lib/landing";

function badgeLabel(kind: FeaturedKind): string {
  return kind === "sponsored" ? T.sponsoredBadge : T.featuredBadge;
}

/**
 * "Izdvajamo" na naslovnici — 1 veliki, 2 srednja, 3 mala (mali = ista VendorCard kao
 * "Najbolje ocijenjeni"). Podaci: data/featured.json → lib/featured-server (SSR).
 * Nema aktivnih unosa → sekcija se ne prikazuje.
 */
export default function FeaturedSection({ items }: { items: FeaturedItem[] }) {
  if (items.length === 0) return null;
  const { hero, medium, small } = splitTiers(items);
  const anySponsored = items.some((i) => i.kind === "sponsored");

  return (
    <section className="lp-sec" aria-labelledby="lp-feat-title">
      <div className="lp-head">
        <h2 id="lp-feat-title">{T.featuredTitle}</h2>
        <span className="muted lp-note">{anySponsored ? T.featuredNoteSponsored : T.featuredNoteEditorial}</span>
      </div>

      {hero && <FeaturedCard item={hero} size="hero" />}

      {medium.length > 0 && (
        <div className="lp-feat-mid">
          {medium.map((it) => (
            <FeaturedCard key={it.vendor.id} item={it} size="medium" />
          ))}
        </div>
      )}

      {small.length > 0 && (
        <div className="results-grid lp-top lp-feat-small">
          {small.map((it) => (
            <VendorCard
              key={it.vendor.id}
              vendor={it.vendor}
              highlight={badgeLabel(it.kind)}
              highlightClass={it.kind === "sponsored" ? "sponsored" : "feat"}
            />
          ))}
        </div>
      )}
    </section>
  );
}

function FeaturedCard({ item, size }: { item: FeaturedItem; size: "hero" | "medium" }) {
  const { vendor, about, kind } = item;
  const { ids, meta, toggle } = useCompare();
  const cats = useMemo(() => catsOf(meta), [meta]);
  const favorites = useFavorites();
  const checked = ids.includes(vendor.id);
  const compareDisabled = !checked && !canAddToCompare(vendor, ids, cats);
  const fav = favorites.ids.includes(vendor.id);
  // "profile" default je široka slika (banner) — bolja za velike kartice od 240px sličice
  const img = vendorImages(vendor, "profile")[0];
  const cat = CATEGORY_BY_SLUG[vendor.category];
  const href = `/pruzatelj/${vendor.slug}`;
  const place = vendor.city || "pokriva regiju";

  return (
    <article className={`fcard fcard-${size}`}>
      <Link href={href} className="fcard-img" tabIndex={-1} aria-hidden="true">
        <Image
          src={img.src}
          alt=""
          fill
          sizes={size === "hero" ? "(max-width: 1200px) 100vw, 1200px" : "(max-width: 900px) 100vw, 600px"}
          style={{ objectFit: "cover" }}
        />
      </Link>
      <button
        className={`fcard-fav${fav ? " on" : ""}`}
        aria-label={fav ? "Ukloni iz favorita" : "Dodaj u favorite"}
        aria-pressed={fav}
        onClick={() => {
          if (!fav) track("favorite_added", { slug: vendor.slug, category: vendor.category });
          favorites.toggle(vendor.id);
        }}
      >
        {fav ? "♥" : "♡"}
      </button>

      <div className="fcard-body">
        <div className="fcard-meta">
          <span className={`badge ${kind === "sponsored" ? "sponsored" : "feat"}`}>{badgeLabel(kind)}</span>
          <span className="muted">
            {cat?.name ?? vendor.category} · {place}
          </span>
        </div>
        <h3 className="fcard-name">
          <Link href={href}>{vendor.name}</Link>
        </h3>
        {size === "hero" && about && <p className="fcard-about">{about}</p>}
        <div className="fcard-row2">
          <span className={isOnRequest(vendor.price) ? "price-upit" : "price"}>{formatPrice(vendor.price)}</span>
          {vendor.reviewCount > 0 && (
            <span className="rating">
              {" "}
              · <span className="star">★</span> {formatRating(vendor.rating)}
              <span className="muted"> ({vendor.reviewCount})</span>
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
        </div>
        <div className="fcard-actions">
          {size === "hero" ? (
            <Link href={href} className="btn btn-primary">
              Pogledaj profil
            </Link>
          ) : (
            <Link href={href} className="fcard-more">
              Profil →
            </Link>
          )}
          <label
            className={`compare-box${compareDisabled ? " disabled" : ""}`}
            title={compareDisabled ? COMPARE_INCOMPATIBLE_HINT : undefined}
          >
            <input type="checkbox" checked={checked} disabled={compareDisabled} onChange={() => toggle(vendor)} />
            usporedi
          </label>
        </div>
      </div>
    </article>
  );
}
