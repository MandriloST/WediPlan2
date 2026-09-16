import { NextResponse } from "next/server";
import { sitemapEntries } from "@/lib/mock/search";

// MOCK — GET /api/sitemap (samo slugovi)
export function GET() {
  return NextResponse.json(sitemapEntries());
}
