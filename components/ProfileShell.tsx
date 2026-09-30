"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { GROUP_LABELS, REGIONS } from "@/lib/data";
import { api } from "@/lib/api/client";
import { euro } from "@/lib/format";
import VendorCard from "./VendorCard";
import { useBudget, useFavorites } from "@/stores";
import { authApi } from "@/lib/api/auth";
import { authMessage, useAuth } from "@/stores/auth";
import { useRouter } from "next/navigation";

export default function ProfileShell() {
  const user = useAuth((s) => s.user);
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
      {!user && (
        <>
          <p className="sub">
            Favoriti i plan spremaju se na ovom uređaju (offline). Prijavom (e-mail ili Google) sinkroniziraju se
            s računom.
          </p>
          <p>
            <Link className="btn btn-sm" href="/prijava">
              Prijava e-mailom
            </Link>{" "}
            <Link className="btn btn-sm" href="/prijava">
              Prijava Googleom
            </Link>
          </p>
        </>
      )}

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

      {user && <DangerZone />}
    </main>
  );
}

/** Brisanje računa (§9, Plan prioriteti #2). Prikazuje se samo prijavljenom korisniku. */
function DangerZone() {
  const router = useRouter();
  const logout = useAuth((s) => s.logout);
  const [open, setOpen] = useState(false);
  const [word, setWord] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function confirmDelete() {
    setBusy(true);
    setError(null);
    try {
      await authApi.deleteAccount(word.trim());
      // lokalno stanje (favoriti/plan na ovom uređaju) ostaje korisniku — briše se samo račun
      await logout();
      router.push("/");
    } catch (err) {
      setError(authMessage(err));
      setBusy(false);
    }
  }

  return (
    <section className="danger-zone">
      <h2 style={{ fontFamily: "var(--font-display)", fontWeight: 550, fontSize: 20 }}>Opasna zona</h2>
      {!open ? (
        <>
          <p className="muted">
            Brisanje računa je trajno: uklanja vaše favorite, plan i zahtjeve za preuzimanje profila,
            i odjavljuje vas. Ako ste vlasnik profila pružatelja, profil ostaje javan (samo prestaje
            biti povezan s vašim računom).
          </p>
          <button className="btn btn-danger btn-sm" onClick={() => setOpen(true)}>
            Obriši račun
          </button>
        </>
      ) : (
        <div className="danger-confirm">
          <p>
            Za potvrdu upišite riječ <strong>OBRISI</strong> u polje ispod. Ova radnja se ne može
            poništiti.
          </p>
          {error && <p className="auth-error" role="alert">{error}</p>}
          <div className="actions">
            <input
              className="danger-input"
              value={word}
              onChange={(e) => setWord(e.target.value)}
              placeholder="OBRISI"
              aria-label="Upišite OBRISI za potvrdu"
              autoFocus
            />
            <button
              className="btn btn-danger btn-sm"
              disabled={busy || word.trim() !== "OBRISI"}
              onClick={confirmDelete}
            >
              {busy ? "Brišem…" : "Trajno obriši račun"}
            </button>
            <button className="btn btn-sm" disabled={busy} onClick={() => { setOpen(false); setWord(""); setError(null); }}>
              Odustani
            </button>
          </div>
        </div>
      )}
    </section>
  );
}
