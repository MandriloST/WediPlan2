"use client";

import { useEffect } from "react";
import BudgetCalculator from "./BudgetCalculator";
import { useBudget } from "@/stores";

/** Slide-over drawer — non-intrusive, never blocks the landing (README). */
export default function BudgetDrawer() {
  const { drawerOpen, closeDrawer } = useBudget();

  useEffect(() => {
    if (!drawerOpen) return;
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && closeDrawer();
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [drawerOpen, closeDrawer]);

  if (!drawerOpen) return null;
  return (
    <>
      <div className="drawer-backdrop" onClick={closeDrawer} />
      <aside className="drawer" role="dialog" aria-modal="true" aria-label="Kalkulator budžeta">
        <div className="drawer-head">
          <h2>🧮 Kalkulator budžeta</h2>
          <button className="btn btn-ghost btn-sm" onClick={closeDrawer} aria-label="Zatvori">
            ✕
          </button>
        </div>
        <BudgetCalculator onNavigate={closeDrawer} />
      </aside>
    </>
  );
}
