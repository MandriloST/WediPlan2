"use client";

import { useEffect, useState } from "react";
import Link from "next/link";

const KEY = "wediplan.cookie-notice.v1";

/**
 * Minimalna obavijest o kolačićima (§9). Koristimo samo nužne kolačiće i agregiranu analitiku bez
 * marketinških kolačića, pa nije potreban sustav privola — dovoljna je informativna obavijest.
 */
export default function CookieNotice() {
  const [show, setShow] = useState(false);

  useEffect(() => {
    try { if (!localStorage.getItem(KEY)) setShow(true); } catch { /* localStorage nedostupan */ }
  }, []);

  function dismiss() {
    try { localStorage.setItem(KEY, "1"); } catch { /* ignore */ }
    setShow(false);
  }

  if (!show) return null;
  return (
    <div className="cookie-notice" role="dialog" aria-label="Obavijest o kolačićima">
      <p>
        Koristimo samo nužne kolačiće i anonimnu statistiku posjeta — bez marketinških kolačića.
        Više u <Link href="/pravila-privatnosti">Pravilima privatnosti</Link>.
      </p>
      <button type="button" className="btn btn-sm" onClick={dismiss}>U redu</button>
    </div>
  );
}
