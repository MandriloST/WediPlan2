import type { Metadata } from "next";
import Link from "next/link";

export const metadata: Metadata = {
  title: "Inspiracija — Wediplan",
  robots: { index: false, follow: true },
};

/** Rezervirano mjesto: sekcija "Inspiracija za vaš dan" dolazi nakon MVP-a (redizajn 3a). */
export default function InspirationPage() {
  return (
    <main className="container page soon">
      <h1>Inspiracija</h1>
      <p className="sub">
        Uskoro: lokacije, savjeti i stvarni troškovi vjenčanja po regijama. Dok pripremamo članke,
        pogledajte pružatelje po kategorijama.
      </p>
      <Link href="/kategorije" className="btn btn-primary">
        Pregledaj kategorije
      </Link>
    </main>
  );
}
