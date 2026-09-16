import { NextRequest, NextResponse } from "next/server";
import { sharesFor } from "@/lib/budget";
import type { RegionId } from "@/lib/types";

// GET /api/budget-defaults?region= — regional distribution defaults
export function GET(req: NextRequest) {
  const region = (req.nextUrl.searchParams.get("region") as RegionId) ?? undefined;
  return NextResponse.json({ region: region ?? "hr", shares: sharesFor(region) });
}
