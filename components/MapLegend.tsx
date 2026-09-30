"use client";

import { useState } from "react";

/**
 * Legenda karte (Zadatak 11): što znače zeleni i zadani pinovi. Pinovi su isti stilovi kao na karti
 * (.pin / .pin.exact / .pin.upit u globals.css), samo statični (aria-hidden — značenje čita tekst).
 * Desktop: uvijek otvorena. Mobitel (≤ 640px): sklopiva iza gumba "Legenda" (CSS skriva tijelo dok
 * nije .open), da ne zauzima malu kartu. Stanje je samo UI — nema hydration razlike jer je default zatvoreno.
 */
export default function MapLegend() {
  const [open, setOpen] = useState(false);
  return (
    <div className={`map-legend${open ? " open" : ""}`} role="group" aria-label="Legenda karte">
      <button
        type="button"
        className="map-legend-toggle"
        aria-expanded={open}
        onClick={() => setOpen((o) => !o)}
      >
        Legenda
      </button>
      <ul className="map-legend-body">
        <li>
          <span className="pin exact legend-pin" aria-hidden="true">
            <span className="pin-dot" />
            od 850 €
          </span>
          točna lokacija
        </li>
        <li>
          <span className="pin legend-pin" aria-hidden="true">
            od 850 €
          </span>
          približno — centar grada
        </li>
        <li>
          <span className="pin upit legend-pin" aria-hidden="true">
            na upit
          </span>
          cijena na upit
        </li>
      </ul>
    </div>
  );
}
