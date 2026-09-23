/** @type {import('next').NextConfig} */
import { withSentryConfig } from "@sentry/nextjs/config";

// Slike: danas iz public/images; nakon selidbe na Bunny postavi
// NEXT_PUBLIC_IMAGE_BASE=https://wediplan.b-cdn.net (ista struktura foldera)
// i remotePattern se doda automatski.
const imageBase = process.env.NEXT_PUBLIC_IMAGE_BASE;
const remotePatterns = [];
function addRemote(base) {
  if (base && base.startsWith("http")) {
    const u = new URL(base);
    remotePatterns.push({
      protocol: u.protocol.replace(":", ""),
      hostname: u.hostname,
      pathname: `${u.pathname.replace(/\/$/, "")}/**`,
    });
  }
}
addRemote(imageBase);
// Faza 5 — uploadane slike pružatelja: u produkciji s R2/CDN domene (NEXT_PUBLIC_UPLOADS_BASE,
// npr. https://cdn.wediplan.hr). U dev-u se serviraju preko /uploads proxyja (rewrite niže).
addRemote(process.env.NEXT_PUBLIC_UPLOADS_BASE);

// Faza 2 — prekidač izvora podataka (server-only env, NIJE NEXT_PUBLIC_):
//   API_URL=http://localhost:5080        → /api/* ide na .NET (lokalno)
//   API_URL=https://api.wediplan.hr      → /api/* ide na .NET (Vercel)
//   (nije postavljeno)                   → mock rute iz app/api/* nad data/vendors.json
// beforeFiles: rewrite ima prednost pred app/api/* rutama, pa mock ostaje u repou
// kao referenca, ali se ne izvršava dok je API_URL postavljen.
const apiUrl = (process.env.API_URL ?? "").replace(/\/$/, "");
if (apiUrl && !/^https?:\/\//.test(apiUrl)) {
  throw new Error(`API_URL mora počinjati s http:// ili https:// (dobiveno: "${apiUrl}")`);
}

const nextConfig = {
  reactStrictMode: true,
  images: { remotePatterns },
  async rewrites() {
    if (!apiUrl) return { beforeFiles: [], afterFiles: [], fallback: [] };
    return {
      beforeFiles: [
        { source: "/api/:path*", destination: `${apiUrl}/api/:path*` },
        // Lokalno uploadane slike (LocalPhotoStorage servira ih .NET na /uploads) → proxy same-origin.
        { source: "/uploads/:path*", destination: `${apiUrl}/uploads/:path*` },
      ],
      afterFiles: [],
      fallback: [],
    };
  },
  async redirects() {
    // ukinuta kategorija "Auto za mladence" -> spojena u najam-limuzina
    return [
      { source: "/auto-za-mladence", destination: "/najam-limuzina", permanent: true },
      { source: "/:region/auto-za-mladence", destination: "/:region/najam-limuzina", permanent: true },
    ];
  },
};

// Monitoring (§Zadatak 8) — withSentryConfig SAMO omata build (source-map upload, auto-instrument);
// ne pokreće ništa u runtimeu i ne treba DSN da bi build prošao. Upload izvornih mapa u Sentry je
// opcionalan i sam po sebi bez organizacije/tokena samo tiho preskače (ne ruši build) — postavi
// SENTRY_ORG/SENTRY_PROJECT/SENTRY_AUTH_TOKEN (CI/Vercel env, nikad u git) kad to zatreba.
// Za Next 14 (ova verzija) withSentryConfig automatski uključuje experimental.instrumentationHook
// (potrebno da se instrumentation.ts uopće izvrši) — ništa dodatno ovdje nije potrebno postaviti.
export default withSentryConfig(nextConfig, {
  org: process.env.SENTRY_ORG,
  project: process.env.SENTRY_PROJECT,
  authToken: process.env.SENTRY_AUTH_TOKEN,
  silent: true, // bez build-log šuma kad org/token nisu postavljeni (dev/CI bez Sentryja)
  tunnelRoute: "/monitoring", // events idu kroz vlastitu domenu — ad-block ne guši Sentry pozive
  disableLogger: true, // manji client bundle (uklanja Sentry-ev interni debug logger iz koda)
});
