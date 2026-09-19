"use client";

import { useState } from "react";
import { optOutApi } from "@/lib/api/provider";

/**
 * GDPR opt-out (§9, faza 6). Diskretan link na NECLAIMANIM profilima: pružatelj koji je uvezen
 * bez svoje prijave može zatražiti skidanje profila. Nakon slanja profil se odmah skida iz
 * javnog prikaza (reverzibilno preko admina).
 */
export default function OptOutLink({ slug, claimStatus }: { slug: string; claimStatus?: string }) {
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState("");
  const [contact, setContact] = useState("");
  const [state, setState] = useState<"idle" | "sending" | "done" | "error">("idle");

  // Na preuzetim (claimed) profilima opt-out nema smisla — vlasnik upravlja profilom sam.
  if (claimStatus === "claimed") return null;

  async function submit() {
    setState("sending");
    try {
      await optOutApi.submit(slug, reason.trim(), contact.trim());
      setState("done");
    } catch {
      setState("error");
    }
  }

  if (state === "done") {
    return (
      <div className="optout-box">
        <p className="optout-done">
          Zahtjev je zaprimljen i profil je skinut iz javnog prikaza. Ako je ovo pogreška,
          javite nam se putem kontakta u podnožju.
        </p>
      </div>
    );
  }

  return (
    <div className="optout-box">
      {!open ? (
        <button type="button" className="optout-link" onClick={() => setOpen(true)}>
          Ovo je moj obrt/firma i ne želim biti na stranici
        </button>
      ) : (
        <div className="optout-form">
          <p className="optout-intro">
            Zatražite skidanje ovog profila iz javnog prikaza (GDPR). Razlog i kontakt su neobavezni
            i pomažu nam u provjeri.
          </p>
          <textarea
            placeholder="Razlog (neobavezno)"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            rows={2}
          />
          <input
            type="text"
            placeholder="Kontakt za povrat informacije (neobavezno)"
            value={contact}
            onChange={(e) => setContact(e.target.value)}
          />
          {state === "error" && <p className="auth-error">Slanje nije uspjelo. Pokušajte ponovno.</p>}
          <div className="optout-actions">
            <button type="button" className="btn btn-sm" disabled={state === "sending"} onClick={submit}>
              {state === "sending" ? "Šaljem…" : "Skini moj profil"}
            </button>
            <button type="button" className="btn btn-sm btn-ghost" onClick={() => setOpen(false)}>
              Odustani
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
