import { NextRequest, NextResponse } from "next/server";
import { categoriesWithCounts } from "@/lib/mock/search";

// MOCK — GET /api/categories?region= (§L)
export const dynamic = "force-dynamic";
export function GET(req: NextRequest) {
  return NextResponse.json(categoriesWithCounts(req.nextUrl.searchParams.get("region") ?? undefined));
}
