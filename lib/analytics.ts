/**
 * First-party analitika (§A, odluka #11) — agregatno, nikad individualno.
 *
 * Pravila privatnosti (ne mijenjati bez rasprave):
 *  - bez kolačića; session_hash je nasumičan ID u sessionStorage (umire sa zatvaranjem taba)
 *  - bez PII: nikad email/telefon/ime korisnika; user id se ne veže ni kad auth dođe
 *  - Do Not Track / Global Privacy Control → ništa se ne šalje
 *  - page = samo pathname (bez query stringa — ?q= može sadržavati bilo što)
 *  - tekst pretrage se čisti (cleanQuery): bez nizova od 6+ znamenki (telefon), bez @, max 40 znakova
 *
 * Tehnički: red čekanja + batch (≤ 20, isto kao server), flush nakon 4 s ili kad se tab
 * sakrije (sendBeacon). Nikad ne blokira render i nikad ne baca grešku (fail-silent).
 * Katalog eventa = whitelist u backend EventsController — dodavati na oba mjesta.
 */

export type AnalyticsEvent =
  | "page_view"
  | "search_performed"
  | "vendor_viewed"
  | "map_region_clicked"
  | "map_pin_clicked"
  | "compare_added"
  | "compare_viewed"
  | "budget_calculated"
  | "outbound_click"
  | "favorite_added";

type PropValue = string | number | boolean | string[] | undefined | null;
export type EventProps = Record<string, PropValue>;

interface QueuedEvent {
  name: AnalyticsEvent;
  sessionHash: string;
  page: string;
  props?: Record<string, string | number | boolean | string[]>;
}

const ENDPOINT = "/api/events";
const MAX_BATCH = 20; // = EventsController.MaxBatch
const MAX_QUEUE = 100; // zaštita memorije ako je mreža dulje nedostupna
const FLUSH_MS = 4000;
const SID_KEY = "wediplan.sid";

const isBrowser = typeof window !== "undefined";
let queue: QueuedEvent[] = [];
let timer: ReturnType<typeof setTimeout> | null = null;
let memorySid: string | null = null;
let listening = false;

/** DNT / GPC — poštuje se bez iznimke (§A). */
export function analyticsOptedOut(): boolean {
  if (!isBrowser) return true;
  const n = navigator as Navigator & { globalPrivacyControl?: boolean; msDoNotTrack?: string };
  const w = window as Window & { doNotTrack?: string };
  return (
    n.globalPrivacyControl === true ||
    n.doNotTrack === "1" ||
    n.doNotTrack === "yes" ||
    w.doNotTrack === "1" ||
    n.msDoNotTrack === "1"
  );
}

function randomId(): string {
  try {
    const b = new Uint8Array(12);
    crypto.getRandomValues(b);
    return Array.from(b, (x) => x.toString(16).padStart(2, "0")).join("");
  } catch {
    return Math.random().toString(16).slice(2) + Date.now().toString(16);
  }
}

function sessionHash(): string {
  try {
    let sid = sessionStorage.getItem(SID_KEY);
    if (!sid) {
      sid = randomId();
      sessionStorage.setItem(SID_KEY, sid);
    }
    return sid;
  } catch {
    // sessionStorage blokiran (privatni način / postavke) → ID samo u memoriji
    return (memorySid ??= randomId());
  }
}

/**
 * Tekst pretrage za agregatne reporte ("što se traži"), bez osobnih podataka:
 * odbacuje unose koji izgledaju kao email ili telefon, skraćuje i normalizira.
 */
export function cleanQuery(q: string | undefined | null): string | undefined {
  if (!q) return undefined;
  const t = q.trim().toLowerCase().replace(/\s+/g, " ");
  if (!t) return undefined;
  if (t.includes("@") || /\d{6,}/.test(t.replace(/[\s\-/.()+]/g, ""))) return undefined;
  return t.slice(0, 40);
}

function sanitize(props?: EventProps): QueuedEvent["props"] {
  if (!props) return undefined;
  const out: NonNullable<QueuedEvent["props"]> = {};
  for (const [k, v] of Object.entries(props)) {
    if (v === undefined || v === null || v === "") continue;
    if (typeof v === "string") out[k] = v.slice(0, 80);
    else if (Array.isArray(v)) out[k] = v.slice(0, 4).map((x) => String(x).slice(0, 80));
    else if (typeof v === "number") {
      if (Number.isFinite(v)) out[k] = v;
    } else out[k] = v;
  }
  return Object.keys(out).length ? out : undefined;
}

function send(batch: QueuedEvent[], beacon: boolean) {
  const body = JSON.stringify(batch);
  try {
    if (beacon && typeof navigator.sendBeacon === "function") {
      const ok = navigator.sendBeacon(ENDPOINT, new Blob([body], { type: "application/json" }));
      if (ok) return;
    }
    void fetch(ENDPOINT, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body,
      // keepalive samo kad stranica odlazi (Chromium ga u DevToolsu prikazuje kao
      // "failed/ERR_ABORTED" iako stigne — za redovni flush nije potreban, jer klijentska
      // navigacija u Next.js-u ne gasi dokument)
      keepalive: beacon,
      credentials: "omit", // analitika nikad ne nosi kolačiće (ni nakon Faze 3)
    }).catch(() => {});
  } catch {
    /* fail-silent */
  }
}

/** Pošalji sve iz reda (u komadima ≤ 20). beacon=true kad stranica odlazi. */
export function flush(beacon = false) {
  if (timer) {
    clearTimeout(timer);
    timer = null;
  }
  while (queue.length) send(queue.splice(0, MAX_BATCH), beacon);
}

function listen() {
  if (listening) return;
  listening = true;
  document.addEventListener("visibilitychange", () => {
    if (document.visibilityState === "hidden") flush(true);
  });
  window.addEventListener("pagehide", () => flush(true));
}

/**
 * Zabilježi event. Sigurno za pozivanje bilo gdje u klijentskom kodu:
 * na serveru, uz DNT/GPC ili pri bilo kakvoj grešci ne radi ništa.
 */
export function track(name: AnalyticsEvent, props?: EventProps) {
  try {
    if (!isBrowser || analyticsOptedOut()) return;
    listen();
    if (queue.length >= MAX_QUEUE) queue.shift();
    queue.push({ name, sessionHash: sessionHash(), page: location.pathname.slice(0, 300), props: sanitize(props) });
    if (queue.length >= MAX_BATCH) flush();
    else if (!timer) timer = setTimeout(() => flush(), FLUSH_MS);
  } catch {
    /* fail-silent */
  }
}

/** Za outbound linkove: zabilježi i odmah pošalji (korisnik možda napušta stranicu). */
export function trackNow(name: AnalyticsEvent, props?: EventProps) {
  track(name, props);
  flush(true);
}

/** Samo za testove. */
export function __resetAnalyticsForTests() {
  queue = [];
  if (timer) clearTimeout(timer);
  timer = null;
  memorySid = null;
}
