import type { PriceModel } from "./types";

const nf = new Intl.NumberFormat("hr-HR", { maximumFractionDigits: 0 });

export function euro(n: number): string {
  return `${nf.format(n)} €`;
}

/** "od 850 €" | "55–80 €/os." — pricing transparency, always visible */
export function formatPrice(p: PriceModel): string {
  if (p.kind === "onRequest") return "cijena na upit";
  if (p.kind === "from") return `od ${euro(p.from)}`;
  return `${nf.format(p.from)}–${nf.format(p.to)} €/os.`;
}

export function isOnRequest(p: PriceModel): boolean {
  return p.kind === "onRequest";
}

/** Short label used on map pins */
export function pinPrice(p: PriceModel): string {
  if (p.kind === "onRequest") return "na upit";
  if (p.kind === "from") return `od ${euro(p.from)}`;
  return `od ${p.from} €/os.`;
}

export function formatRating(r: number): string {
  return r.toLocaleString("hr-HR", { minimumFractionDigits: 1, maximumFractionDigits: 1 });
}
