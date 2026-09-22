import { readFile } from "node:fs/promises";
import path from "node:path";

/**
 * Učita sliku za generiranje Open Graph slike kao base64 data URI (§SEO, drugi val).
 * Lokalne slike (public/…, danas zadano — IMAGE_BASE="/images") čita izravno s diska umjesto
 * fetch-om na vlastiti origin (brže, bez rizika od self-fetch petlje). Udaljene (Bunny CDN kad
 * je NEXT_PUBLIC_IMAGE_BASE postavljen) dohvaća preko fetch-a.
 *
 * Nikad ne baca: OG generator na svaki neuspjeh (slika nedostaje, mreža padne) mora i dalje
 * vratiti valjanu sliku — poziva ovo, dobije null, i pada na brand pozadinu bez fotografije.
 */
export async function readOgImageAsset(srcOrUrl: string): Promise<string | null> {
  try {
    if (/^https?:\/\//i.test(srcOrUrl)) {
      const res = await fetch(srcOrUrl);
      if (!res.ok) return null;
      const buf = Buffer.from(await res.arrayBuffer());
      const type = res.headers.get("content-type") ?? "image/jpeg";
      return `data:${type};base64,${buf.toString("base64")}`;
    }
    const filePath = path.join(process.cwd(), "public", srcOrUrl.replace(/^\//, ""));
    const buf = await readFile(filePath);
    const ext = path.extname(filePath).toLowerCase();
    const type = ext === ".png" ? "image/png" : ext === ".webp" ? "image/webp" : "image/jpeg";
    return `data:${type};base64,${buf.toString("base64")}`;
  } catch {
    return null;
  }
}
