import type { Metadata } from "next";
import ExploreShell from "@/components/ExploreShell";
import { getCategories } from "@/lib/api/server";
import type { CategoryWithCount } from "@/lib/types";

export const metadata: Metadata = {
  title: "Sve kategorije — Wediplan",
  description:
    "Svih 29 kategorija za vjenčanje u Hrvatskoj — dvorane, fotografi, glazba, catering i ostalo, s brojem pružatelja po regiji.",
  alternates: { canonical: "/kategorije" },
};

export const revalidate = 60;

/** Grid svih kategorija (bivši landing, §L) — od redizajna naslovnice (3a) na vlastitoj ruti. */
export default async function CategoriesPage() {
  let initialCategories: CategoryWithCount[] | undefined;
  try {
    initialCategories = await getCategories();
  } catch (e) {
    console.error("[kategorije] SSR dohvat nije uspio:", e);
  }
  return <ExploreShell filters={{}} initialCategories={initialCategories} />;
}
