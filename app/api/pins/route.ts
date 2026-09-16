import { NextRequest, NextResponse } from "next/server";
import { CATEGORY_BY_SLUG } from "@/lib/data";
import { queryPins } from "@/lib/mock/search";

// MOCK — GET /api/pins?category=&region=&q= (karta; category obavezna — §L)
export function GET(req: NextRequest) {
  const sp = req.nextUrl.searchParams;
  const category = sp.get("category") ?? "";
  if (!CATEGORY_BY_SLUG[category])
    return NextResponse.json({ error: "category_required" }, { status: 400 });
  return NextResponse.json(
    queryPins({ category, region: sp.get("region") ?? undefined, q: sp.get("q") ?? undefined })
  );
}
