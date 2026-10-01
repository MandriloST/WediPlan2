"use client";

import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { adminApi, providerMessage } from "@/lib/api/provider";
import { AuthError } from "@/lib/api/auth";
import { actionLabel, actorLabel, entityLabel, formatWhen, summarizeChanges } from "@/lib/audit";
import {
  CONSENT_CHANNELS, CONSENT_SCOPES, CONSENT_STATUSES, DATA_SOURCES, consentSummaryLine, fromDateInput, toDateInput,
} from "@/lib/provenance";
import { useAuth } from "@/stores/auth";
import type {
  AdminAuditEntry, AdminClaim, AdminConsentSummary, AdminImportedReview, AdminOptOut, AdminPhoto, AdminProvenance, AdminReview,
} from "@/lib/types";

/** Oblik forme za porijeklo/privolu: datumi kao "yyyy-MM-dd" (input type=date), ostalo tekst. */
interface ProvForm {
  dataSource: string; dataCollectedAt: string; consentStatus: string; consentRequestedAt: string; consentAt: string;
  consentChannel: string; consentScope: string[]; consentNote: string; googlePlaceId: string;
}
const toForm = (p: AdminProvenance): ProvForm => ({
  dataSource: p.dataSource ?? "", dataCollectedAt: toDateInput(p.dataCollectedAt), consentStatus: p.consentStatus,
  consentRequestedAt: toDateInput(p.consentRequestedAt), consentAt: toDateInput(p.consentAt),
  consentChannel: p.consentChannel ?? "", consentScope: [...p.consentScope], consentNote: p.consentNote ?? "",
  googlePlaceId: p.googlePlaceId ?? "",
});

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
  // §Zadatak 15 — post-moderacija fotografija (slike su javne odmah; admin vodi evidenciju i sakriva neprimjerene)
  const [photos, setPhotos] = useState<AdminPhoto[]>([]);
  const [photoStatus, setPhotoStatus] = useState<"unreviewed" | "approved" | "flagged">("unreviewed");
  // §Zadatak 17 — porijeklo podataka i privola (uz isti slug kao povijest promjena) + sažetak kampanje
  const [consent, setConsent] = useState<AdminConsentSummary | null>(null);
  const [prov, setProv] = useState<{ slug: string; data: AdminProvenance } | null>(null);
  const [provForm, setProvForm] = useState<ProvForm | null>(null);
  const [provBusy, setProvBusy] = useState(false);
  const [provSaved, setProvSaved] = useState(false);
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
      adminApi.photos(photoStatus, 60),
      adminApi.consentSummary(),
    ])
      .then(([c, r, o, i, ph, cs]) => { setClaims(c); setReviews(r); setOptouts(o); setImported(i); setPhotos(ph); setConsent(cs); })
      .catch((e) => setError(providerMessage(e)));
  }, [reviewStatus, impStatus, photoStatus]);

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

  async function saveProv() {
    if (!prov || !provForm) return;
    setError(null); setProvSaved(false); setProvBusy(true);
    try {
      const orig = prov.data;
      const saved = await adminApi.saveProvenance(prov.slug, {
        dataSource: provForm.dataSource || null,
        dataCollectedAt: fromDateInput(provForm.dataCollectedAt, orig.dataCollectedAt),
        consentStatus: provForm.consentStatus as AdminProvenance["consentStatus"],
        consentRequestedAt: fromDateInput(provForm.consentRequestedAt, orig.consentRequestedAt),
        consentAt: fromDateInput(provForm.consentAt, orig.consentAt),
        consentChannel: provForm.consentChannel || null,
        consentScope: provForm.consentScope,
        consentNote: provForm.consentNote.trim() || null,
        googlePlaceId: provForm.googlePlaceId.trim() || null,
      });
      setProv({ slug: prov.slug, data: saved });
      setProvForm(toForm(saved));
      setProvSaved(true);
      // osvježi povijest (izmjena je zabilježena) i sažetak privole
      setAudit(await adminApi.audit(prov.slug, 100));
      adminApi.consentSummary().then(setConsent).catch(() => {});
    } catch (e) {
      setError(providerMessage(e));
    } finally {
      setProvBusy(false);
    }
  }

  const setPF = (patch: Partial<ProvForm>) => { setProvSaved(false); setProvForm((f) => (f ? { ...f, ...patch } : f)); };

  async function loadAudit(ev: React.FormEvent) {
    ev.preventDefault();
    setError(null);
    setAuditBusy(true);
    setProvSaved(false);
    try {
      const slug = auditSlug.trim();
      const [entries, p] = await Promise.all([
        adminApi.audit(slug || undefined, 100),
        slug ? adminApi.provenance(slug) : Promise.resolve(null),
      ]);
      setAudit(entries);
      setProv(p ? { slug, data: p } : null);
      setProvForm(p ? toForm(p) : null);
    } catch (e) {
      setAudit(null); setProv(null); setProvForm(null);
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

      <h2 className="admin-h2" style={{ marginTop: 28 }}>Fotografije pružatelja — pregled ({photos.length})</h2>
      <p className="muted" style={{ fontSize: 13, margin: "0 0 10px" }}>
        Slike koje partneri učitaju su <strong>javne odmah</strong>; ovdje vodiš evidenciju koje si pregledao. „U redu“ je samo evidencija,
        a „Sakrij…“ odmah uklanja sliku s javnog profila i karte (razlog vidi vlasnik profila). Klik na sliku otvara punu veličinu.
      </p>
      <div style={{ display: "flex", gap: 8, flexWrap: "wrap", alignItems: "center", margin: "0 0 10px" }}>
        <select
          className="audit-input"
          style={{ maxWidth: 220 }}
          value={photoStatus}
          onChange={(e) => setPhotoStatus(e.target.value as typeof photoStatus)}
          aria-label="Status fotografija"
        >
          <option value="unreviewed">Nepregledane</option>
          <option value="approved">Pregledane (u redu)</option>
          <option value="flagged">Skrivene</option>
        </select>
        {photoStatus === "unreviewed" && photos.length > 0 && (
          <button
            className="btn btn-primary btn-sm"
            onClick={() => act(() => adminApi.approvePhotosBatch(photos.map((p) => p.id)))}
          >
            Odobri sve prikazane ({photos.length})
          </button>
        )}
      </div>
      {photos.length === 0 ? (
        <p className="muted">Nema fotografija u ovom statusu.</p>
      ) : (
        <ul className="photo-admin-grid">
          {photos.map((p) => (
            <li key={p.id} className="photo-card">
              <a href={p.url} target="_blank" rel="noopener noreferrer" title="Otvori punu veličinu">
                {/* eslint-disable-next-line @next/next/no-img-element */}
                <img src={p.thumbUrl} alt={`Fotografija: ${p.vendorName}`} loading="lazy" />
              </a>
              <div className="photo-card-body">
                <Link href={`/pruzatelj/${p.vendorSlug}`}>{p.vendorName}</Link>
                <p className="muted photo-card-meta">
                  {p.source === "partner" ? "partner" : p.source}
                  {p.createdAt ? ` · ${formatWhen(p.createdAt)}` : " · datum nepoznat (prije evidencije)"}
                  {p.uploaderEmail ? ` · ${p.uploaderEmail}` : ""}
                </p>
                <p className="muted photo-card-meta">
                  {p.rightsConfirmedAt ? `prava potvrđena ${formatWhen(p.rightsConfirmedAt)}` : "prava nisu potvrđena (starija slika)"}
                </p>
                {p.moderationStatus !== "unreviewed" && (
                  <p className="muted photo-card-meta">
                    {p.moderationStatus === "flagged" ? "Skrio/la" : "Pregledao/la"}: {p.reviewerEmail ?? "— (nepoznato)"}
                    {p.reviewedAt ? ` · ${formatWhen(p.reviewedAt)}` : ""}
                    {p.moderationStatus === "flagged" && p.moderationNote ? ` · razlog: ${p.moderationNote}` : ""}
                  </p>
                )}
                <div className="photo-card-actions">
                  {p.moderationStatus === "unreviewed" && (
                    <button className="btn btn-primary btn-sm" onClick={() => act(() => adminApi.approvePhoto(p.id))}>U redu</button>
                  )}
                  {p.moderationStatus !== "flagged" && (
                    <button
                      className="btn btn-sm"
                      onClick={() => {
                        // razlog je OBAVEZAN i vidi ga vlasnik profila (+ e-mail ako je profil preuzet)
                        const note = window.prompt("Razlog skrivanja (obavezno — vidi ga vlasnik profila):");
                        if (note === null) return;
                        if (!note.trim()) { setError(providerMessage(new AuthError("note_required", 400))); return; }
                        act(() => adminApi.flagPhoto(p.id, note.trim()));
                      }}
                    >
                      Sakrij…
                    </button>
                  )}
                  {p.moderationStatus === "flagged" && (
                    <button className="btn btn-sm" onClick={() => act(() => adminApi.unflagPhoto(p.id))}>Vrati u prikaz</button>
                  )}
                </div>
              </div>
            </li>
          ))}
        </ul>
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
      {consent && (
        <p className="muted" style={{ fontSize: 13, margin: "0 0 10px" }}>
          <strong>Privola pružatelja:</strong> {consentSummaryLine(consent)}
        </p>
      )}
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
      {prov && provForm && (
        <div className="prov-box">
          <h3 className="prov-box-title">Porijeklo podataka i privola — <code>{prov.slug}</code></h3>
          <p className="muted" style={{ fontSize: 12.5, margin: "0 0 10px" }}>
            Interno — nikad javno. Spremanje zamjenjuje sva polja. „Odbijeno“ uz to skriva profil (opt-out); vraćanje u prikaz je zasebna radnja.
          </p>
          <div className="prov-grid">
            <label>Izvor podataka
              <select className="audit-input" value={provForm.dataSource} onChange={(e) => setPF({ dataSource: e.target.value })}>
                <option value="">— nije navedeno —</option>
                {DATA_SOURCES.map(([v, l]) => <option key={v} value={v}>{l}</option>)}
              </select>
            </label>
            <label>Datum prikupljanja
              <input className="audit-input" type="date" value={provForm.dataCollectedAt} onChange={(e) => setPF({ dataCollectedAt: e.target.value })} />
            </label>
            <label>Status privole
              <select className="audit-input" value={provForm.consentStatus} onChange={(e) => setPF({ consentStatus: e.target.value })}>
                {CONSENT_STATUSES.map(([v, l]) => <option key={v} value={v}>{l}</option>)}
              </select>
            </label>
            <label>Privola zatražena
              <input className="audit-input" type="date" value={provForm.consentRequestedAt} onChange={(e) => setPF({ consentRequestedAt: e.target.value })} />
            </label>
            <label>Privola dana
              <input className="audit-input" type="date" value={provForm.consentAt} onChange={(e) => setPF({ consentAt: e.target.value })} />
            </label>
            <label>Kanal
              <select className="audit-input" value={provForm.consentChannel} onChange={(e) => setPF({ consentChannel: e.target.value })}>
                <option value="">— nije navedeno —</option>
                {CONSENT_CHANNELS.map(([v, l]) => <option key={v} value={v}>{l}</option>)}
              </select>
            </label>
            <label>Google Place ID
              <input className="audit-input" value={provForm.googlePlaceId} maxLength={300} placeholder="ChIJ…" onChange={(e) => setPF({ googlePlaceId: e.target.value })} />
            </label>
          </div>
          <fieldset className="prov-scope">
            <legend>Opseg privole</legend>
            {CONSENT_SCOPES.map(([v, l]) => (
              <label key={v}>
                <input
                  type="checkbox"
                  checked={provForm.consentScope.includes(v)}
                  onChange={(e) => setPF({ consentScope: e.target.checked ? [...provForm.consentScope, v] : provForm.consentScope.filter((x) => x !== v) })}
                />{" "}{l}
              </label>
            ))}
          </fieldset>
          <label className="prov-note">Napomena o privoli
            <input className="audit-input" style={{ maxWidth: "100%" }} value={provForm.consentNote} maxLength={1000}
              placeholder="npr. pristao u DM-u 12.9., čeka potvrdu za slike" onChange={(e) => setPF({ consentNote: e.target.value })} />
          </label>
          <div style={{ display: "flex", gap: 10, alignItems: "center", marginTop: 10 }}>
            <button className="btn btn-primary btn-sm" onClick={saveProv} disabled={provBusy}>{provBusy ? "Spremam…" : "Spremi"}</button>
            {provSaved && <span className="muted" style={{ fontSize: 13 }}>Spremljeno.</span>}
          </div>
        </div>
      )}
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
