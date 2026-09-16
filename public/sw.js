/* Wediplan service worker — app shell cache + statički resursi.
   Plan & favoriti su u localStorage, pa rade offline i bez mreže.

   v2 (Faza 2): API više NIJE stale-while-revalidate. Pravi API vraća žive podatke
   (liste, pinovi, brojači, ?ids=, budget-matches, suggest) — posluživanje iz cachea
   prikazivalo bi zastarjele podatke, a svaki upit (tipkanje, slider) punio bi cache
   bez granice. Sada: API ide mrežom; samo mali, rijetko promjenjivi šifrarnici
   imaju mrežu-pa-cache za offline. Promjena imena cacheova briše stari v1 cache. */
const SHELL = "wediplan-shell-v2";
const RUNTIME = "wediplan-runtime-v2";
const API = "wediplan-api-v2";
const API_OFFLINE = ["/api/regions", "/api/categories", "/api/budget-defaults"];
const PRECACHE = ["/", "/budzet", "/usporedba", "/profil", "/manifest.webmanifest", "/data/croatia-regions.geojson"];

self.addEventListener("install", (e) => {
  e.waitUntil(caches.open(SHELL).then((c) => c.addAll(PRECACHE)).then(() => self.skipWaiting()));
});

self.addEventListener("activate", (e) => {
  e.waitUntil(
    caches.keys().then((keys) =>
      Promise.all(keys.filter((k) => ![SHELL, RUNTIME, API].includes(k)).map((k) => caches.delete(k)))
    ).then(() => self.clients.claim())
  );
});

self.addEventListener("fetch", (e) => {
  const url = new URL(e.request.url);
  if (e.request.method !== "GET" || url.origin !== location.origin) return;

  // API: mreža. Samo šifrarnici (bez query varijanti osim regije) imaju offline kopiju.
  if (url.pathname.startsWith("/api/")) {
    if (!API_OFFLINE.includes(url.pathname)) return; // pusti browser (bez SW-a)
    e.respondWith(
      fetch(e.request)
        .then((res) => {
          if (res.ok) {
            const copy = res.clone();
            caches.open(API).then((c) => c.put(e.request, copy));
          }
          return res;
        })
        .catch(async () => (await caches.match(e.request)) || Response.error())
    );
    return;
  }

  // statički resursi (hashirani /_next/static, GeoJSON): stale-while-revalidate
  if (url.pathname.startsWith("/data/") || url.pathname.startsWith("/_next/static/")) {
    e.respondWith(
      caches.open(RUNTIME).then(async (cache) => {
        const cached = await cache.match(e.request);
        const fresh = fetch(e.request)
          .then((res) => {
            if (res.ok) cache.put(e.request, res.clone());
            return res;
          })
          .catch(() => cached);
        return cached || fresh;
      })
    );
    return;
  }

  // navigations: network first, fall back to cached shell
  if (e.request.mode === "navigate") {
    e.respondWith(
      fetch(e.request)
        .then((res) => {
          caches.open(RUNTIME).then((c) => c.put(e.request, res.clone()));
          return res;
        })
        .catch(async () => (await caches.match(e.request)) || (await caches.match("/")))
    );
  }
});
