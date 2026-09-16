"use client";

import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { BudgetPlan, RegionId } from "@/lib/types";
import { computeCaps } from "@/lib/budget";
import { vendorCategories, type Categorized } from "@/lib/categories";
import { track } from "@/lib/analytics";

/* ---------------- Compare (max 4, oldest replaced, toast) ---------------- */

/** Što store pamti o odabranom pružatelju (Faza 2: klijent nema cijeli katalog). */
export interface CompareItemMeta {
  name: string;
  cats: string[];
}

/** Bilo koji oblik pružatelja (Vendor, PinVendor) iz kojeg se može izvesti meta. */
export type CompareCandidate = { id: string; name: string; slug?: string } & Categorized;

interface CompareState {
  ids: string[];
  /** id → naziv + sve kategorije (za tray inicijale i blokadu nekompatibilnih) */
  meta: Record<string, CompareItemMeta>;
  dismissed: boolean; // tray dismissed until next change
  toast: string | null;
  toggle: (v: CompareCandidate) => void;
  remove: (id: string) => void;
  clear: () => void;
  /** osvježi meta s API-ja i makni ID-jeve koji više ne postoje (zatvoreni/skriveni vendori) */
  refresh: (found: CompareCandidate[], requested: string[]) => void;
  dismissTray: () => void;
  clearToast: () => void;
}

/** meta → { id: kategorije } za canAddToCompare (čista funkcija; pozivati s useMemo). */
export const catsOf = (meta: Record<string, CompareItemMeta>) =>
  Object.fromEntries(Object.entries(meta).map(([k, m]) => [k, m.cats]));

const metaOf = (v: CompareCandidate): CompareItemMeta => ({ name: v.name, cats: vendorCategories(v) });

export const useCompare = create<CompareState>()(
  persist(
    (set, get) => ({
      ids: [],
      meta: {},
      dismissed: false,
      toast: null,
      toggle: (v) => {
        const { ids, meta } = get();
        if (ids.includes(v.id)) {
          const { [v.id]: _drop, ...rest } = meta;
          set({ ids: ids.filter((x) => x !== v.id), meta: rest, dismissed: false });
          return;
        }
        const nextMeta = { ...meta, [v.id]: metaOf(v) };
        track("compare_added", { slug: v.slug, category: v.category });
        if (ids.length >= 4) {
          // cap at 4 — oldest replaced with a toast (README)
          const { [ids[0]]: _old, ...rest } = nextMeta;
          set({
            ids: [...ids.slice(1), v.id],
            meta: rest,
            dismissed: false,
            toast: "Maksimalno 4 za usporedbu — najstariji odabir je zamijenjen.",
          });
          return;
        }
        set({ ids: [...ids, v.id], meta: nextMeta, dismissed: false });
      },
      remove: (id) =>
        set((s) => {
          const { [id]: _drop, ...rest } = s.meta;
          return { ids: s.ids.filter((x) => x !== id), meta: rest };
        }),
      clear: () => set({ ids: [], meta: {} }),
      refresh: (found, requested) =>
        set((s) => {
          const byId = new Map(found.map((v) => [v.id, v]));
          const asked = new Set(requested);
          // briši samo ono što je API izričito NE vratio (ne dira ID-jeve dodane u međuvremenu)
          const ids = s.ids.filter((id) => !asked.has(id) || byId.has(id));
          const meta: Record<string, CompareItemMeta> = {};
          for (const id of ids) {
            const v = byId.get(id);
            const m = v ? metaOf(v) : s.meta[id];
            if (m) meta[id] = m;
          }
          return { ids, meta };
        }),
      dismissTray: () => set({ dismissed: true }),
      clearToast: () => set({ toast: null }),
    }),
    {
      name: "wediplan.compare",
      version: 1,
      partialize: (s) => ({ ids: s.ids, meta: s.meta }),
      // v0 je pamtio samo ids → meta se dopuni pri prvom osvježavanju (CompareTray)
      migrate: (persisted: any) => ({ ids: persisted?.ids ?? [], meta: persisted?.meta ?? {} }),
    }
  )
);

/* ---------------- Budget plan ---------------- */

interface BudgetState {
  plan: BudgetPlan | null;
  drawerOpen: boolean;
  setPlan: (guests: number, region: RegionId, total: number) => void;
  clearPlan: () => void;
  openDrawer: () => void;
  closeDrawer: () => void;
}

export const useBudget = create<BudgetState>()(
  persist(
    (set) => ({
      plan: null,
      drawerOpen: false,
      setPlan: (guests, region, total) =>
        set({ plan: { guests, region, total, caps: computeCaps(total, region) } }),
      clearPlan: () => set({ plan: null }),
      openDrawer: () => set({ drawerOpen: true }),
      closeDrawer: () => set({ drawerOpen: false }),
    }),
    { name: "wediplan.budget", partialize: (s) => ({ plan: s.plan }) }
  )
);

/* ---------------- Favorites ---------------- */

interface FavoritesState {
  ids: string[];
  toggle: (id: string) => void;
  /** makni ID-jeve koje je API izričito NE vratio (requested − found) */
  prune: (foundIds: string[], requested: string[]) => void;
}

export const useFavorites = create<FavoritesState>()(
  persist(
    (set, get) => ({
      ids: [],
      toggle: (id) =>
        set({
          ids: get().ids.includes(id) ? get().ids.filter((x) => x !== id) : [...get().ids, id],
        }),
      prune: (foundIds, requested) =>
        set((s) => {
          const found = new Set(foundIds);
          const asked = new Set(requested);
          const ids = s.ids.filter((id) => !asked.has(id) || found.has(id));
          return ids.length === s.ids.length ? {} : { ids };
        }),
    }),
    { name: "wediplan.favorites" }
  )
);

/* ---------------- Chosen wedding date (for availability in compare) ---------------- */

interface DateState {
  date: string | null; // ISO yyyy-mm-dd
  setDate: (d: string | null) => void;
}

export const useWeddingDate = create<DateState>()(
  persist(
    (set) => ({ date: null, setDate: (d) => set({ date: d }) }),
    { name: "wediplan.date" }
  )
);
