import { NextRequest, NextResponse } from "next/server";
import { suggest } from "@/lib/mock/search";

// MOCK — GET /api/suggest?q= (typeahead)
export function GET(req: NextRequest) {
  return NextResponse.json(suggest(req.nextUrl.searchParams.get("q") ?? ""));
}
