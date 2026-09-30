/**
 * Ikone za next/og (Satori) generirane OG slike (§SEO, drugi val).
 *
 * NAPOMENA: simboli poput "★" i "✓" NISU pouzdani kao tekstualni znakovi ovdje — Instrument
 * Sans (lib/og-fonts.ts) ih ne sadrži, a Satori u ovoj (bundled @vercel/og) verziji ne radi
 * per-glyph fallback na drugi font kad prvi font ne pokriva znak (potvrđeno testom: i sama
 * "sans-serif" generička zamjena ne spašava nedostajući znak). Zato su ikone crtane kao SVG,
 * neovisno o fontu.
 */
export function CheckIcon({ size = 20, color = "#fff" }: { size?: number; color?: string }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" style={{ display: "flex" }}>
      <path
        d="M4.5 12.75l6 6 9-13.5"
        stroke={color}
        strokeWidth={3}
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

export function StarIcon({ size = 20, color = "#fbbf24" }: { size?: number; color?: string }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill={color} style={{ display: "flex" }}>
      <path d="M12 .587l3.668 7.568 8.332 1.151-6.064 5.828 1.48 8.279L12 19.771l-7.416 3.642 1.48-8.279L.001 9.306l8.332-1.151z" />
    </svg>
  );
}
