import { readFile } from "node:fs/promises";
import path from "node:path";

/**
 * Fontovi za next/og (Satori) generiranje OG slika (§SEO, drugi val).
 *
 * NAPOMENA — zašto ne Google Fonts CDN izravno: Google Fonts CDN dijeli fontove po
 * "subsetu" (latin / latin-ext/…) tako da je hrvatski č/ć/š/ž/đ u ODVOJENOM "latin-ext"
 * fajlu bez osnovnih slova a-z (i obrnuto) — učitavanje samo jednog od njih rezultira
 * polovicom alfabeta kao "tofu" kvadratićima. Satori ovdje (bundled @vercel/og) također
 * NE podržava .woff2 (samo .ttf/.otf/.woff), pa Google Fonts CDN fajlovi ionako ne bi radili.
 *
 * Zato su ovdje dva STATIČNA .ttf instancirana iz punog (ne-subsetiranog) varijabilnog
 * fonta Instrument Sans sa službenog google/fonts repozitorija (github.com/google/fonts,
 * ofl/instrumentsans — OFL-1.1 licenca, v. public/fonts/InstrumentSans-OFL.txt), pomoću
 * `fonttools varLib.instancer` na wght=400 i wght=700. Sadrže PUN Latin Extended-A raspon
 * (potvrđeno: č ć š ž đ Č Ć Š Ž Đ + €), ne samo osnovni "latin" ASCII podskup.
 *
 * Čitaju se s diska (public/fonts/…) — bez mrežnog poziva na fonts.googleapis.com pri
 * svakom generiranju slike (brže, i ne ovisi o dostupnosti tog hosta iz produkcijskog okoliša).
 */
export interface OgFont {
  name: string;
  data: ArrayBuffer;
  weight: 400 | 700;
  style: "normal";
}

let cached: OgFont[] | null = null;

export async function loadOgFonts(): Promise<OgFont[]> {
  if (cached) return cached;
  const dir = path.join(process.cwd(), "public", "fonts");
  const [regular, bold] = await Promise.all([
    readFile(path.join(dir, "InstrumentSans-Regular.ttf")),
    readFile(path.join(dir, "InstrumentSans-Bold.ttf")),
  ]);
  cached = [
    { name: "Instrument Sans", data: toArrayBuffer(regular), weight: 400, style: "normal" },
    { name: "Instrument Sans", data: toArrayBuffer(bold), weight: 700, style: "normal" },
  ];
  return cached;
}

function toArrayBuffer(buf: Buffer): ArrayBuffer {
  // Buffer.buffer je ArrayBufferLike (može biti SharedArrayBuffer) — Uint8Array kopija
  // preko slice() jamči pravi ArrayBuffer koji next/og (Satori) tipovi traže.
  return Uint8Array.from(buf).buffer;
}
