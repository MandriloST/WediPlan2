/**
 * Kanonski URL stranice.
 * Lokalno: http://localhost:3000
 * Vercel preview: automatski iz VERCEL_URL
 * Produkcija: postavi NEXT_PUBLIC_SITE_URL (npr. https://wediplan.hr) u Vercel env
 */
export const SITE_URL =
  process.env.NEXT_PUBLIC_SITE_URL ??
  (process.env.VERCEL_URL ? `https://${process.env.VERCEL_URL}` : "http://localhost:3000");

/**
 * Pretvori relativnu putanju ("/pruzatelj/slug", "/images/…") u apsolutni URL na SITE_URL.
 * Već apsolutne URL-ove (npr. slika s Bunny CDN-a kad je NEXT_PUBLIC_IMAGE_BASE postavljen)
 * vraća nepromijenjene. Koristi se za JSON-LD i Open Graph, koji apsolutne URL-ove traže
 * bez obzira izvire li slika iz public/ ili s CDN-a (§SEO, drugi val).
 */
export function absoluteUrl(path: string): string {
  if (/^https?:\/\//i.test(path)) return path;
  return `${SITE_URL}${path.startsWith("/") ? path : `/${path}`}`;
}
