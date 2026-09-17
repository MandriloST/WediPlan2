"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import type { Me } from "@/lib/api/auth";
import { authMessage, useAuth } from "@/stores/auth";
import { syncLocalToAccount } from "@/lib/sync";

/**
 * Zajednički obrazac za "klik na poveznicu iz e-maila": pročita ?token=, pozove akciju,
 * prijavi korisnika i preusmjeri. Koristi magic-consume i verify-email.
 */
export default function TokenAction({
  action,
  pending,
  success,
}: {
  action: (token: string) => Promise<Me>;
  pending: string;
  success: string;
}) {
  const router = useRouter();
  const params = useSearchParams();
  const token = params.get("token");
  const setUser = useAuth((s) => s.setUser);
  const [error, setError] = useState<string | null>(null);
  const ran = useRef(false);

  useEffect(() => {
    if (ran.current) return; // React StrictMode dvostruki effect — pokreni jednom
    ran.current = true;
    if (!token) {
      setError("Nedostaje token u poveznici.");
      return;
    }
    (async () => {
      try {
        const user = await action(token);
        setUser(user);
        await syncLocalToAccount();
        router.push("/profil");
        router.refresh();
      } catch (err) {
        setError(authMessage(err));
      }
    })();
  }, [token, action, setUser, router]);

  if (error) {
    return (
      <div className="auth-sent">
        <div className="auth-error" role="alert">{error}</div>
        <Link className="btn btn-sm" href="/prijava">
          Natrag na prijavu
        </Link>
      </div>
    );
  }
  return (
    <div className="auth-sent">
      <p aria-live="polite">{pending}</p>
      <span className="spinner" aria-hidden />
    </div>
  );
}
