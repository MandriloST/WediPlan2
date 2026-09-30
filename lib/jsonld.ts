import type { VendorProfileData } from "./types";
import { formatPrice, isOnRequest } from "./format";
import { vendorImages } from "./images";
import { breadcrumb } from "./profile";
import { absoluteUrl } from "./site";

/**
 * Strukturirani podaci (schema.org) za profil pružatelja (§SEO, drugi val — PLAN-PRIORITETI-
 * LANSIRANJE-2.md, Zadatak 6). LocalBusiness pokriva sve kategorije dovoljno dobro za Google
 * Rich Results bez posebnog mapiranja po kategoriji (dvorana/restoran/fotograf/…) — finiji
 * podtipovi (npr. EventVenue za sale, FoodEstablishment za catering) su razuman budući
 * dodatak, ne blokiraju.
 *
 * Isti etos kao "null = koordinate nepoznate → bez pina" (lib/images.ts): geo, priceRange i
 * aggregateRating izostaju kad ih stvarno nemamo — NIKAD ne izmišljamo podatak radi Googlea.
 */
export function vendorJsonLd(data: VendorProfileData): Record<string, unknown> {
  const { vendor, about, userReviews } = data;
  const url = absoluteUrl(`/pruzatelj/${vendor.slug}`);
  const image = absoluteUrl(vendorImages(vendor, "profile")[0].src);

  const business: Record<string, unknown> = {
    "@type": "LocalBusiness",
    "@id": url,
    name: vendor.name,
    url,
    image,
  };
  if (about?.trim()) business.description = about.trim();

  if (vendor.city) {
    business.address = {
      "@type": "PostalAddress",
      addressLocality: vendor.city,
      addressCountry: (vendor.country ?? "hr").toUpperCase(),
    };
  }

  // Nikad ne izmišljamo koordinate — isto pravilo kao na karti (lib/images.ts, VendorMapper).
  if (vendor.lat != null && vendor.lng != null) {
    business.geo = { "@type": "GeoCoordinates", latitude: vendor.lat, longitude: vendor.lng };
  }

  // "cijena na upit" se ne prikazuje kao priceRange — nemamo brojku za reći Googleu.
  if (!isOnRequest(vendor.price)) {
    business.priceRange = formatPrice(vendor.price);
  }

  // aggregateRating SAMO iz vlastitih, objavljenih recenzija korisnika Wediplana (Zadatak 18).
  // NE koristimo vendor.rating/reviewCount: to su ocjene iz Excela s vanjskih izvora (vendor.ratingSource,
  // npr. Google) — Googleove smjernice za review snippete ne dopuštaju agregiranje ocjena s drugih stranica,
  // a ocjene Google Places API-ja se ionako ne smiju spremati ni ponovno objavljivati (PLAN-3, Odluka 11).
  // Bez recenzija → polje izostaje (Google odbija AggregateRating bez recenzija). Vizualni prikaz ocjene na
  // profilu (VendorProfile.tsx) ostaje netaknut.
  const own = (userReviews ?? []).filter(
    (r) => Number.isFinite(r.rating) && r.rating >= 1 && r.rating <= 5
  );
  if (own.length > 0) {
    const avg = own.reduce((sum, r) => sum + r.rating, 0) / own.length;
    business.aggregateRating = {
      "@type": "AggregateRating",
      ratingValue: Math.round(avg * 10) / 10,
      reviewCount: own.length,
      bestRating: 5,
      worstRating: 1,
    };
  }

  const sameAs = [vendor.social?.instagram, vendor.social?.facebook].filter(
    (v): v is string => Boolean(v)
  );
  if (sameAs.length) business.sameAs = sameAs;

  // breadcrumb() (lib/profile.ts) je već izvor istine za vizualnu navigacijsku putanju na
  // profilu (VendorProfile.tsx) — ista lista se ovdje samo produžuje pružateljem i pretvara
  // u apsolutne URL-ove za BreadcrumbList.
  const crumbs = [...breadcrumb(vendor), { label: vendor.name, href: `/pruzatelj/${vendor.slug}` }];
  const breadcrumbList = {
    "@type": "BreadcrumbList",
    itemListElement: crumbs.map((c, i) => ({
      "@type": "ListItem",
      position: i + 1,
      name: c.label,
      item: absoluteUrl(c.href),
    })),
  };

  return { "@context": "https://schema.org", "@graph": [business, breadcrumbList] };
}

/** Sigurno serijaliziraj za <script type="application/ld+json"> — spriječi prijevremeni
 *  </script> ako ime/opis pružatelja sadrži takav niz. */
export function jsonLdScript(data: unknown): string {
  return JSON.stringify(data).replace(/</g, "\\u003c");
}
