"use client";

import Link from "next/link";
import { useState } from "react";
import { authApi } from "@/lib/api/auth";
import { authMessage } from "@/stores/auth";

export default function RegisterForm() {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sent, setSent] = useState(false);

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (password.length < 8) {
      setError("Lozinka mora imati najmanje 8 znakova.");
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await authApi.register(email.trim(), password, displayName.trim() || undefined);
      setSent(true);
    } catch (err) {
      setError(authMessage(err));
    } finally {
      setBusy(false);
    }
  }

  if (sent) {
    return (
      <div className="auth-sent">
        <p>
          Poslali smo poveznicu za potvrdu na <strong>{email}</strong>.
        </p>
        <p className="muted">Otvorite je da dovršite registraciju (provjerite i spam).</p>
      </div>
    );
  }

  return (
    <>
      {error && <div className="auth-error" role="alert">{error}</div>}
      <form onSubmit={onSubmit} className="auth-form">
        <label>
          Ime (nije obavezno)
          <input type="text" autoComplete="name" value={displayName}
            onChange={(e) => setDisplayName(e.target.value)} />
        </label>
        <label>
          E-mail
          <input type="email" autoComplete="email" required value={email}
            onChange={(e) => setEmail(e.target.value)} />
        </label>
        <label>
          Lozinka
          <input type="password" autoComplete="new-password" required minLength={8} value={password}
            onChange={(e) => setPassword(e.target.value)} />
          <span className="hint">najmanje 8 znakova</span>
        </label>
        <button className="btn btn-primary" type="submit" disabled={busy}>
          {busy ? "Kreiram račun…" : "Registriraj se"}
        </button>
      </form>
    </>
  );
}
