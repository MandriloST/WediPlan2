import type { AdminConsentSummary } from "./types";

/**
 * Porijeklo podataka i privola pružatelja (§Zadatak 17) — vrijednosti i oznake za admin sučelje. Interne vrijednosti MORAJU pratiti
 * backend (`Import/ProvenanceRules.cs`) i padajuće liste u `scripts/make-template.py`.
 */

export const DATA_SOURCES: ReadonlyArray<readonly [string, string]> = [
  ["google_maps", "Google Maps"],
  ["web", "Web stranica"],
  ["instagram", "Instagram"],
  ["facebook", "Facebook"],
  ["partner", "Partner (sam dao podatke)"],
  ["preporuka", "Preporuka"],
  ["drugo", "Drugo"],
];

export const CONSENT_STATUSES: ReadonlyArray<readonly [string, string]> = [
  ["unknown", "Nepoznato"],
  ["requested", "Zatraženo"],
  ["granted", "Dano"],
  ["refused", "Odbijeno"],
];

export const CONSENT_CHANNELS: ReadonlyArray<readonly [string, string]> = [
  ["email", "E-mail"],
  ["instagram", "Instagram"],
  ["facebook", "Facebook"],
  ["telefon", "Telefon"],
  ["osobno", "Osobno"],
  ["claim", "Preuzimanje profila (claim)"],
];

export const CONSENT_SCOPES: ReadonlyArray<readonly [string, string]> = [
  ["data", "podaci"],
  ["photos", "slike"],
  ["reviews", "recenzije"],
];

/** ISO datum (UTC) → vrijednost za `<input type="date">` ("2026-09-30"); prazno za null/neispravno. */
export function toDateInput(iso?: string | null): string {
  if (!iso) return "";
  const day = iso.slice(0, 10);
  return /^\d{4}-\d{2}-\d{2}$/.test(day) ? day : "";
}

/**
 * Vrijednost iz `<input type="date">` → ISO za API. Prazno → `null` (briše datum). Ako je odabran ISTI dan kao u originalu, vraća se
 * ORIGINAL (npr. `consentAt` iz claima ima i sat — spremanje forme ga ne smije prekrojiti na ponoć).
 */
export function fromDateInput(value: string, original?: string | null): string | null {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return null;
  if (original && toDateInput(original) === value) return original;
  return `${value}T00:00:00Z`;
}

/** Sažetak za praćenje kampanje: „ukupno 120 · nepoznato 80 · zatraženo 20 · dano 18 · odbijeno 2". */
export function consentSummaryLine(s: AdminConsentSummary): string {
  return `ukupno ${s.total} · nepoznato ${s.unknown} · zatraženo ${s.requested} · dano ${s.granted} · odbijeno ${s.refused}`;
}
