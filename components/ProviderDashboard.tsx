"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { providerApi, providerMessage } from "@/lib/api/provider";
import { useAuth } from "@/stores/auth";
import type { PriceModel, ProviderVendor, VendorDraft } from "@/lib/types";
import { CATEGORY_BY_SLUG } from "@/lib/data";

/**
 * Nadzorna ploča partnera (§6.5). Prikazuje preuzete/pending profile, uređivanje drafta
 * (about/usluge/cijena/stil), osnovnu statistiku i objavu (samo za odobrenog vlasnika).
 */
export default function ProviderDashboard() {
  const { user, loading } = useAuth();
  const router = useRouter();
  const [vendors, setVendors] = useState<ProviderVendor[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (loading) return;
    if (!user) { router.replace("/prijava?next=/partner"); return; }
    providerApi.vendors().then(setVendors).catch((e) => setError(providerMessage(e)));
  }, [user, loading, router]);

  if (loading || (!user && !error)) return <main className="container page"><p className="muted">Učitavanje…</p></main>;

  return (
    <main className="container page">
      <h1>Nadzorna ploča partnera</h1>
      <p className="sub">Uredite podatke svojih profila. Promjene postaju javne nakon provjere (ili odmah, ako ste odobreni vlasnik).</p>

      {error && <p className="auth-error">{error}</p>}

      {vendors && vendors.length === 0 && (
        <div className="empty">
          <h3>Još niste preuzeli nijedan profil</h3>
          <p>Pronađite svoj profil u katalogu i kliknite “Ovo je moj profil — preuzmi ga”.</p>
          <div className="actions">
            <Link href="/" className="btn btn-primary btn-sm">Pronađi svoj profil</Link>
          </div>
        </div>
      )}

      {vendors?.map((v) => <ProviderVendorCard key={v.slug} vendor={v} />)}
    </main>
  );
}

function statusLabel(v: ProviderVendor): { text: string; cls: string } {
  if (v.myStatus === "owner") return { text: "✓ Vaš profil (odobren)", cls: "verified" };
  if (v.myStatus === "rejected") return { text: "Zahtjev odbijen", cls: "over-cap" };
  return { text: "⏳ Čeka odobrenje", cls: "new" };
}

function ProviderVendorCard({ vendor }: { vendor: ProviderVendor }) {
  const cat = CATEGORY_BY_SLUG[vendor.category]?.name ?? vendor.category;
  const [draft, setDraft] = useState<VendorDraft>(vendor.draft);
  const [busy, setBusy] = useState(false);
  const [msg, setMsg] = useState<string | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const status = statusLabel(vendor);

  function setPrice(p: PriceModel) { setDraft((d) => ({ ...d, price: p })); }

  async function save() {
    setBusy(true); setErr(null); setMsg(null);
    try {
      await providerApi.saveDraft(vendor.slug, draft);
      setMsg(vendor.canPublish ? "Spremljeno kao skica." : "Spremljeno. Bit će javno nakon odobrenja.");
    } catch (e) { setErr(providerMessage(e)); } finally { setBusy(false); }
  }

  async function publish() {
    setBusy(true); setErr(null); setMsg(null);
    try {
      await providerApi.saveDraft(vendor.slug, draft);
      await providerApi.publish(vendor.slug);
      setMsg("Objavljeno — promjene su javne.");
    } catch (e) { setErr(providerMessage(e)); } finally { setBusy(false); }
  }

  return (
    <section className="prov-card">
      <header className="prov-head">
        <div>
          <h2 style={{ margin: 0, fontFamily: "var(--font-display)", fontWeight: 550, fontSize: 20 }}>{vendor.name}</h2>
          <p className="muted" style={{ margin: "2px 0 0", fontSize: 13.5 }}>{cat}</p>
        </div>
        <span className={`badge ${status.cls}`}>{status.text}</span>
      </header>

      <div className="prov-stats">
        <div><strong>{vendor.stats.views30}</strong><span>pregleda (30 dana)</span></div>
        <div><strong>{vendor.stats.compares30}</strong><span>dodavanja u usporedbu</span></div>
        <div><strong>{vendor.stats.favorites30}</strong><span>spremanja u favorite</span></div>
      </div>

      <div className="prov-form">
        <label>
          Opis (o sebi)
          <textarea rows={4} value={draft.about ?? ""} maxLength={2000}
            onChange={(e) => setDraft((d) => ({ ...d, about: e.target.value }))} />
        </label>

        <label>
          Usluge (jedna po retku)
          <textarea rows={4} value={draft.services.join("\n")}
            onChange={(e) => setDraft((d) => ({ ...d, services: e.target.value.split("\n") }))} />
        </label>

        <fieldset className="prov-price">
          <legend>Cijena</legend>
          <select value={draft.price.kind} onChange={(e) => {
            const k = e.target.value as PriceModel["kind"];
            if (k === "onRequest") setPrice({ kind: "onRequest" });
            else if (k === "from") setPrice({ kind: "from", from: draft.price.kind === "from" ? draft.price.from : 0 });
            else setPrice({ kind: "perPerson", from: 0, to: 0 });
          }}>
            <option value="from">Od (fiksni paket)</option>
            <option value="perPerson">Po osobi (raspon)</option>
            <option value="onRequest">Na upit</option>
          </select>
          {draft.price.kind === "from" && (
            <input type="number" min={0} placeholder="od €" value={draft.price.from || ""}
              onChange={(e) => setPrice({ kind: "from", from: Number(e.target.value) })} />
          )}
          {draft.price.kind === "perPerson" && (() => {
            const pp = draft.price;
            return (
              <>
                <input type="number" min={0} placeholder="od €/os." value={pp.from || ""}
                  onChange={(e) => setPrice({ kind: "perPerson", from: Number(e.target.value), to: pp.to })} />
                <input type="number" min={0} placeholder="do €/os." value={pp.to || ""}
                  onChange={(e) => setPrice({ kind: "perPerson", from: pp.from, to: Number(e.target.value) })} />
              </>
            );
          })()}
        </fieldset>

        <label>
          Stil / oznake (odvojeno zarezom)
          <input type="text" value={draft.styleTags.join(", ")}
            onChange={(e) => setDraft((d) => ({ ...d, styleTags: e.target.value.split(",").map((s) => s.trim()).filter(Boolean) }))} />
        </label>

        {msg && <p className="fit good" style={{ marginTop: 0 }}>{msg}</p>}
        {err && <p className="auth-error">{err}</p>}

        <div className="actions">
          <button className="btn btn-sm" disabled={busy} onClick={save}>
            {busy ? "…" : "Spremi skicu"}
          </button>
          {vendor.canPublish && (
            <button className="btn btn-primary btn-sm" disabled={busy} onClick={publish}>
              {busy ? "…" : "Spremi i objavi"}
            </button>
          )}
          <Link href={`/pruzatelj/${vendor.slug}`} className="btn btn-sm btn-ghost">Pogledaj profil ↗</Link>
        </div>
      </div>
    </section>
  );
}
