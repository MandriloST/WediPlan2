"use client";

import { useState } from "react";
import { authApi } from "@/lib/api/auth";

export default function ForgotForm() {
  const [email, setEmail] = useState("");
  const [busy, setBusy] = useState(false);
  const [sent, setSent] = useState(false);

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    try {
      await authApi.forgotPassword(email.trim());
    } catch {
      /* anti-enumeracija */
    } finally {
      setSent(true);
      setBusy(false);
    }
  }

  if (sent)
    return (
      <div className="auth-sent">
        <p>Ako račun postoji, poslali smo upute za novu lozinku na <strong>{email}</strong>.</p>
        <p className="muted">Provjerite poštu (i spam).</p>
      </div>
    );

  return (
    <form onSubmit={onSubmit} className="auth-form">
      <p className="muted">Upišite e-mail i poslat ćemo vam poveznicu za postavljanje nove lozinke.</p>
      <label>
        E-mail
        <input type="email" autoComplete="email" required value={email}
          onChange={(e) => setEmail(e.target.value)} />
      </label>
      <button className="btn btn-primary" type="submit" disabled={busy}>
        {busy ? "Šaljem…" : "Pošalji upute"}
      </button>
    </form>
  );
}
