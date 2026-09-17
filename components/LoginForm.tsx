"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";
import { authApi } from "@/lib/api/auth";
import { authMessage, useAuth } from "@/stores/auth";
import { syncLocalToAccount } from "@/lib/sync";

/** Sigurna interna putanja za povratak nakon prijave (spriječi open-redirect). */
function safeNext(raw: string | null): string {
  return raw && raw.startsWith("/") && !raw.startsWith("//") ? raw : "/";
}

export default function LoginForm() {
  const router = useRouter();
  const params = useSearchParams();
  const next = safeNext(params.get("next"));
  const { providers, setUser } = useAuth();

  const [mode, setMode] = useState<"password" | "magic">("password");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [magicSent, setMagicSent] = useState(false);

  async function onPasswordLogin(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const user = await authApi.login(email.trim(), password);
      setUser(user);
      await syncLocalToAccount(); // prebaci localStorage favorite/plan u account
      router.push(next);
      router.refresh();
    } catch (err) {
      setError(authMessage(err));
    } finally {
      setBusy(false);
    }
  }

  async function onMagicRequest(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await authApi.requestMagic(email.trim());
      setMagicSent(true);
    } catch {
      // anti-enumeracija: uvijek "poslano"
      setMagicSent(true);
    } finally {
      setBusy(false);
    }
  }

  if (magicSent) {
    return (
      <div className="auth-sent">
        <p>
          Ako račun postoji, poslali smo poveznicu za prijavu na <strong>{email}</strong>.
        </p>
        <p className="muted">Provjerite poštu (i spam). Poveznica vrijedi 15 minuta.</p>
        <button className="btn btn-sm" onClick={() => setMagicSent(false)}>
          ← Natrag
        </button>
      </div>
    );
  }

  return (
    <>
      {providers?.google && (
        <>
          <a className="btn btn-google" href={authApi.googleUrl(next)}>
            <span aria-hidden>G</span> Nastavi s Googleom
          </a>
          <div className="auth-or">ili</div>
        </>
      )}

      <div className="auth-tabs" role="tablist">
        <button
          role="tab"
          aria-selected={mode === "password"}
          className={mode === "password" ? "active" : ""}
          onClick={() => setMode("password")}
        >
          Lozinka
        </button>
        <button
          role="tab"
          aria-selected={mode === "magic"}
          className={mode === "magic" ? "active" : ""}
          onClick={() => setMode("magic")}
        >
          Poveznica na e-mail
        </button>
      </div>

      {error && <div className="auth-error" role="alert">{error}</div>}

      {mode === "password" ? (
        <form onSubmit={onPasswordLogin} className="auth-form">
          <label>
            E-mail
            <input type="email" autoComplete="email" required value={email}
              onChange={(e) => setEmail(e.target.value)} />
          </label>
          <label>
            Lozinka
            <input type="password" autoComplete="current-password" required value={password}
              onChange={(e) => setPassword(e.target.value)} />
          </label>
          <button className="btn btn-primary" type="submit" disabled={busy}>
            {busy ? "Prijava…" : "Prijavi se"}
          </button>
          <Link className="auth-link" href="/prijava/zaboravljena">
            Zaboravljena lozinka?
          </Link>
        </form>
      ) : (
        <form onSubmit={onMagicRequest} className="auth-form">
          <p className="muted">Poslat ćemo vam poveznicu za prijavu bez lozinke.</p>
          <label>
            E-mail
            <input type="email" autoComplete="email" required value={email}
              onChange={(e) => setEmail(e.target.value)} />
          </label>
          <button className="btn btn-primary" type="submit" disabled={busy}>
            {busy ? "Šaljem…" : "Pošalji poveznicu"}
          </button>
        </form>
      )}
    </>
  );
}
