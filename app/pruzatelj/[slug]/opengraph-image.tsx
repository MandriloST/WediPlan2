import { ImageResponse } from "next/og";
import { getProfile } from "@/lib/api/server";
import { withProfileDefaults } from "@/lib/profile";
import { CATEGORY_BY_SLUG, homeLabel } from "@/lib/data";
import { formatPrice, formatRating, isOnRequest } from "@/lib/format";
import { vendorImages } from "@/lib/images";
import { readOgImageAsset } from "@/lib/og-image";
import { loadOgFonts } from "@/lib/og-fonts";
import { CheckIcon, StarIcon } from "@/lib/og-icons";

// fs pristup lokalnim slikama/fontovima (public/…) traži nodejs runtime, ne edge.
export const runtime = "nodejs";
// Isti interval kao stranica profila (revalidate = 300 u page.tsx) — slika se ne
// smrzava zauvijek ako pružatelj promijeni fotografiju/ime.
export const revalidate = 300;

export const alt = "Wediplan — profil pružatelja";
export const size = { width: 1200, height: 630 };
export const contentType = "image/png";

const BRAND_GRADIENT = "linear-gradient(135deg, #2470cc 0%, #17538f 100%)";

export default async function Image({ params }: { params: { slug: string } }) {
  const [raw, fonts] = await Promise.all([getProfile(params.slug), loadOgFonts()]);
  const profile = raw ? withProfileDefaults(raw) : null;

  // Nepoznat/skinut profil (opt-out, 404) — i dalje moramo vratiti valjanu sliku
  // (crawler koji je ranije uhvatio link ne smije dobiti grešku), pa ide brand fallback.
  if (!profile) {
    return new ImageResponse(
      (
        <div
          style={{
            width: "100%",
            height: "100%",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            background: BRAND_GRADIENT,
            fontFamily: "Instrument Sans, sans-serif",
          }}
        >
          <div style={{ fontSize: 64, fontWeight: 700, color: "#fff" }}>Wediplan</div>
        </div>
      ),
      { ...size, fonts }
    );
  }

  const { vendor } = profile;
  const cat = CATEGORY_BY_SLUG[vendor.category]?.name ?? "";
  const place = homeLabel(vendor);
  // Ista slika koju vidi posjetitelj profila (VendorProfile.tsx: vendorImages(vendor, "profile")).
  // SAMO stvarna fotografija pružatelja ide u pozadinu OG slike — generirane placeholder
  // ilustracije (defaults-profile/*.jpg) su zamišljene kao mala kartica, ne kao full-bleed
  // hero pozadina 1200×630 (i neke od njih next/og trenutno pogrešno skalira; v. STANJE.md).
  // Bez stvarne fotografije ide brand gradijent — izgleda i profesionalnije za dijeljenje.
  const mainImage = vendorImages(vendor, "profile")[0];
  const bg = mainImage.isDefault ? null : await readOgImageAsset(mainImage.src);

  return new ImageResponse(
    (
      <div
        style={{
          width: "100%",
          height: "100%",
          display: "flex",
          position: "relative",
          background: bg ? "#21252c" : BRAND_GRADIENT,
          fontFamily: "Instrument Sans, sans-serif",
        }}
      >
        {bg && (
          // eslint-disable-next-line @next/next/no-img-element
          <img
            src={bg}
            width={1200}
            height={630}
            // next/og (Satori) NE podržava "inset" shorthand — mora eksplicitno sve četiri
            // strane, inače se element sruši na intrinsic veličinu (provjereno testom).
            style={{ position: "absolute", top: 0, left: 0, right: 0, bottom: 0, objectFit: "cover" }}
          />
        )}
        {/* Tamni gradijent odozdo — tekst čitljiv na bilo kojoj fotografiji. */}
        <div
          style={{
            position: "absolute",
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            display: "flex",
            background:
              "linear-gradient(to top, rgba(23,26,31,0.92) 0%, rgba(23,26,31,0.55) 45%, rgba(23,26,31,0.05) 75%)",
          }}
        />

        {/* Wordmark — uvijek vidljiv, i preko fotografije i preko brand pozadine. */}
        <div
          style={{
            position: "absolute",
            top: 40,
            left: 56,
            display: "flex",
            fontSize: 28,
            fontWeight: 700,
            color: "#fff",
            letterSpacing: -0.5,
          }}
        >
          Wediplan
        </div>

        {vendor.verified && (
          <div
            style={{
              position: "absolute",
              top: 40,
              right: 56,
              display: "flex",
              alignItems: "center",
              padding: "8px 18px",
              borderRadius: 999,
              background: "rgba(255,255,255,0.16)",
              color: "#fff",
              fontSize: 20,
              fontWeight: 700,
            }}
          >
            <CheckIcon size={20} />
            <span style={{ marginLeft: 8, display: "flex" }}>Provjereno</span>
          </div>
        )}

        {/* Donji tekstualni blok. */}
        <div
          style={{
            position: "absolute",
            left: 56,
            right: 56,
            bottom: 48,
            display: "flex",
            flexDirection: "column",
          }}
        >
          {cat && (
            <div
              style={{
                display: "flex",
                alignSelf: "flex-start",
                padding: "6px 16px",
                marginBottom: 18,
                borderRadius: 999,
                background: "rgba(255,255,255,0.18)",
                color: "#fff",
                fontSize: 22,
                fontWeight: 700,
              }}
            >
              {cat}
            </div>
          )}
          <div
            style={{
              display: "flex",
              fontSize: 60,
              fontWeight: 700,
              color: "#fff",
              lineHeight: 1.1,
              maxWidth: 1000,
            }}
          >
            {vendor.name}
          </div>
          <div
            style={{
              display: "flex",
              alignItems: "center",
              marginTop: 16,
              fontSize: 28,
              color: "rgba(255,255,255,0.88)",
            }}
          >
            <div style={{ display: "flex" }}>{place || "Hrvatska"}</div>
            {vendor.reviewCount > 0 && (
              <div style={{ display: "flex", alignItems: "center", marginLeft: 20 }}>
                <StarIcon size={24} />
                <span style={{ marginLeft: 8, display: "flex" }}>
                  {formatRating(vendor.rating)} ({vendor.reviewCount})
                </span>
              </div>
            )}
          </div>
          <div
            style={{
              display: "flex",
              alignSelf: "flex-start",
              marginTop: 24,
              padding: "12px 24px",
              borderRadius: 999,
              background: "#2470cc",
              color: "#fff",
              fontSize: 26,
              fontWeight: 700,
            }}
          >
            {isOnRequest(vendor.price) ? "Cijena na upit" : formatPrice(vendor.price)}
          </div>
        </div>
      </div>
    ),
    { ...size, fonts }
  );
}
