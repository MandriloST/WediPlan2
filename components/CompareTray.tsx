"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useEffect } from "react";
import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api/client";
import { useCompare } from "@/stores";

/** Sticky pill — appears only after the first vendor is checked (README). */
export default function CompareTray() {
  const { ids, meta, dismissed, dismissTray, refresh } = useCompare();
  const pathname = usePathname();

  // localStorage može sadržavati ID-jeve iz starijih verzija (npr. mock slugovi prije
  // Faze 2) ili pružatelje koji su u međuvremenu skriveni → jednom po skupu provjeri s API-jem.
  // Briše se SAMO kad API uspješno odgovori (greška mreže ne smije isprazniti usporedbu).
  const key = [...ids].sort().join(",");
  const { data } = useQuery({
    queryKey: ["compare-validate", key],
    queryFn: async ({ signal }) => ({ asked: key.split(","), items: await api.vendorsByIds(ids, signal) }),
    enabled: ids.length > 0,
    staleTime: 10 * 60_000,
  });
  useEffect(() => {
    if (data) refresh(data.items, data.asked);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [data]);

  if (ids.length === 0 || dismissed || pathname === "/usporedba") return null;

  const initials = (id: string) => (meta[id]?.name ?? "?").slice(0, 2).toUpperCase();

  return (
    <div className="tray" role="status" aria-label="Odabrano za usporedbu">
      <div className="thumbs">
        {ids.map((id) => (
          <span key={id} className="t" title={meta[id]?.name}>
            {initials(id)}
          </span>
        ))}
      </div>
      <span className="count">
        {ids.length} {ids.length === 1 ? "odabran" : "odabrana"}
      </span>
      <Link href="/usporedba" className="btn btn-primary btn-sm">
        Usporedi ⇄
      </Link>
      <button className="close" aria-label="Sakrij" onClick={dismissTray}>
        ×
      </button>
    </div>
  );
}
