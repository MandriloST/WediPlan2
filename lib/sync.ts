"use client";

import { coupleApi } from "@/lib/api/auth";
import { useBudget, useFavorites } from "@/stores";
import type { RegionId } from "@/lib/types";

/**
 * Migracija localStorage → account nakon prijave (§5: "merge, ne pregazi").
 *
 * Tok pri prijavi:
 *  1. pročitaj lokalne favorite/plan (Zustand persist),
 *  2. POST /api/favorites/merge — server radi UNIJU favorita i postavlja plan SAMO ako ga
 *     korisnik još nema,
 *  3. rezultat (spojeno stanje sa servera) upiši natrag u storeove → UI odmah pokazuje
 *     spojene favorite/plan, a od tada su izvor istine na serveru.
 *
 * Sve je best-effort: ako mreža/endpoint zakažu, prijava se nastavlja (favoriti ostaju lokalno).
 */
export async function syncLocalToAccount(): Promise<void> {
  try {
    const favIds = useFavorites.getState().ids;
    const localPlan = useBudget.getState().plan;
    const planDto = localPlan
      ? { guests: localPlan.guests, region: localPlan.region, total: localPlan.total }
      : null;

    const merged = await coupleApi.merge(favIds, planDto);
    if (!merged) return;

    // upiši spojeno stanje natrag (server je sad izvor istine)
    useFavorites.setState({ ids: merged.favoriteIds });
    if (merged.plan) {
      useBudget.getState().setPlan(
        merged.plan.guests,
        merged.plan.region as RegionId,
        merged.plan.total
      );
    }
  } catch {
    /* prijava se nastavlja bez obzira na sync */
  }
}

/**
 * Učitaj couple podatke sa servera u storeove (poziva se kad je korisnik već prijavljen,
 * npr. pri boot-u nakon osvježavanja stranice). NE radi merge — server je izvor istine.
 */
export async function loadAccountData(): Promise<void> {
  try {
    const data = await coupleApi.get();
    if (!data) return;
    useFavorites.setState({ ids: data.favoriteIds });
    if (data.plan) {
      useBudget.getState().setPlan(data.plan.guests, data.plan.region as RegionId, data.plan.total);
    }
  } catch {
    /* ostani na localStorage */
  }
}
