"use client";

import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { useState } from "react";
import { authApi } from "@/lib/api/auth";
import { authMessage } from "@/stores/auth";

export default function ResetForm() {
  const params = useSearchParams();
  const email = params.get("email") ?? "";
  const token = params.get("token") ?? "";
  const [password, setPassword] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState(false);

  const invalid = !email || !token;

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (password.length < 8) {
      setError("Lozinka mora imati najmanje 8 znakova.");
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await authApi.resetPassword(email, token, password);
      setDone(true);
    } catch (err) {
      setError(authMessage(err));
    } finally {
      setBusy(false);
    }
  }

  if (invalid)
    return (
      <div className="auth-sent">
        <div className="auth-error" role="alert">Neispravna ili nepotpuna poveznica.</div>
        <Link className="btn btn-sm" href="/prijava/zaboravljena">Zatraži novu</Link>
      </div>
    );

  if (done)
    return (
      <div className="auth-sent">
        <p>Lozinka je promijenjena.</p>
        <Link className="btn btn-primary" href="/prijava">Prijavi se</Link>
      </div>
    );

  return (
    <>
      {error && <div className="auth-error" role="alert">{error}</div>}
      <form onSubmit={onSubmit} className="auth-form">
        <label>
          Nova lozinka
          <input type="password" autoComplete="new-password" required minLength={8} value={password}
            onChange={(e) => setPassword(e.target.value)} />
          <span className="hint">najmanje 8 znakova</span>
        </label>
        <button className="btn btn-primary" type="submit" disabled={busy}>
          {busy ? "Spremam…" : "Postavi novu lozinku"}
        </button>
      </form>
    </>
  );
}
