"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useState } from "react";
import { AuthError } from "@/lib/api/auth";
import { claimApi, providerMessage } from "@/lib/api/provider";
import { useAuth } from "@/stores/auth";
import type { Claim, Vendor } from "@/lib/types";

/**
 * Preuzimanje profila (§6). Prikazuje se na neclaimanom profilu:
 *  - gost → poziv na prijavu,
 *  - prijavljen → gumb "Ovo je moj profil" → forma (poruka) → zahtjev u moderaciju.
 * Nakon slanja: potvrda + link na nadzornu ploču partnera (uređivanje drafta odmah), plus
 * (§Zadatak 5) mogućnost odmah potvrditi vlasništvo e-mailom umjesto čekanja ručnog pregleda.
 */
export default function ClaimPanel({ vendor }: { vendor: Vendor }) {
  const { user, loading } = useAuth();
  const pathname = usePathname();
  const [open, setOpen] = useState(false);
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [claim, setClaim] = useState<Claim | null>(null);

  const [verifyBusy, setVerifyBusy] = useState(false);
  const [verifyError, setVerifyError] = useState<string | null>(null);
  const [noEmailOnFile, setNoEmailOnFile] = useState(false);
  const [sentTo, setSentTo] = useState<string | null>(null);

  if (vendor.claimStatus === "claimed") return null; // već preuzet — bez CTA
  if (loading) return null;

  async function sendVerification() {
    if (!claim) return;
    setVerifyBusy(true); setVerifyError(null);
    try {
      const res = await claimApi.sendVerification(claim.id);
      setSentTo(res.sentTo);
    } catch (err) {
      if (err instanceof AuthError && err.code === "no_email_on_file") setNoEmailOnFile(true);
      else setVerifyError(providerMessage(err));
    } finally {
      setVerifyBusy(false);
    }
  }

  if (claim) {
    return (
      <div className="claim-box">
        <p className="fit good" style={{ margin: 0 }}>
          ✓ Zahtjev je zaprimljen. Do odobrenja možete uređivati profil u nadzornoj ploči — promjene
          postaju javne nakon provjere.
        </p>
        <Link href="/partner" className="btn btn-primary btn-sm" style={{ marginTop: 10 }}>
          Otvori nadzornu ploču
        </Link>

        {claim.evidence !== "email_verified" && (
          <div style={{ marginTop: 14, paddingTop: 14, borderTop: "1px solid var(--border, #e5e5e5)" }}>
            {noEmailOnFile ? (
              <p className="muted" style={{ fontSize: 13.5, margin: 0 }}>
                Za ovaj profil nemamo e-mail adresu na koju bismo poslali potvrdu — preuzimanje
                odobrava naš tim ručno, obično u roku nekoliko dana.
              </p>
            ) : sentTo ? (
              <p className="muted" style={{ fontSize: 13.5, margin: 0 }}>
                Poslali smo poveznicu za potvrdu na <strong>{sentTo}</strong>. Provjerite taj inbox
                (i mapu neželjene pošte) — klikom na poveznicu profil se odmah preuzima.
              </p>
            ) : (
              <>
                <p className="muted" style={{ fontSize: 13.5, margin: "0 0 8px" }}>
                  Imate brži put: potvrdite vlasništvo e-mailom koji imamo za ovaj profil i
                  preuzimanje je odmah odobreno, bez čekanja ručnog pregleda.
                </p>
                {verifyError && <p className="auth-error" role="alert" style={{ fontSize: 13 }}>{verifyError}</p>}
                <button className="btn btn-sm" disabled={verifyBusy} onClick={sendVerification}>
                  {verifyBusy ? "Šaljem…" : "Potvrdi vlasništvo e-mailom"}
                </button>
              </>
            )}
          </div>
        )}
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
      const created = await claimApi.create(vendor.slug, message.trim() || undefined);
      setClaim(created);
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
