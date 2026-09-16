"use client";

import Link from "next/link";

/** Granica greške (npr. API nedostupan pri renderu profila). */
export default function Error({ reset }: { error: Error; reset: () => void }) {
  return (
    <main className="container page">
      <div className="empty" style={{ marginTop: 30 }}>
        <h3>Nešto nije u redu</h3>
        <p>Podaci trenutno nisu dostupni. Pokušajte ponovno za trenutak.</p>
        <div className="actions">
          <button className="btn btn-primary btn-sm" onClick={reset}>
            Pokušaj ponovno
          </button>
          <Link className="btn btn-sm" href="/">
            Početna
          </Link>
        </div>
      </div>
    </main>
  );
}
