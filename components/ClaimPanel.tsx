"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useState } from "react";
import { claimApi, providerMessage } from "@/lib/api/provider";
import { useAuth } from "@/stores/auth";
import type { Vendor } from "@/lib/types";

/**
 * Preuzimanje profila (§6). Prikazuje se na neclaimanom profilu:
 *  - gost → poziv na prijavu,
 *  - prijavljen → gumb "Ovo je moj profil" → forma (poruka) → zahtjev u moderaciju.
 * Nakon slanja: potvrda + link na nadzornu ploču partnera (uređivanje drafta odmah).
 */
export default function ClaimPanel({ vendor }: { vendor: Vendor }) {
  const { user, loading } = useAuth();
  const pathname = usePathname();
  const [open, setOpen] = useState(false);
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sent, setSent] = useState(false);

  if (vendor.claimStatus === "claimed") return null; // već preuzet — bez CTA
  if (loading) return null;

  if (sent) {
    return (
      <div className="claim-box">
        <p className="fit good" style={{ margin: 0 }}>
          ✓ Zahtjev je zaprimljen. Do odobrenja možete uređivati profil u nadzornoj ploči — promjene
          postaju javne nakon provjere.
        </p>
        <Link href="/partner" className="btn btn-primary btn-sm" style={{ marginTop: 10 }}>
          Otvori nadzornu ploču
        </Link>
      </div>
    );
  }

  if (!user) {
    const next = pathname ? `?next=${encodeURIComponent(pathname)}` : "";
    return (
      <div className="claim-box">
        <strong>Jeste li vlasnik ovog profila?</strong>
        <p className="muted" style={{ margin: "4px 0 10px", fontSize: 13.5 }}>
          Preuzmite ga i uredite podatke, cijene i opis.
        </p>
        <Link href={`/prijava${next}`} className="btn btn-sm">
          Prijava za preuzimanje
        </Link>
      </div>
    );
  }

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true); setError(null);
    try {
      await claimApi.create(vendor.slug, message.trim() || undefined);
      setSent(true);
    } catch (err) {
      setError(providerMessage(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="claim-box">
      <strong>Jeste li vlasnik ovog profila?</strong>
      <p className="muted" style={{ margin: "4px 0 10px", fontSize: 13.5 }}>
        Preuzmite ga i uredite podatke, cijene i opis. Zahtjev pregledava naš tim.
      </p>
      {!open ? (
        <button className="btn btn-sm" onClick={() => setOpen(true)}>
          Ovo je moj profil — preuzmi ga
        </button>
      ) : (
        <form onSubmit={submit} className="claim-form">
          <label>
            Poruka (nije obavezno)
            <textarea
              rows={3}
              value={message}
              maxLength={2000}
              placeholder="Npr. poveznica na web ili kratko objašnjenje da ste vlasnik."
              onChange={(e) => setMessage(e.target.value)}
            />
          </label>
          {error && <p className="auth-error" role="alert">{error}</p>}
          <div className="actions">
            <button className="btn btn-primary btn-sm" disabled={busy} type="submit">
              {busy ? "Šaljem…" : "Pošalji zahtjev"}
            </button>
            <button className="btn btn-sm btn-ghost" type="button" onClick={() => setOpen(false)}>
              Odustani
            </button>
          </div>
        </form>
      )}
    </div>
  );
}
