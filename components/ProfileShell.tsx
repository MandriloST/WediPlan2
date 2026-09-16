"use client";

import Link from "next/link";
import { useEffect } from "react";
import { useQuery } from "@tanstack/react-query";
import { GROUP_LABELS, REGIONS } from "@/lib/data";
import { api } from "@/lib/api/client";
import { euro } from "@/lib/format";
import VendorCard from "./VendorCard";
import { useBudget, useFavorites } from "@/stores";

export default function ProfileShell() {
  const favorites = useFavorites((s) => s.ids);
  const plan = useBudget((s) => s.plan);
  const prune = useFavorites((s) => s.prune);
  const { data: favVendors = [], isLoading, isError, isPlaceholderData } = useQuery({
    queryKey: ["vendors-by-ids", "fav", favorites],
    queryFn: ({ signal }) => api.vendorsByIds(favorites, signal),
    enabled: favorites.length > 0,
    placeholderData: (prev) => prev,
  });
  // makni favorite koji više ne postoje — samo nakon USPJEŠNOG odgovora
  useEffect(() => {
    if (favorites.length && !isLoading && !isError && !isPlaceholderData && favVendors.length !== favorites.length)
      prune(favVendors.map((v) => v.id), favorites);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [favVendors, isLoading, isError, isPlaceholderData]);

  return (
    <main className="container page">
      <h1>♡ Profil</h1>
      <p className="sub">
        Favoriti i plan spremaju se na ovom uređaju (offline). Registracijom (e-mail ili Google) sinkroniziraju se
        s računom — uskoro.
      </p>
      <p>
        <button className="btn btn-sm" disabled title="Uskoro">
          Prijava e-mailom
        </button>{" "}
        <button className="btn btn-sm" disabled title="Uskoro">
          Prijava Googleom
        </button>
      </p>

      {plan && (
        <>
          <h2 style={{ fontFamily: "var(--font-display)", fontWeight: 550, fontSize: 20 }}>Vaš plan</h2>
          <p>
            {REGIONS.find((r) => r.id === plan.region)?.name} · {plan.guests} gostiju ·{" "}
            <span className="price">{euro(plan.total)}</span>
            {"  "}
            {Object.entries(plan.caps).map(([g, cap]) => (
              <span key={g} className="badge" style={{ marginLeft: 6 }}>
                {GROUP_LABELS[g]} ≤ {euro(cap)}
              </span>
            ))}
          </p>
        </>
      )}

      <h2 style={{ fontFamily: "var(--font-display)", fontWeight: 550, fontSize: 20 }}>Favoriti</h2>
      {favorites.length > 0 && isLoading ? (
        <div className="results-grid">
          {favorites.slice(0, 6).map((id) => (
            <div key={id} className="skel" />
          ))}
        </div>
      ) : favorites.length > 0 && isError ? (
        <p className="muted">Favoriti su sačuvani na ovom uređaju, ali podaci trenutno nisu dostupni.</p>
      ) : favVendors.length === 0 ? (
        <div className="empty">
          <h3>Još nema favorita</h3>
          <p>Dodirnite ♡ na kartici pružatelja da ga spremite.</p>
          <div className="actions">
            <Link href="/" className="btn btn-primary btn-sm">
              Istraži pružatelje
            </Link>
          </div>
        </div>
      ) : (
        <div className="results-grid">
          {favVendors.map((v) => (
            <VendorCard key={v.id} vendor={v} />
          ))}
        </div>
      )}
    </main>
  );
}
