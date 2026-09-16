import type { Vendor } from "./types";

/**
 * Mock dostupnosti dok ne stigne live kalendar (Coming soon → CRM).
 * Deterministički po vendor+datum, pa su usporedba i profil uvijek konzistentni.
 * Zamjena u produkciji: GET /api/vendors/{id}/availability (vidi API.md).
 */

export type DayStatus = "free" | "busy" | "onRequest";

function hash(s: string): number {
  let h = 0;
  for (const ch of s) h = (h * 31 + ch.charCodeAt(0)) % 997;
  return h;
}

export function dayStatus(vendor: Pick<Vendor, "id" | "liveCalendar">, isoDate: string): DayStatus {
  if (!vendor.liveCalendar) return "onRequest";
  const h = hash(vendor.id + isoDate);
  const day = new Date(isoDate + "T12:00:00").getDay();
  // subote su tražene — češće zauzete
  if (day === 6) return h % 2 === 0 ? "busy" : "free";
  return h % 3 === 0 ? "busy" : "free";
}

export function statusLabel(status: DayStatus): { label: string; free: boolean } {
  switch (status) {
    case "free":
      return { label: "✓ slobodan", free: true };
    case "busy":
      return { label: "zauzet na taj datum", free: false };
    case "onRequest":
      return { label: "na upit — kontaktirajte pružatelja", free: false };
  }
}

export interface MonthDay {
  iso: string;
  dayOfMonth: number;
  status: DayStatus;
  past: boolean;
}

/** Kalendarska rešetka za mjesec (ponedjeljak prvi): null = prazna ćelija prije 1. */
export function monthGrid(
  vendor: Pick<Vendor, "id" | "liveCalendar">,
  year: number,
  month: number // 0-based
): (MonthDay | null)[] {
  const first = new Date(Date.UTC(year, month, 1));
  const daysInMonth = new Date(Date.UTC(year, month + 1, 0)).getUTCDate();
  const lead = (first.getUTCDay() + 6) % 7; // Mon=0
  const today = new Date().toISOString().slice(0, 10);

  const cells: (MonthDay | null)[] = Array.from({ length: lead }, () => null);
  for (let d = 1; d <= daysInMonth; d++) {
    const iso = `${year}-${String(month + 1).padStart(2, "0")}-${String(d).padStart(2, "0")}`;
    cells.push({ iso, dayOfMonth: d, status: dayStatus(vendor, iso), past: iso < today });
  }
  return cells;
}

export const MONTHS_HR = [
  "Siječanj", "Veljača", "Ožujak", "Travanj", "Svibanj", "Lipanj",
  "Srpanj", "Kolovoz", "Rujan", "Listopad", "Studeni", "Prosinac",
];
