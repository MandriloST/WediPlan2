/** @type {import('next').NextConfig} */

// Slike: danas iz public/images; nakon selidbe na Bunny postavi
// NEXT_PUBLIC_IMAGE_BASE=https://wediplan.b-cdn.net (ista struktura foldera)
// i remotePattern se doda automatski.
const imageBase = process.env.NEXT_PUBLIC_IMAGE_BASE;
const remotePatterns = [];
if (imageBase && imageBase.startsWith("http")) {
  const u = new URL(imageBase);
  remotePatterns.push({
    protocol: u.protocol.replace(":", ""),
    hostname: u.hostname,
    pathname: `${u.pathname.replace(/\/$/, "")}/**`,
  });
}

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
      beforeFiles: [{ source: "/api/:path*", destination: `${apiUrl}/api/:path*` }],
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
export default nextConfig;
