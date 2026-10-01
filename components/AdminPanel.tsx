"use client";

import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { adminApi, providerMessage } from "@/lib/api/provider";
import { AuthError } from "@/lib/api/auth";
import { actionLabel, actorLabel, entityLabel, formatWhen, summarizeChanges } from "@/lib/audit";
import { useAuth } from "@/stores/auth";
import type { AdminAuditEntry, AdminClaim, AdminImportedReview, AdminOptOut, AdminReview } from "@/lib/types";

/** Minimalno admin sučelje (§6): moderacija claimova i korisničkih recenzija. Samo rola admin. */
export default function AdminPanel() {
  const { user, loading } = useAuth();
  const router = useRouter();
  const [claims, setClaims] = useState<AdminClaim[]>([]);
  const [reviews, setReviews] = useState<AdminReview[]>([]);
  // §Zadatak 16 — filter korisničkih recenzija, te provjera uvezenih recenzija ("što oni kažu")
  const [reviewStatus, setReviewStatus] = useState<"pending" | "published" | "rejected">("pending");
  const [imported, setImported] = useState<AdminImportedReview[]>([]);
  const [impStatus, setImpStatus] = useState<"unverified" | "verified" | "rejected">("unverified");
  const [notes, setNotes] = useState<Record<string, string>>({});
  const [optouts, setOptouts] = useState<AdminOptOut[]>([]);
  const [error, setError] = useState<string | null>(null);
  // §Zadatak 14 — povijest promjena (GDPR). null = još nije učitano.
  const [auditSlug, setAuditSlug] = useState("");
  const [audit, setAudit] = useState<AdminAuditEntry[] | null>(null);
  const [auditBusy, setAuditBusy] = useState(false);
  const isAdmin = !!user?.roles.includes("admin");

  const load = useCallback(() => {
    Promise.all([
      adminApi.claims("pending"),
      adminApi.reviews(reviewStatus),
      adminApi.optouts(),
      adminApi.importedReviews(impStatus),
    ])
      .then(([c, r, o, i]) => { setClaims(c); setReviews(r); setOptouts(o); setImported(i); })
      .catch((e) => setError(providerMessage(e)));
  }, [reviewStatus, impStatus]);

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

  async function loadAudit(ev: React.FormEvent) {
    ev.preventDefault();
    setError(null);
    setAuditBusy(true);
    try {
      setAudit(await adminApi.audit(auditSlug.trim() || undefined, 100));
    } catch (e) {
      setAudit(null);
      setError(e instanceof AuthError && e.status === 404 ? "Pružatelj s tim slugom ne postoji." : providerMessage(e));
    } finally {
      setAuditBusy(false);
    }
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
                  {c.evidence === "email_verified" && <span className="badge verified">✓ e-mail potvrđen</span>}
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

      <h2 className="admin-h2" style={{ marginTop: 28 }}>
        Recenzije korisnika ({reviews.length})
      </h2>
      <p style={{ margin: "0 0 10px" }}>
        <select
          className="audit-input"
          style={{ maxWidth: 220 }}
          value={reviewStatus}
          onChange={(e) => setReviewStatus(e.target.value as typeof reviewStatus)}
          aria-label="Status recenzija korisnika"
        >
          <option value="pending">Na čekanju</option>
          <option value="published">Objavljene</option>
          <option value="rejected">Odbijene</option>
        </select>
      </p>
      {reviews.length === 0 ? (
        <p className="muted">Nema recenzija u ovom statusu.</p>
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
                {r.status !== "pending" && (
                  <p className="muted" style={{ fontSize: 12.5, margin: "6px 0 0" }}>
                    {r.status === "rejected" ? "Odbio/la" : "Objavio/la"}: {r.deciderEmail ?? "— (nepoznato)"}
                    {r.decidedAt ? ` · ${formatWhen(r.decidedAt)}` : ""}
                    {r.status === "rejected" && r.rejectReason ? ` · razlog: ${r.rejectReason}` : ""}
                  </p>
                )}
              </div>
              {r.status === "pending" && (
                <div className="admin-actions">
                  <button className="btn btn-primary btn-sm" onClick={() => act(() => adminApi.approveReview(r.id))}>Objavi</button>
                  <button
                    className="btn btn-sm"
                    onClick={() => {
                      // razlog je interni (ne prikazuje se javno ni autoru); Odustani = ne odbijaj
                      const reason = window.prompt("Razlog odbijanja (neobavezno; interno, ne prikazuje se autoru):");
                      if (reason === null) return;
                      act(() => adminApi.rejectReview(r.id, reason));
                    }}
                  >
                    Odbij
                  </button>
                </div>
              )}
            </article>
          ))}
        </div>
      )}

      <h2 className="admin-h2" style={{ marginTop: 28 }}>Uvezene recenzije — provjera izvora ({imported.length})</h2>
      <p className="muted" style={{ fontSize: 13, margin: "0 0 10px" }}>
        Recenzije koje je Wediplan prenio iz vanjskih izvora. „Provjereno“ daje bedž na profilu; „Odbij“ ih skriva s profila.
        Napomena o dokazu (npr. gdje je screenshot) čuva se uz odluku. Odluka ostaje i nakon ponovnog Excel uvoza.
      </p>
      <p style={{ margin: "0 0 10px" }}>
        <select
          className="audit-input"
          style={{ maxWidth: 220 }}
          value={impStatus}
          onChange={(e) => setImpStatus(e.target.value as typeof impStatus)}
          aria-label="Status uvezenih recenzija"
        >
          <option value="unverified">Neprovjerene</option>
          <option value="verified">Provjerene</option>
          <option value="rejected">Odbijene</option>
        </select>
      </p>
      {imported.length === 0 ? (
        <p className="muted">Nema uvezenih recenzija u ovom statusu.</p>
      ) : (
        <div className="admin-list">
          {imported.map((r) => (
            <article key={r.id} className="admin-item">
              <div className="admin-item-main" style={{ flex: 1 }}>
                <div>
                  <Link href={`/pruzatelj/${r.vendorSlug}`}>{r.vendorName}</Link>{" "}
                  <span className="star" style={{ color: "#d9a514" }}>{"★".repeat(Math.round(r.rating))}</span>
                </div>
                <p className="muted" style={{ fontSize: 13 }}>{r.author} · {r.source}, {r.year}.</p>
                <p className="admin-msg">{r.text}</p>
                {r.verificationStatus !== "unverified" && (
                  <p className="muted" style={{ fontSize: 12.5, margin: "6px 0 0" }}>
                    {r.verificationStatus === "verified" ? "Provjerio/la" : "Odbio/la"}: {r.verifierEmail ?? "— (nepoznato)"}
                    {r.verifiedAt ? ` · ${formatWhen(r.verifiedAt)}` : ""}
                  </p>
                )}
                <input
                  className="audit-input"
                  style={{ maxWidth: "100%", marginTop: 8 }}
                  placeholder="Napomena o dokazu (npr. screenshot u Driveu / Recenzije / …)"
                  maxLength={1000}
                  value={notes[r.id] ?? r.evidenceNote ?? ""}
                  onChange={(e) => setNotes((n) => ({ ...n, [r.id]: e.target.value }))}
                  aria-label="Napomena o dokazu"
                />
              </div>
              <div className="admin-actions">
                {r.verificationStatus !== "verified" && (
                  <button className="btn btn-primary btn-sm"
                    onClick={() => act(() => adminApi.verifyImportedReview(r.id, notes[r.id] ?? r.evidenceNote))}>
                    Provjereno
                  </button>
                )}
                {r.verificationStatus !== "rejected" && (
                  <button className="btn btn-sm"
                    onClick={() => act(() => adminApi.rejectImportedReview(r.id, notes[r.id] ?? r.evidenceNote))}>
                    Odbij
                  </button>
                )}
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

      <h2 className="admin-h2" style={{ marginTop: 28 }}>Povijest promjena</h2>
      <p className="muted" style={{ fontSize: 13, margin: "0 0 10px" }}>
        Tko je, kada i što promijenio (GDPR). Unesi slug pružatelja za njegovu povijest (uključuje slike, recenzije i
        zahtjeve za preuzimanje), ili ostavi prazno za zadnje promjene svih. Kontakti i tekstovi recenzija bilježe se samo kao „promijenjeno“.
      </p>
      <form onSubmit={loadAudit} style={{ display: "flex", gap: 8, flexWrap: "wrap", marginBottom: 12 }}>
        <input
          className="audit-input"
          placeholder="slug pružatelja (npr. foto-anic)"
          value={auditSlug}
          onChange={(e) => setAuditSlug(e.target.value)}
          aria-label="Slug pružatelja"
        />
        <button className="btn btn-primary btn-sm" type="submit" disabled={auditBusy}>
          {auditBusy ? "Učitavanje…" : "Prikaži"}
        </button>
      </form>
      {audit !== null &&
        (audit.length === 0 ? (
          <p className="muted">Nema zapisa.</p>
        ) : (
          <div className="audit-wrap">
            <table className="audit-table">
              <thead>
                <tr><th>Kada</th><th>Tko</th><th>Što</th><th>Akcija</th><th>Promjene</th></tr>
              </thead>
              <tbody>
                {audit.map((a) => (
                  <tr key={a.id}>
                    <td className="audit-when">{formatWhen(a.occurredAt)}</td>
                    <td>{actorLabel(a)}</td>
                    <td>{entityLabel(a.entityType)}</td>
                    <td>{actionLabel(a.action)}</td>
                    <td>
                      {summarizeChanges(a.changes).map((line, i) => (
                        <div key={i} className="audit-line">{line}</div>
                      ))}
                      {a.source && <div className="muted audit-src">{a.source}</div>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ))}
    </main>
  );
}
