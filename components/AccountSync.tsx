"use client";

import { useEffect, useRef } from "react";
import { useAuth } from "@/stores/auth";
import { useBudget, useFavorites } from "@/stores";
import { coupleApi } from "@/lib/api/auth";
import { loadAccountData } from "@/lib/sync";

/**
 * Drži couple podatke (favoriti/plan) u sinkronu sa serverom DOK je korisnik prijavljen.
 *
 * - Pri prijavi (user postane != null): učitaj server-stanje u storeove (merge se već dogodio
 *   u LoginForm/TokenAction preko syncLocalToAccount; ovo pokriva boot nakon refresha).
 * - Nakon toga: svaka promjena favorita/plana u storeu se mirrora na server (fire-and-forget).
 * - Pri odjavi: prestani mirrorati (server više nije dostupan; storeovi ostaju kao lokalni).
 *
 * Bez UI-ja. Mirror je best-effort; greške se progutaju (UI se ne blokira).
 */
export default function AccountSync() {
  const user = useAuth((s) => s.user);
  const loading = useAuth((s) => s.loading);
  const active = useRef(false);
  const prevFav = useRef<string[]>([]);
  const prevPlanKey = useRef<string | null>(null);

  // aktiviraj/deaktiviraj prema prijavi
  useEffect(() => {
    if (loading) return;
    if (user && !active.current) {
      active.current = true;
      // učitaj server-stanje (nakon refresha); LoginForm je već napravio merge pri svježoj prijavi
      void loadAccountData().then(() => {
        prevFav.current = useFavorites.getState().ids;
        const p = useBudget.getState().plan;
        prevPlanKey.current = p ? `${p.guests}|${p.region}|${p.total}` : null;
      });
    } else if (!user && active.current) {
      active.current = false;
    }
  }, [user, loading]);

  // mirror favorita na server (diff prema prethodnom stanju)
  useEffect(() => {
    return useFavorites.subscribe((state) => {
      if (!active.current) {
        prevFav.current = state.ids;
        return;
      }
      const before = new Set(prevFav.current);
      const after = new Set(state.ids);
      for (const id of state.ids) if (!before.has(id)) void coupleApi.add(id).catch(() => {});
      for (const id of Array.from(before)) if (!after.has(id)) void coupleApi.remove(id).catch(() => {});
      prevFav.current = state.ids;
    });
  }, []);

  // mirror plana na server
  useEffect(() => {
    return useBudget.subscribe((state) => {
      const p = state.plan;
      const key = p ? `${p.guests}|${p.region}|${p.total}` : null;
      if (!active.current) {
        prevPlanKey.current = key;
        return;
      }
      if (key === prevPlanKey.current) return;
      prevPlanKey.current = key;
      if (p) void coupleApi.savePlan({ guests: p.guests, region: p.region, total: p.total });
      else void coupleApi.deletePlan().catch(() => {});
    });
  }, []);

  return null;
}
