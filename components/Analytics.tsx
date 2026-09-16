"use client";

import { usePathname } from "next/navigation";
import { useEffect } from "react";
import { track } from "@/lib/analytics";
import { CATEGORY_BY_SLUG, REGION_BY_ID } from "@/lib/data";

/**
 * Automatski page_view (§A) pri svakoj promjeni putanje (i klijentskoj navigaciji).
 * Iz putanje izvlači regiju/kategoriju kao dimenzije za daily_stats — bez query stringa.
 */
export function pageDims(pathname: string): { region?: string; category?: string } {
  const segs = pathname.split("/").filter(Boolean);
  if (segs.length > 2) return {};
  const out: { region?: string; category?: string } = {};
  for (const s of segs) {
    if (s in REGION_BY_ID) out.region = s;
    else if (CATEGORY_BY_SLUG[s]) out.category = s;
  }
  return out;
}

export default function Analytics() {
  const pathname = usePathname();
  useEffect(() => {
    if (pathname) track("page_view", pageDims(pathname));
  }, [pathname]);
  return null;
}
