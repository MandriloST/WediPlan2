"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useState } from "react";
import { reviewApi, providerMessage } from "@/lib/api/provider";
import { useAuth } from "@/stores/auth";

/**
 * Forma za korisničku recenziju (§6). Samo za prijavljene; recenzija ide u moderaciju.
 * Gost vidi poziv na prijavu (s povratkom na profil).
 */
export default function ReviewForm({ vendorSlug }: { vendorSlug: string }) {
  const { user, loading } = useAuth();
  const pathname = usePathname();
  const [rating, setRating] = useState(0);
  const [hover, setHover] = useState(0);
  const [text, setText] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState(false);

  if (loading) return <div className="empty" aria-busy style={{ marginTop: 14 }} />;

  if (!user) {
    const next = pathname ? `?next=${encodeURIComponent(pathname)}` : "";
    return (
      <div className="empty" style={{ marginTop: 14 }}>
        <h3>Podijelite svoje iskustvo</h3>
        <p>Recenziju mogu ostaviti prijavljeni korisnici. Provjeravamo recenzije prije objave.</p>
        <div className="actions">
          <Link href={`/prijava${next}`} className="btn btn-primary btn-sm">
            Prijava za pisanje recenzije
          </Link>
        </div>
      </div>
    );
  }

  if (done) {
    return (
      <div className="fit good" style={{ marginTop: 14 }}>
        ✓ Hvala! Recenzija je zaprimljena i čeka provjeru prije objave.
      </div>
    );
  }

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (rating < 1) { setError("Odaberite ocjenu (1–5 zvjezdica)."); return; }
    if (text.trim().length < 10) { setError("Napišite barem nekoliko riječi (min. 10 znakova)."); return; }
    setBusy(true); setError(null);
    try {
      await reviewApi.create(vendorSlug, rating, text.trim());
      setDone(true);
    } catch (err) {
      setError(providerMessage(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <form className="review-form" onSubmit={submit}>
      <h3>Napišite recenziju</h3>
      <div className="star-input" role="radiogroup" aria-label="Ocjena">
        {[1, 2, 3, 4, 5].map((n) => (
          <button
            type="button"
            key={n}
            className={`star-btn${(hover || rating) >= n ? " on" : ""}`}
            aria-label={`${n} ${n === 1 ? "zvjezdica" : "zvjezdice"}`}
            aria-pressed={rating === n}
            onMouseEnter={() => setHover(n)}
            onMouseLeave={() => setHover(0)}
            onClick={() => setRating(n)}
          >
            ★
          </button>
        ))}
      </div>
      <textarea
        className="review-text"
        placeholder="Kakvo je bilo vaše iskustvo? (usluga, komunikacija, vrijednost za novac…)"
        value={text}
        maxLength={4000}
        rows={4}
        onChange={(e) => setText(e.target.value)}
      />
      {error && <p className="auth-error" role="alert">{error}</p>}
      <button className="btn btn-primary btn-sm" disabled={busy} type="submit">
        {busy ? "Šaljem…" : "Pošalji recenziju"}
      </button>
    </form>
  );
}
