import { BUDGET_DEFAULTS, CATEGORY_BY_SLUG } from "./data";
import type { BudgetGroup, BudgetPlan, RegionId, Vendor } from "./types";

/** Redoslijed budžetskih omotnica (kalkulator, grid kategorija). */
export const GROUP_ORDER: BudgetGroup[] = ["sala", "catering", "foto", "glazba", "ostalo"];

type Priced = Pick<Vendor, "price">;
type Grouped = Pick<Vendor, "category">;

export function sharesFor(region: RegionId | undefined) {
  const hit = BUDGET_DEFAULTS.find((d) => d.region === region);
  return (hit ?? BUDGET_DEFAULTS[0]).shares;
}

export function computeCaps(total: number, region: RegionId): Record<BudgetGroup, number> {
  const shares = sharesFor(region);
  const caps = {} as Record<BudgetGroup, number>;
  (Object.keys(shares) as BudgetGroup[]).forEach((g) => {
    caps[g] = Math.round((total * shares[g]) / 50) * 50; // round to 50 €
  });
  return caps;
}

/** Lower-bound cost estimate for comparing against a category cap. */
export function estimateCost(vendor: Priced, guests: number): number {
  if (vendor.price.kind === "onRequest") return 0; // nepoznato → ne isključuj iz budžeta
  return vendor.price.kind === "perPerson" ? vendor.price.from * guests : vendor.price.from;
}

export function vendorGroup(vendor: Grouped): BudgetGroup {
  return (CATEGORY_BY_SLUG[vendor.category]?.group ?? "ostalo") as BudgetGroup;
}

/** Vendors above the cap are greyed out with "izvan budžeta" — never hidden. */
export function isOverBudget(vendor: Priced & Grouped, plan: BudgetPlan | null): boolean {
  if (!plan) return false;
  const cap = plan.caps[vendorGroup(vendor)];
  return estimateCost(vendor, plan.guests) > cap;
}

/**
 * Brojači "N opcija" po grupi i "Prikaži N pružatelja" računa server
 * (GET /api/budget-matches — v. lib/api). Klijent šalje capove koje je SAM izračunao
 * (computeCaps), pa pravilo zaokruživanja postoji na jednom mjestu.
 */
export function budgetMatchParams(plan: Pick<BudgetPlan, "region" | "guests" | "caps">): URLSearchParams {
  const sp = new URLSearchParams({ region: plan.region, guests: String(plan.guests) });
  for (const g of GROUP_ORDER) sp.set(g, String(plan.caps[g]));
  return sp;
}
