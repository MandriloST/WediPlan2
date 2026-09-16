/**
 * Kanonski URL stranice.
 * Lokalno: http://localhost:3000
 * Vercel preview: automatski iz VERCEL_URL
 * Produkcija: postavi NEXT_PUBLIC_SITE_URL (npr. https://wediplan.hr) u Vercel env
 */
export const SITE_URL =
  process.env.NEXT_PUBLIC_SITE_URL ??
  (process.env.VERCEL_URL ? `https://${process.env.VERCEL_URL}` : "http://localhost:3000");
