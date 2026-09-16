import type { Metadata } from "next";
import BudgetCalculator from "@/components/BudgetCalculator";

export const metadata: Metadata = { title: "Kalkulator budžeta — Wediplan" };

export default function BudgetPage() {
  return (
    <main className="container page" style={{ maxWidth: 560 }}>
      <h1>🧮 Budžet</h1>
      <p className="sub">Realna slika troškova odmah — raspodjela po regiji i pružatelji koji stanu u budžet.</p>
      <BudgetCalculator />
    </main>
  );
}
