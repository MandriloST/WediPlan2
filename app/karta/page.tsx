import type { Metadata } from "next";
import MapPageShell from "@/components/MapPageShell";
import { CATEGORY_BY_SLUG } from "@/lib/data";

export const metadata: Metadata = { title: "Karta — Wediplan", robots: { index: false, follow: true } };

export default function MapPage({ searchParams }: { searchParams: { kategorija?: string } }) {
  const k = searchParams.kategorija;
  return <MapPageShell category={k && CATEGORY_BY_SLUG[k] ? k : undefined} />;
}
