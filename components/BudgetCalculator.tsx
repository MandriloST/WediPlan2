"use client";

import { useRouter } from "next/navigation";
import { useEffect, useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api/client";
import { track } from "@/lib/analytics";
import { REGIONS, GROUP_LABELS } from "@/lib/data";
import { euro } from "@/lib/format";
import { budgetMatchParams, computeCaps, GROUP_ORDER, sharesFor } from "@/lib/budget";
import type { BudgetGroup, RegionId } from "@/lib/types";
import { useBudget } from "@/stores";

/** Vrijednost koja kasni za ulazom (slider) — da svaki pomak ne ode na API. */
function useDebounced<T>(value: T, ms = 300): T {
  const [v, setV] = useState(value);
  useEffect(() => {
    const t = setTimeout(() => setV(value), ms);
    return () => clearTimeout(t);
  }, [value, ms]);
  return v;
}
const GROUP_CATEGORY: Record<BudgetGroup, string> = {
  sala: "restorani-i-sale",
  catering: "catering",
  foto: "foto-i-video",
  glazba: "glazba-bendovi",
  ostalo: "",
};

export default function BudgetCalculator({ onNavigate }: { onNavigate?: () => void }) {
  const router = useRouter();
  const { plan, setPlan, clearPlan } = useBudget();

  const [guests, setGuests] = useState(plan?.guests ?? 120);
  const [region, setRegion] = useState<RegionId>(plan?.region ?? "dalmacija");
  const [total, setTotal] = useState(plan?.total ?? 18000);

  const shares = sharesFor(region);
  const caps = useMemo(() => computeCaps(total, region), [total, region]);
  const debounced = useDebounced(budgetMatchParams({ guests, region, caps }).toString());
  const { data: counts, isFetching } = useQuery({
    queryKey: ["budget-matches", debounced],
    queryFn: ({ signal }) => api.budgetMatches(new URLSearchParams(debounced), signal),
    placeholderData: (prev) => prev, // slider ne "trepće" dok stiže novi broj
    staleTime: 5 * 60_000,
  });
  const n = (x: number | undefined) => (x === undefined ? "…" : String(x));

  function commit() {
    setPlan(guests, region, total);
    track("budget_calculated", { guests, region });
  }

  function apply() {
    commit();
    onNavigate?.();
    router.push(`/${region}`);
  }

  return (
    <div className="calc">
      <div className="frow">
        <div className="fld">
          <label htmlFor="b-guests">Broj gostiju</label>
          <input
            id="b-guests"
            type="number"
            min={10}
            max={600}
            step={10}
            value={guests}
            onChange={(e) => setGuests(Number(e.target.value) || 0)}
          />
        </div>
        <div className="fld">
          <label htmlFor="b-region">Regija</label>
          <select id="b-region" value={region} onChange={(e) => setRegion(e.target.value as RegionId)}>
            {REGIONS.map((r) => (
              <option key={r.id} value={r.id}>
                {r.name}
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className="fld">
        <label htmlFor="b-total">Ukupni budžet</label>
        <div className="total">{euro(total)}</div>
        <input
          id="b-total"
          type="range"
          min={5000}
          max={60000}
          step={500}
          value={total}
          onChange={(e) => setTotal(Number(e.target.value))}
          aria-valuetext={euro(total)}
        />
      </div>

      <div>
        <div className="dist" aria-hidden>
          {GROUP_ORDER.map((g) => (
            <div key={g} className="seg" style={{ width: `${shares[g] * 100}%` }}>
              {GROUP_LABELS[g]} {Math.round(shares[g] * 100)}%
            </div>
          ))}
        </div>
        <p style={{ fontSize: 12.5, color: "var(--muted)", margin: "6px 2px 0" }}>
          ≈ uobičajena raspodjela za regiju {REGIONS.find((r) => r.id === region)?.name}
        </p>
      </div>

      <button className="btn btn-primary" onClick={apply}>
        Prikaži {n(counts?.matches)} pružatelja u mom budžetu →
      </button>

      <div className="plan" aria-busy={isFetching}>
        <h3>Vaš plan — sve na jednom mjestu</h3>
        {GROUP_ORDER.map((g) => {
          const href = GROUP_CATEGORY[g] ? `/${region}/${GROUP_CATEGORY[g]}` : `/${region}`;
          return (
            <a
              key={g}
              className="cap"
              href={href}
              onClick={(e) => {
                e.preventDefault();
                commit();
                onNavigate?.();
                router.push(href);
              }}
            >
              <span>
                {GROUP_LABELS[g]} ≤ <strong>{euro(caps[g])}</strong>
              </span>
              <span className="n">{n(counts?.groups[g])} opcija →</span>
            </a>
          );
        })}
        {plan && (
          <button className="btn btn-ghost btn-sm" onClick={clearPlan}>
            Ukloni plan
          </button>
        )}
      </div>

      <div className="pwa-hint">📲 Dodaj na početni zaslon — plan i favoriti rade i offline (PWA)</div>
    </div>
  );
}
