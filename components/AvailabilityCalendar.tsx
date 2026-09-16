"use client";

import { useState } from "react";
import { MONTHS_HR, monthGrid } from "@/lib/availability";
import type { Vendor } from "@/lib/types";
import { useWeddingDate } from "@/stores";

export default function AvailabilityCalendar({ vendor }: { vendor: Vendor }) {
  const now = new Date();
  const [ym, setYm] = useState({ y: now.getFullYear(), m: now.getMonth() });
  const { date, setDate } = useWeddingDate();

  if (!vendor.liveCalendar) {
    return (
      <div className="avail-card">
        <h3>Dostupnost</h3>
        <p className="on-request">
          Ovaj pružatelj još ne vodi kalendar uživo — dostupnost provjerite upitom.
        </p>
        <button className="btn btn-sm" disabled title="Uskoro">
          ✉ Pošalji upit (uskoro)
        </button>
        <p className="note">Kalendar uživo stiže s partnerskim CRM-om — bez čekanja odgovora.</p>
      </div>
    );
  }

  const cells = monthGrid(vendor, ym.y, ym.m);
  const prev = () => setYm(({ y, m }) => (m === 0 ? { y: y - 1, m: 11 } : { y, m: m - 1 }));
  const next = () => setYm(({ y, m }) => (m === 11 ? { y: y + 1, m: 0 } : { y, m: m + 1 }));
  const notPast = ym.y > now.getFullYear() || (ym.y === now.getFullYear() && ym.m >= now.getMonth());

  return (
    <div className="avail-card">
      <h3>
        Dostupnost <span className="badge live">✓ kalendar uživo</span>
      </h3>
      <div className="cal-head">
        <button className="btn btn-ghost btn-sm" onClick={prev} disabled={!notPast} aria-label="Prethodni mjesec">
          ‹
        </button>
        <strong>
          {MONTHS_HR[ym.m]} {ym.y}.
        </strong>
        <button className="btn btn-ghost btn-sm" onClick={next} aria-label="Sljedeći mjesec">
          ›
        </button>
      </div>
      <div className="cal-grid" role="grid" aria-label="Kalendar dostupnosti">
        {["Po", "Ut", "Sr", "Če", "Pe", "Su", "Ne"].map((d) => (
          <span key={d} className="dow">
            {d}
          </span>
        ))}
        {cells.map((c, i) =>
          c === null ? (
            <span key={`e${i}`} />
          ) : (
            <button
              key={c.iso}
              className={`day ${c.status}${c.past ? " past" : ""}${date === c.iso ? " picked" : ""}`}
              disabled={c.past || c.status === "busy"}
              title={c.status === "busy" ? "Zauzeto" : "Slobodno — postavi kao datum vjenčanja"}
              onClick={() => setDate(c.iso)}
            >
              {c.dayOfMonth}
            </button>
          )
        )}
      </div>
      <p className="legend">
        <span className="dot free" /> slobodno · <span className="dot busy" /> zauzeto — klik na slobodan dan
        postavlja vaš datum
      </p>
    </div>
  );
}
