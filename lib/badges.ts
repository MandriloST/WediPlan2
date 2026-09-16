import type { Vendor } from "./types";

/**
 * Sustav oznaka v1 — [ZA ODOBRENJE #9] u PLAN-ARHITEKTURA.md
 *
 * Načela:
 * - Oznake se IZVODE iz podataka, nikad ne unose ručno (kao locationPrecision).
 * - Dva slota: POVJERENJE (uvijek najviše jedna, po prioritetu) i ZASLUGA (najjača).
 * - Nikad negativne javne oznake — interni statusi žive u Excelu (status "Skriveno").
 * - Sponzorstvo NIKAD ne utječe na oznake (vidi §Monetizacija u planu).
 */

/** Pragovi — javni kriteriji, objašnjeni na /oznake */
export const TOP_RATED_MIN_RATING = 4.8;
export const TOP_RATED_MIN_REVIEWS = 20;

export interface VendorBadge {
  id: "claimed" | "data-verified" | "new" | "top-rated";
  label: string;
  /** dodatna CSS klasa uz .badge */
  className: string;
  /** tooltip s javnim kriterijem dodjele */
  tooltip: string;
}

/**
 * Slot POVJERENJA — točno jedno stanje profila, po prioritetu:
 * verificirani profil (vlasnik preuzeo) → provjereni podaci (WediPlan potvrdio
 * kontakt) → novi na WediPlanu (v1 proxy: još bez recenzija; Faza 1: created_at).
 */
export function trustBadge(v: Vendor): VendorBadge | null {
  if (v.claimStatus === "claimed")
    return {
      id: "claimed",
      label: "✓ Verificirani profil",
      className: "verified",
      tooltip: "Vlasnik je preuzeo profil i sam potvrdio podatke.",
    };
  if (v.verified)
    return {
      id: "data-verified",
      label: "✓ Provjereni podaci",
      className: "verified",
      tooltip: "WediPlan je potvrdio kontakt i osnovne podatke pružatelja.",
    };
  if (v.reviewCount === 0)
    return {
      id: "new",
      label: "Novi na WediPlanu",
      className: "new",
      tooltip: "Nedavno dodan profil — podaci se još dopunjuju.",
    };
  return null;
}

/**
 * Slot ZASLUGE — zarađene oznake iz podataka; prikazuje se najjača.
 * v1: samo Top ocijenjen. Kasnije (uz tracking/CRM): Popularan, Brzo odgovara.
 */
export function meritBadge(v: Vendor): VendorBadge | null {
  if (v.rating >= TOP_RATED_MIN_RATING && v.reviewCount >= TOP_RATED_MIN_REVIEWS)
    return {
      id: "top-rated",
      label: `★ Top ocijenjen${v.ratingSource ? ` · ${v.ratingSource}` : ""}`,
      className: "top",
      tooltip: `Ocjena ${TOP_RATED_MIN_RATING}+ uz najmanje ${TOP_RATED_MIN_REVIEWS} recenzija (izvor je uvijek naveden).`,
    };
  return null;
}

/** Obje oznake za prikaz (povjerenje pa zasluga), bez null vrijednosti. */
export function vendorBadges(v: Vendor): VendorBadge[] {
  return [trustBadge(v), meritBadge(v)].filter(Boolean) as VendorBadge[];
}
