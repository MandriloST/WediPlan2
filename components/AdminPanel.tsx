"use client";

import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { adminApi, providerMessage } from "@/lib/api/provider";
import { useAuth } from "@/stores/auth";
import type { AdminClaim, AdminOptOut, AdminReview } from "@/lib/types";

/** Minimalno admin sučelje (§6): moderacija claimova i korisničkih recenzija. Samo rola admin. */
export default function AdminPanel() {
  const { user, loading } = useAuth();
  const router = useRouter();
  const [claims, setClaims] = useState<AdminClaim[]>([]);
  const [reviews, setReviews] = useState<AdminReview[]>([]);
  const [optouts, setOptouts] = useState<AdminOptOut[]>([]);
  const [error, setError] = useState<string | null>(null);
  const isAdmin = !!user?.roles.includes("admin");

  const load = useCallback(() => {
    Promise.all([adminApi.claims("pending"), adminApi.reviews("pending"), adminApi.optouts()])
      .then(([c, r, o]) => { setClaims(c); setReviews(r); setOptouts(o); })
      .catch((e) => setError(providerMessage(e)));
  }, []);

  useEffect(() => {
    if (loading) return;
    if (!user) { router.replace("/prijava?next=/admin"); return; }
    if (isAdmin) load();
  }, [user, loading, isAdmin, router, load]);

  if (loading || (!user && !error)) return <main className="container page"><p className="muted">Učitavanje…</p></main>;
  if (user && !isAdmin)
    return (
      <main className="container page">
        <h1>Admin</h1>
        <p className="muted">Nemate ovlasti za ovu stranicu.</p>
      </main>
    );

  async function act(fn: () => Promise<unknown>) {
    setError(null);
    try { await fn(); load(); } catch (e) { setError(providerMessage(e)); }
  }

  return (
    <main className="container page">
      <h1>Admin — moderacija</h1>
      {error && <p className="auth-error">{error}</p>}

      <h2 className="admin-h2">Zahtjevi za preuzimanje ({claims.length})</h2>
      {claims.length === 0 ? (
        <p className="muted">Nema zahtjeva na čekanju.</p>
      ) : (
        <div className="admin-list">
          {claims.map((c) => (
            <article key={c.id} className="admin-item">
              <div className="admin-item-main">
                <div>
                  <Link href={`/pruzatelj/${c.vendorSlug}`}>{c.vendorName}</Link>{" "}
                  {c.evidence === "domain_match" && <span className="badge verified">✓ domena se poklapa</span>}
                </div>
                <p className="muted" style={{ fontSize: 13 }}>
                  {c.userDisplayName ? `${c.userDisplayName} · ` : ""}{c.userEmail}
                </p>
                {c.message && <p className="admin-msg">{c.message}</p>}
              </div>
              <div className="admin-actions">
                <button className="btn btn-primary btn-sm" onClick={() => act(() => adminApi.approveClaim(c.id))}>Odobri</button>
                <button className="btn btn-sm" onClick={() => act(() => adminApi.rejectClaim(c.id))}>Odbij</button>
              </div>
            </article>
          ))}
        </div>
      )}

      <h2 className="admin-h2" style={{ marginTop: 28 }}>Recenzije za provjeru ({reviews.length})</h2>
      {reviews.length === 0 ? (
        <p className="muted">Nema recenzija na čekanju.</p>
      ) : (
        <div className="admin-list">
          {reviews.map((r) => (
            <article key={r.id} className="admin-item">
              <div className="admin-item-main">
                <div>
                  <Link href={`/pruzatelj/${r.vendorSlug}`}>{r.vendorName}</Link>{" "}
                  <span className="star" style={{ color: "#d9a514" }}>{"★".repeat(Math.round(r.rating))}</span>
                </div>
                <p className="muted" style={{ fontSize: 13 }}>{r.userEmail}</p>
                <p className="admin-msg">{r.text}</p>
              </div>
              <div className="admin-actions">
                <button className="btn btn-primary btn-sm" onClick={() => act(() => adminApi.approveReview(r.id))}>Objavi</button>
                <button className="btn btn-sm" onClick={() => act(() => adminApi.rejectReview(r.id))}>Odbij</button>
              </div>
            </article>
          ))}
        </div>
      )}

      <h2 className="admin-h2" style={{ marginTop: 28 }}>Skriveni profili — GDPR opt-out ({optouts.length})</h2>
      {optouts.length === 0 ? (
        <p className="muted">Nema skrivenih profila.</p>
      ) : (
        <div className="admin-list">
          {optouts.map((o) => (
            <article key={o.slug} className="admin-item">
              <div className="admin-item-main">
                <div><strong>{o.name}</strong> <span className="muted">({o.category})</span></div>
                <p className="muted" style={{ fontSize: 13 }}>/{o.slug}{o.isPublished ? "" : " · nije objavljen"}</p>
              </div>
              <div className="admin-actions">
                <button className="btn btn-sm" onClick={() => act(() => adminApi.restoreOptout(o.slug))}>Vrati u prikaz</button>
              </div>
            </article>
          ))}
        </div>
      )}
    </main>
  );
}
