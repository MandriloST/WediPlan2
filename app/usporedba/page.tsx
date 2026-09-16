"use client";

import Image from "next/image";
import Link from "next/link";
import { useEffect, useRef } from "react";
import { useQuery } from "@tanstack/react-query";
import { track } from "@/lib/analytics";
import { CATEGORY_BY_SLUG } from "@/lib/data";
import { api } from "@/lib/api/client";
import { formatPrice, formatRating, isOnRequest } from "@/lib/format";
import { dayStatus, statusLabel } from "@/lib/availability";
import type { Vendor } from "@/lib/types";
import { useCompare, useWeddingDate } from "@/stores";
import { coverImage } from "@/lib/images";

/** Ista logika kao kalendar na profilu (lib/availability) — konzistentno svugdje. */
function availability(vendor: Vendor, date: string | null) {
  if (!vendor.liveCalendar) return { label: "na upit — kontaktirajte pružatelja", free: false };
  if (!date) return { label: "✓ kalendar uživo — odaberite datum", free: false };
  return statusLabel(dayStatus(vendor, date));
}

export default function ComparePage() {
  const { ids, remove, clear } = useCompare();
  const { date, setDate } = useWeddingDate();
  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ["vendors-by-ids", ids],
    queryFn: ({ signal }) => api.vendorsByIds(ids, signal),
    enabled: ids.length > 0,
    placeholderData: (prev) => prev, // uklanjanje stupca bez treptanja
  });
  // redoslijed = redoslijed odabira; bez podataka za ID koji je upravo uklonjen
  const vendors = ids
    .map((id) => data?.find((v) => v.id === id))
    .filter(Boolean) as Vendor[];

  const viewedKey = vendors.length >= 2 ? vendors.map((v) => v.slug).sort().join(",") : "";
  const tracked = useRef("");
  useEffect(() => {
    if (!viewedKey || tracked.current === viewedKey) return;
    tracked.current = viewedKey;
    track("compare_viewed", { slugs: viewedKey.split(","), category: vendors[0]?.category });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [viewedKey]);

  if (ids.length > 0 && isLoading)
    return (
      <main className="container page">
        <h1>Usporedba ⇄</h1>
        <div className="skel" style={{ height: 320 }} aria-busy="true" />
      </main>
    );
  if (ids.length > 0 && isError)
    return (
      <main className="container page">
        <h1>Usporedba ⇄</h1>
        <div className="empty">
          <h3>Podaci trenutno nisu dostupni</h3>
          <p>Vaš odabir je sačuvan na ovom uređaju. Pokušajte ponovno za trenutak.</p>
          <div className="actions">
            <button className="btn btn-primary btn-sm" onClick={() => refetch()}>
              Pokušaj ponovno
            </button>
          </div>
        </div>
      </main>
    );

  return (
    <main className="container page">
      <h1>Usporedba ⇄</h1>

      {vendors.length < 2 ? (
        <div className="empty">
          <h3>{vendors.length === 0 ? "Još ništa za usporedbu" : "Dodajte barem još jednog"}</h3>
          <p>Označite „usporedi” na 2–4 pružatelja i vratite se ovdje.</p>
          <div className="actions">
            <Link href="/" className="btn btn-primary btn-sm">
              Istraži pružatelje
            </Link>
          </div>
        </div>
      ) : (
        <>
          <p className="sub">
            Datum vjenčanja:{" "}
            <input
              type="date"
              value={date ?? ""}
              onChange={(e) => setDate(e.target.value || null)}
              aria-label="Datum vjenčanja"
              style={{
                border: "1px solid var(--border-strong)",
                borderRadius: 8,
                padding: "6px 10px",
                minHeight: 40,
              }}
            />
          </p>
          <div className="compare-scroll">
            <table className="compare-table">
              <thead>
                <tr>
                  <th scope="col">
                    <span className="sr-only" />
                  </th>
                  {vendors.map((v) => (
                    <th key={v.id} scope="col" className="col-head">
                      <div className="cmp-img">
                        <Image src={coverImage(v).src} alt={v.name} fill sizes="160px" style={{ objectFit: "cover" }} />
                      </div>
                      <Link href={`/pruzatelj/${v.slug}`}>{v.name}</Link>
                      <div style={{ fontWeight: 400, color: "var(--muted)", fontSize: 13 }}>
                        {v.city || "—"}
                      </div>
                      <button className="remove" onClick={() => remove(v.id)}>
                        ukloni ×
                      </button>
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                <tr className="price-row">
                  <td>Cijena</td>
                  {vendors.map((v) => (
                    <td key={v.id}>
                      <span className={isOnRequest(v.price) ? "price-upit" : "price"}>{formatPrice(v.price)}</span>
                    </td>
                  ))}
                </tr>
                <tr>
                  <td>Recenzije</td>
                  {vendors.map((v) => (
                    <td key={v.id}>
                      {v.reviewCount > 0 ? <>★ {formatRating(v.rating)} · {v.reviewCount}</> : <span style={{ color: "var(--muted)" }}>još bez ocjene</span>}
                      {v.verified && (
                        <div>
                          <span className="badge verified">✓ provjereno</span>
                        </div>
                      )}
                    </td>
                  ))}
                </tr>
                <tr>
                  <td>{date ? new Date(date).toLocaleDateString("hr-HR") : "Dostupnost"}</td>
                  {vendors.map((v) => {
                    const a = availability(v, date);
                    return (
                      <td key={v.id} className={a.free ? "free" : ""}>
                        {a.label}
                      </td>
                    );
                  })}
                </tr>
                <tr>
                  <td>Stil</td>
                  {vendors.map((v) => (
                    <td key={v.id}>
                      {v.styleTags.length ? v.styleTags.join(" · ") : "—"}
                    </td>
                  ))}
                </tr>
                <tr>
                  <td>Kategorija</td>
                  {vendors.map((v) => (
                    <td key={v.id}>{CATEGORY_BY_SLUG[v.category]?.name}</td>
                  ))}
                </tr>
              </tbody>
            </table>
          </div>
          <p style={{ marginTop: 14 }}>
            <button className="btn btn-ghost btn-sm" onClick={clear}>
              Isprazni usporedbu
            </button>
          </p>
        </>
      )}
    </main>
  );
}
