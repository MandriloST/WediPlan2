"use client";

import Image from "next/image";
import Link from "next/link";
import { CATEGORY_BY_SLUG, GROUP_LABELS } from "@/lib/data";
import { breadcrumb } from "@/lib/profile";
import { euro, formatPrice, formatRating, isOnRequest } from "@/lib/format";
import { estimateCost, isOverBudget, vendorGroup } from "@/lib/budget";
import type { Vendor, VendorProfileData } from "@/lib/types";
import { useEffect, useMemo } from "react";
import { track, trackNow } from "@/lib/analytics";
import { catsOf, useBudget, useCompare, useFavorites } from "@/stores";
import AvailabilityCalendar from "./AvailabilityCalendar";
import { vendorImages } from "@/lib/images";
import { vendorBadges } from "@/lib/badges";
import VendorCard from "./VendorCard";
import ReviewForm from "./ReviewForm";
import ClaimPanel from "./ClaimPanel";
import OptOutLink from "./OptOutLink";
import { homeLabel } from "@/lib/data";
import {
  canAddToCompare,
  COMPARE_INCOMPATIBLE_HINT,
  extraCategories,
} from "@/lib/categories";

export default function VendorProfile({ data, similar }: { data: VendorProfileData; similar: Vendor[] }) {
  const { vendor, about, services, importedReviews, userReviews } = data;
  const { ids, meta, toggle } = useCompare();
  const cats = useMemo(() => catsOf(meta), [meta]);
  const compareDisabled = !ids.includes(vendor.id) && !canAddToCompare(vendor, ids, cats);

  // §A: vendor_viewed jednom po profilu (osnova za "Statistiku profila", §M.1)
  useEffect(() => {
    track("vendor_viewed", { slug: vendor.slug, category: vendor.category, region: vendor.region });
  }, [vendor.slug, vendor.category, vendor.region]);

  const outbound = (target: "instagram" | "facebook" | "web") => () =>
    trackNow("outbound_click", { slug: vendor.slug, category: vendor.category, target });
  const favorites = useFavorites();
  const plan = useBudget((s) => s.plan);

  const cat = CATEGORY_BY_SLUG[vendor.category] ?? { slug: vendor.category, name: vendor.category, group: "ostalo" };
  const over = isOverBudget(vendor, plan);
  const fav = favorites.ids.includes(vendor.id);
  const images = vendorImages(vendor);

  return (
    <main className="container page profile">
      <nav className="crumbs" aria-label="Navigacijska putanja">
        {breadcrumb(vendor).map((c) => (
          <span key={c.href}>
            <Link href={c.href}>{c.label}</Link> ›{" "}
          </span>
        ))}
        <span aria-current="page">{vendor.name}</span>
      </nav>

      <div className={`gallery g${images.length}`} aria-label="Fotografije">
        {images.map((im, i) => (
          <div key={im.src} className={`gimg${i === 0 ? " main" : ""}`}>
            <Image
              src={im.src}
              alt={im.isDefault ? `${vendor.name} — ilustracija` : vendor.name}
              fill
              priority={i === 0}
              sizes="(max-width: 900px) 100vw, 60vw"
              style={{ objectFit: "cover" }}
            />
          </div>
        ))}
      </div>

      <div className="profile-grid">
        <div className="profile-main">
          <header className="profile-head">
            <div>
              <h1>{vendor.name}</h1>
              <p className="sub" style={{ margin: "2px 0 0" }}>
                {cat.name}
                {(() => {
                  const extra = extraCategories(vendor)
                    .map((s) => CATEGORY_BY_SLUG[s]?.short ?? CATEGORY_BY_SLUG[s]?.name)
                    .filter(Boolean);
                  return extra.length ? ` · također: ${extra.join(", ")}` : "";
                })()}
                {" · "}{homeLabel(vendor)}
                {vendor.coverage === "hr" && " · pokriva cijelu Hrvatsku"}
                {vendor.coverageNote ? ` · ${vendor.coverageNote}` : ""}
              </p>
              <div className="badges" style={{ marginTop: 8 }}>
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
                {vendor.styleTags.map((t) => (
                  <span key={t} className="badge">
                    {t}
                  </span>
                ))}
                <Link href="/oznake" className="badge-help" title="Javni kriteriji dodjele oznaka">
                  Što znače oznake?
                </Link>
              </div>
              {(vendor.social?.instagram || vendor.social?.facebook) && (
                <div className="socials" style={{ marginTop: 10 }} aria-label="Društvene mreže">
                  {vendor.social?.instagram && (
                    <a
                      className="social-ico"
                      href={vendor.social.instagram}
                      onClick={outbound("instagram")}
                      onAuxClick={outbound("instagram")}
                      target="_blank"
                      rel="noopener noreferrer nofollow"
                      aria-label={`${vendor.name} na Instagramu`}
                      title="Instagram"
                    >
                      <svg viewBox="0 0 24 24" width="20" height="20" fill="currentColor" aria-hidden="true">
                        <path d="M12 2c2.72 0 3.06.01 4.12.06 1.07.05 1.8.22 2.43.47.66.25 1.22.6 1.77 1.15.55.55.9 1.11 1.15 1.77.25.63.42 1.36.47 2.43.05 1.07.06 1.41.06 4.13 0 2.72-.01 3.06-.06 4.13-.05 1.07-.22 1.8-.47 2.43a4.9 4.9 0 0 1-1.15 1.77c-.55.55-1.11.9-1.77 1.15-.63.25-1.36.42-2.43.47-1.07.05-1.41.06-4.13.06-2.72 0-3.06-.01-4.13-.06-1.07-.05-1.8-.22-2.43-.47a4.9 4.9 0 0 1-1.77-1.15 4.9 4.9 0 0 1-1.15-1.77c-.25-.63-.42-1.36-.47-2.43C2.01 15.06 2 14.72 2 12c0-2.72.01-3.06.06-4.12.05-1.07.22-1.8.47-2.43.25-.66.6-1.22 1.15-1.77.55-.55 1.11-.9 1.77-1.15.63-.25 1.36-.42 2.43-.47C8.94 2.01 9.28 2 12 2Zm0 1.8c-2.67 0-2.99.01-4.04.06-.98.04-1.5.21-1.86.35-.47.18-.8.4-1.15.75-.35.35-.57.68-.75 1.15-.14.36-.31.88-.35 1.86-.05 1.05-.06 1.37-.06 4.04s.01 2.99.06 4.04c.04.98.21 1.5.35 1.86.18.47.4.8.75 1.15.35.35.68.57 1.15.75.36.14.88.31 1.86.35 1.05.05 1.37.06 4.04.06s2.99-.01 4.04-.06c.98-.04 1.5-.21 1.86-.35.47-.18.8-.4 1.15-.75.35-.35.57-.68.75-1.15.14-.36.31-.88.35-1.86.05-1.05.06-1.37.06-4.04s-.01-2.99-.06-4.04c-.04-.98-.21-1.5-.35-1.86a3.1 3.1 0 0 0-.75-1.15 3.1 3.1 0 0 0-1.15-.75c-.36-.14-.88-.31-1.86-.35-1.05-.05-1.37-.06-4.04-.06Zm0 3.07a5.13 5.13 0 1 1 0 10.26 5.13 5.13 0 0 1 0-10.26Zm0 1.8a3.33 3.33 0 1 0 0 6.66 3.33 3.33 0 0 0 0-6.66Zm5.34-3.2a1.2 1.2 0 1 1 0 2.4 1.2 1.2 0 0 1 0-2.4Z" />
                      </svg>
                    </a>
                  )}
                  {vendor.social?.facebook && (
                    <a
                      className="social-ico"
                      href={vendor.social.facebook}
                      onClick={outbound("facebook")}
                      onAuxClick={outbound("facebook")}
                      target="_blank"
                      rel="noopener noreferrer nofollow"
                      aria-label={`${vendor.name} na Facebooku`}
                      title="Facebook"
                    >
                      <svg viewBox="0 0 24 24" width="20" height="20" fill="currentColor" aria-hidden="true">
                        <path d="M22 12.06C22 6.5 17.52 2 12 2S2 6.5 2 12.06c0 5.02 3.66 9.18 8.44 9.94v-7.03H7.9v-2.9h2.54V9.85c0-2.52 1.49-3.91 3.78-3.91 1.09 0 2.24.2 2.24.2v2.47h-1.26c-1.24 0-1.63.78-1.63 1.57v1.88h2.78l-.44 2.9h-2.34V22c4.78-.76 8.44-4.92 8.44-9.94Z" />
                      </svg>
                    </a>
                  )}
                </div>
              )}
            </div>
            <div className="head-price">
              <div className={isOnRequest(vendor.price) ? "price-upit big" : "price big"}>{formatPrice(vendor.price)}</div>
              {vendor.reviewCount > 0 ? (
                <div className="rating">
                  <span className="star">★</span> {formatRating(vendor.rating)}{" "}
                  <span className="muted">
                    ({vendor.reviewCount}){vendor.ratingSource ? ` · ${vendor.ratingSource}` : ""}
                  </span>
                </div>
              ) : (
                <div className="muted" style={{ fontSize: 13.5 }}>još bez recenzija</div>
              )}
            </div>
          </header>

          {plan && !isOnRequest(vendor.price) && (
            <p className={`fit ${over ? "bad" : "good"}`}>
              {over ? (
                <>
                  Izvan vašeg plana — {GROUP_LABELS[vendorGroup(vendor)]} ≤ {euro(plan.caps[vendorGroup(vendor)])}, a
                  procjena za {plan.guests} gostiju je {euro(estimateCost(vendor, plan.guests))}.
                </>
              ) : (
                <>
                  ✓ Stane u vaš plan ({GROUP_LABELS[vendorGroup(vendor)]} ≤ {euro(plan.caps[vendorGroup(vendor)])} za{" "}
                  {plan.guests} gostiju).
                </>
              )}
            </p>
          )}

          <div className="profile-actions">
            <label
              className={`compare-box big${compareDisabled ? " disabled" : ""}`}
              title={compareDisabled ? COMPARE_INCOMPATIBLE_HINT : undefined}
            >
              <input
                type="checkbox"
                checked={ids.includes(vendor.id)}
                disabled={compareDisabled}
                onChange={() => toggle(vendor)}
              />
              Dodaj u usporedbu
            </label>
            <button
              className={`btn btn-sm${fav ? " fav-on" : ""}`}
              aria-pressed={fav}
              onClick={() => {
                if (!fav) track("favorite_added", { slug: vendor.slug, category: vendor.category });
                favorites.toggle(vendor.id);
              }}
            >
              {fav ? "♥ Spremljeno" : "♡ Spremi"}
            </button>
            <button className="btn btn-sm" disabled title="Uskoro">
              ✉ Kontakt (uskoro)
            </button>
          </div>

          <section>
            <h2>Što pružatelj kaže o sebi</h2>
            <p>{about}</p>
            <ul className="services">
              {services.map((s) => (
                <li key={s}>✓ {s}</li>
              ))}
            </ul>
          </section>

          <section>
            <h2>Recenzije</h2>
            <p className="rev-note">
              Dvije vrste recenzija: <strong>prenesene</strong> (prikupio i provjerio Wediplan iz vanjskih izvora
              prije lansiranja) i <strong>Wediplan recenzije</strong> stvarnih korisnika platforme.
            </p>

            {importedReviews.length > 0 ? (
              <div className="reviews">
                {importedReviews.map((r, i) => (
                  <article key={i} className="review">
                    <div className="rev-head">
                      <strong>{r.author}</strong>
                      <span className="star">{"★".repeat(Math.round(r.rating))}</span>
                    </div>
                    <p>{r.text}</p>
                    <p className="rev-src">prenesena recenzija · {r.source}, {r.year}.</p>
                  </article>
                ))}
              </div>
            ) : (
              <p className="muted">Ovaj pružatelj još nema prenesenih recenzija.</p>
            )}

            <div className="user-reviews" style={{ marginTop: 18 }}>
              <h3 style={{ fontFamily: "var(--font-display)", fontWeight: 550, fontSize: 18, margin: "0 0 8px" }}>
                Wediplan recenzije
                {userReviews && userReviews.length > 0 && (
                  <span className="muted" style={{ fontWeight: 400, fontSize: 14 }}>
                    {" "}({userReviews.length})
                  </span>
                )}
              </h3>
              {userReviews && userReviews.length > 0 ? (
                <div className="reviews">
                  {userReviews.map((r) => (
                    <article key={r.id} className="review">
                      <div className="rev-head">
                        <strong>{r.author}</strong>
                        <span className="star">{"★".repeat(Math.round(r.rating))}</span>
                      </div>
                      <p>{r.text}</p>
                      <p className="rev-src">
                        Wediplan recenzija · {new Date(r.createdAt).toLocaleDateString("hr-HR")}
                      </p>
                    </article>
                  ))}
                </div>
              ) : (
                <p className="muted">Još nema recenzija korisnika platforme. Budite prvi.</p>
              )}
              <ReviewForm vendorSlug={vendor.slug} />
            </div>
          </section>
        </div>

        <aside className="profile-aside">
          <AvailabilityCalendar vendor={vendor} />
          <ClaimPanel vendor={vendor} />
          <OptOutLink slug={vendor.slug} claimStatus={vendor.claimStatus} />
          {similar.length > 0 && (
            <div className="similar">
              <h3>Slično u kategoriji {cat.short ?? cat.name}</h3>
              {similar.map((v) => (
                <VendorCard key={v.id} vendor={v} />
              ))}
            </div>
          )}
        </aside>
      </div>
    </main>
  );
}
