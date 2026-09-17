import type { Metadata } from "next";
import ProviderDashboard from "@/components/ProviderDashboard";

export const metadata: Metadata = { title: "Za partnere — Wediplan", robots: { index: false, follow: false } };
export const dynamic = "force-dynamic";

export default function PartnerPage() {
  return <ProviderDashboard />;
}
