import { NextRequest, NextResponse } from "next/server";
import { regionsWithCounts } from "@/lib/mock/search";

// MOCK — GET /api/regions?category= (brojači po pravilu liste: sjedište ∪ pokrivanje)
export const dynamic = "force-dynamic";
export function GET(req: NextRequest) {
  return NextResponse.json(regionsWithCounts(req.nextUrl.searchParams.get("category") ?? undefined));
}
