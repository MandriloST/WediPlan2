import "server-only";
import { VENDORS } from "./vendors";
import { estimateCost, GROUP_ORDER, vendorGroup } from "@/lib/budget";
import type { BudgetGroup, BudgetMatches, RegionId } from "@/lib/types";
import { inRegion } from "./search";

/**
 * MOCK GET /api/budget-matches — zrcalo .NET BudgetMatchesController.
 * Regija po pravilu liste (sjedište ∪ pokrivanje) — broj odgovara onome što par vidi klikom.
 * "na upit" se računa kao 0 € (ne isključuje se iz budžeta — isto kao isOverBudget).
 */
export function budgetMatches(region: RegionId, guests: number, caps: Record<BudgetGroup, number>): BudgetMatches {
  const groups = Object.fromEntries(GROUP_ORDER.map((g) => [g, 0])) as Record<BudgetGroup, number>;
  let matches = 0;
  for (const v of VENDORS) {
    if (!inRegion(v, region)) continue;
    const g = vendorGroup(v);
    if (estimateCost(v, guests) <= caps[g]) {
      groups[g]++;
      matches++;
    }
  }
  return { region, matches, groups };
}
