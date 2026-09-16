import { NextResponse } from "next/server";
import { getProfile } from "@/lib/mock/profile";

// MOCK — GET /api/vendors/{slug} (zrcalo .NET VendorsController.Get)
export function GET(_req: Request, { params }: { params: { slug: string } }) {
  const data = getProfile(params.slug);
  if (!data) return NextResponse.json({ error: "not_found" }, { status: 404 });
  return NextResponse.json(data);
}
