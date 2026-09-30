import { ImageResponse } from "next/og";
import { HERO_IMAGE } from "@/lib/landing";
import { readOgImageAsset } from "@/lib/og-image";
import { loadOgFonts } from "@/lib/og-fonts";

export const runtime = "nodejs"; // fs pristup public/images/hero i public/fonts preko helpera

export const alt = "Wediplan — sve za vjenčanje u Hrvatskoj";
export const size = { width: 1200, height: 630 };
export const contentType = "image/png";

/**
 * Default OG slika za sve rute BEZ vlastite opengraph-image datoteke (naslovnica, /kategorije,
 * /karta, /budzet, /usporedba…). Profil pružatelja ima svoju dinamičku sliku
 * (app/pruzatelj/[slug]/opengraph-image.tsx) koja automatski ima prednost za tu rutu.
 */
export default async function Image() {
  const [bg, fonts] = await Promise.all([readOgImageAsset(HERO_IMAGE), loadOgFonts()]);

  return new ImageResponse(
    (
      <div
        style={{
          width: "100%",
          height: "100%",
          display: "flex",
          position: "relative",
          background: "linear-gradient(135deg, #2470cc 0%, #17538f 100%)",
          fontFamily: "Instrument Sans, sans-serif",
        }}
      >
        {bg && (
          // eslint-disable-next-line @next/next/no-img-element
          <img
            src={bg}
            width={1200}
            height={630}
            // next/og (Satori) NE podržava "inset" shorthand — v. isti komentar u
            // app/pruzatelj/[slug]/opengraph-image.tsx (provjereno testom).
            style={{
              position: "absolute",
              top: 0,
              left: 0,
              right: 0,
              bottom: 0,
              objectFit: "cover",
              opacity: 0.35,
            }}
          />
        )}
        <div
          style={{
            position: "absolute",
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            display: "flex",
            background: "linear-gradient(135deg, rgba(23,71,140,0.55) 0%, rgba(23,26,31,0.75) 100%)",
          }}
        />
        <div
          style={{
            position: "absolute",
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            display: "flex",
            flexDirection: "column",
            alignItems: "flex-start",
            justifyContent: "center",
            padding: "0 80px",
          }}
        >
          <div style={{ display: "flex", fontSize: 76, fontWeight: 700, color: "#fff" }}>
            Wediplan
          </div>
          <div style={{ display: "flex", marginTop: 20, fontSize: 32, color: "rgba(255,255,255,0.9)" }}>
            Sve za vjenčanje u Hrvatskoj
          </div>
          <div
            style={{
              display: "flex",
              marginTop: 28,
              fontSize: 24,
              color: "rgba(255,255,255,0.78)",
            }}
          >
            Transparentne cijene · Usporedba · Kalkulator budžeta
          </div>
        </div>
      </div>
    ),
    { ...size, fonts }
  );
}
