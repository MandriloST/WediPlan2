import { NextRequest, NextResponse } from "next/server";
import { queryVendors } from "@/lib/mock/search";
import type { RegionId } from "@/lib/types";

// MOCK — zrcalo .NET GET /api/vendors (API.md). Aktivno samo bez API_URL
// (s API_URL next.config.mjs prepisuje /api/* na .NET prije ovih ruta).
export function GET(req: NextRequest) {
  const sp = req.nextUrl.searchParams;
  const num = (k: string) => (sp.get(k) ? Number(sp.get(k)) : undefined);
  const ids = sp.get("ids");
  const result = queryVendors({
    ids: ids ? ids.split(",").filter(Boolean) : undefined,
    q: sp.get("q") ?? undefined,
    region: (sp.get("region") as RegionId) ?? undefined,
    category: sp.get("category") ?? undefined,
    date: sp.get("date") ?? undefined,
    page: num("page"),
    pageSize: num("pageSize"),
  });
  return NextResponse.json(result);
}
