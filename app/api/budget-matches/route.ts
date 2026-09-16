import { NextRequest, NextResponse } from "next/server";
import { REGION_BY_ID } from "@/lib/data";
import { GROUP_ORDER } from "@/lib/budget";
import { budgetMatches } from "@/lib/mock/budget";
import type { BudgetGroup, RegionId } from "@/lib/types";

// MOCK — GET /api/budget-matches?region=&guests=&sala=&catering=&foto=&glazba=&ostalo=
export function GET(req: NextRequest) {
  const sp = req.nextUrl.searchParams;
  const region = sp.get("region") as RegionId;
  if (!REGION_BY_ID[region]) return NextResponse.json({ error: "region_required" }, { status: 400 });
  const guests = Math.max(0, Number(sp.get("guests")) || 0);
  const caps = Object.fromEntries(
    GROUP_ORDER.map((g) => [g, Math.max(0, Number(sp.get(g)) || 0)])
  ) as Record<BudgetGroup, number>;
  return NextResponse.json(budgetMatches(region, guests, caps));
}
