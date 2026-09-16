import { notFound } from "next/navigation";
import type { Metadata } from "next";
import { cache } from "react";
import VendorProfile from "@/components/VendorProfile";
import { getProfile, getSimilar } from "@/lib/api/server";
import { withProfileDefaults } from "@/lib/profile";
import { CATEGORY_BY_SLUG, homeLabel } from "@/lib/data";
import { formatPrice } from "@/lib/format";

interface Props {
  params: { slug: string };
}

// Faza 2: ~3200 profila se NE prerenderiraju pri buildu (build ne ovisi o API-ju).
// Renderiraju se na prvi zahtjev i keširaju (ISR), osvježavanje svakih 5 min.
export const revalidate = 300;
export const dynamicParams = true;
export function generateStaticParams() {
  return [];
}

// metadata i stranica dijele isti dohvat u jednom renderu
const load = cache(async (slug: string) => {
  const data = await getProfile(slug);
  return data ? withProfileDefaults(data) : null;
});

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const data = await load(params.slug);
  if (!data) return {};
  const { vendor } = data;
  const catName = CATEGORY_BY_SLUG[vendor.category]?.name ?? "";
  const place = homeLabel(vendor);
  return {
    title: `${vendor.name} — ${[catName, vendor.city].filter(Boolean).join(", ")} | Wediplan`,
    description: `${vendor.name} (${[catName, place].filter(Boolean).join(", ")}) — ${formatPrice(vendor.price)}${
      vendor.reviewCount > 0 ? `, ocjena ${vendor.rating}` : ""
    }. Usporedite cijene i dostupnost na Wediplanu.`,
    alternates: { canonical: `/pruzatelj/${vendor.slug}` },
  };
}

export default async function VendorPage({ params }: Props) {
  const data = await load(params.slug);
  if (!data) notFound();
  const similar = await getSimilar(data.vendor);
  return <VendorProfile data={data} similar={similar} />;
}
