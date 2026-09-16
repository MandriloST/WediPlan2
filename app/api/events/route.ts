import { NextResponse } from "next/server";

// MOCK — POST /api/events (§A). Bez API_URL eventi se odbacuju (tihi 204, kao .NET).
// Za lokalno praćenje što se šalje: ANALYTICS_DEBUG=1 npm run dev → ispis u terminal.
export async function POST(req: Request) {
  if (process.env.ANALYTICS_DEBUG === "1") {
    try {
      console.log("[events]", JSON.stringify(await req.json()));
    } catch {
      /* ignore */
    }
  }
  return new NextResponse(null, { status: 204 });
}
