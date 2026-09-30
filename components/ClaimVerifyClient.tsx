"use client";

import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { AuthError } from "@/lib/api/auth";
import { claimApi, providerMessage } from "@/lib/api/provider";

/**
 * §Zadatak 5 — potvrda vlasništva claima. Ruta iz e-maila (poveznica vodi ovamo s ?token=).
 * Isti obrazac kao TokenAction (auto-fire na mount preko useEffect): mail-skener koji GET-om
 * "posjeti" link samo dohvati HTML, ne izvršava JS — token se troši tek POST-om koji šalje OVAJ
 * kod, a ne sam GET zahtjev na poveznicu.
 */
export default function ClaimVerifyClient() {
  const params = useSearchParams();
  const token = params.get("token");
  const [state, setState] = useState<"pending" | "approved" | "verified" | "error">("pending");
  const [error, setError] = useState<string | null>(null);
  const ran = useRef(false);

  useEffect(() => {
    if (ran.current) return; // React StrictMode dvostruki effect — pokreni jednom
    ran.current = true;
    if (!token) {
      setError("Nedostaje token u poveznici.");
      setState("error");
      return;
    }
    (async () => {
      try {
        const res = await claimApi.verify(token);
        setState(res.status);
      } catch (err) {
        if (err instanceof AuthError && err.status === 401) {
          setError("Prijavite se pa ponovno otvorite ovu poveznicu iz e-maila.");
        } else {
          setError(providerMessage(err));
        }
        setState("error");
      }
    })();
  }, [token]);

  if (state === "pending") {
    return (
      <div className="auth-sent">
        <p aria-live="polite">Potvrđujemo vlasništvo…</p>
        <span className="spinner" aria-hidden />
      </div>
    );
  }

  if (state === "error") {
    return (
      <div className="auth-sent">
        <div className="auth-error" role="alert">{error}</div>
        <Link className="btn btn-sm" href="/prijava">Prijava</Link>
      </div>
    );
  }

  return (
    <div className="auth-sent">
      <p className="fit good" role="status">
        {state === "approved"
          ? "✓ Vlasništvo potvrđeno — profil je odmah preuzet."
          : "✓ Dokaz je zabilježen. Zahtjev čeka pregled našeg tima."}
      </p>
      <Link className="btn btn-primary btn-sm" href="/partner" style={{ marginTop: 10 }}>
        Otvori nadzornu ploču
      </Link>
    </div>
  );
}
