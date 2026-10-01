import type { AdminAuditEntry } from "./types";

/**
 * Prikaz dnevnika promjena u admin sučelju (§Zadatak 14). Čiste funkcije bez ovisnosti o Reactu.
 * Format `changes` (JSON tekst s backenda): izmjena `{"PriceFrom":{"old":800,"new":950}}`, maskirano
 * `{"Phone":{"changed":true}}`, novi entitet `{"Name":"…","Phone":{"set":true}}`.
 */

const ACTION: Record<string, string> = {
  create: "stvoreno",
  update: "izmjena",
  delete: "brisanje",
  optout: "opt-out (skriven)",
  optout_restored: "opt-out poništen",
  account_deleted: "račun obrisan",
  owner_unlinked: "vlasnik odvezan",
};

const ENTITY: Record<string, string> = {
  vendor: "pružatelj",
  vendor_photo: "slika",
  imported_review: "uvezena recenzija",
  user_review: "korisnička recenzija",
  claim: "zahtjev za preuzimanje",
  user: "korisnik",
};

const ACTOR: Record<string, string> = {
  admin: "admin",
  partner: "partner",
  user: "korisnik",
  public: "javno (anonimno)",
  import: "import",
  system: "sustav",
};

export const actionLabel = (a: string) => ACTION[a] ?? a;
export const entityLabel = (t: string) => ENTITY[t] ?? t;

/** „admin: ana@primjer.hr", „import", „korisnik" (e-mail izostaje za javne/sistemske radnje i obrisane korisnike). */
export function actorLabel(e: Pick<AdminAuditEntry, "actorType" | "actorEmail">): string {
  const base = ACTOR[e.actorType] ?? e.actorType;
  return e.actorEmail ? `${base}: ${e.actorEmail}` : base;
}

/** Vrijednost za prikaz u jednom retku: tekst skraćen na `max` znakova, liste „a, b", null kao „∅". */
export function formatValue(v: unknown, max = 60): string {
  if (v === null || v === undefined) return "∅";
  if (typeof v === "string") return v.length > max ? `${v.slice(0, max)}…` : v === "" ? "„“" : v;
  if (Array.isArray(v)) return v.length === 0 ? "[]" : `[${v.map((x) => formatValue(x, 30)).join(", ")}]`;
  if (typeof v === "boolean") return v ? "da" : "ne";
  return String(v);
}

/** Sažetak `changes` u retke: `PriceFrom: 800 → 950`, `Phone: promijenjeno`, `Phone: postavljeno`, `Name: Foto Anić`. */
export function summarizeChanges(changes?: string | null): string[] {
  if (!changes) return [];
  let parsed: unknown;
  try {
    parsed = JSON.parse(changes);
  } catch {
    return [changes];
  }
  if (parsed === null || typeof parsed !== "object" || Array.isArray(parsed)) return [String(changes)];

  return Object.entries(parsed as Record<string, unknown>).map(([key, val]) => {
    if (val !== null && typeof val === "object" && !Array.isArray(val)) {
      const o = val as Record<string, unknown>;
      if ("old" in o || "new" in o) return `${key}: ${formatValue(o.old)} → ${formatValue(o.new)}`;
      if (o.changed === true) return `${key}: promijenjeno`;
      if (o.set === true) return `${key}: postavljeno`;
    }
    return `${key}: ${formatValue(val)}`;
  });
}

/** Lokalno vrijeme zapisa (hr-HR); neispravan datum se vraća kakav jest. */
export function formatWhen(iso: string): string {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleString("hr-HR", { dateStyle: "short", timeStyle: "medium" });
}
