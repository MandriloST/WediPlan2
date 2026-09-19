import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Impressum — Wediplan",
  description: "Podaci o pružatelju usluge Wediplan.",
};

export default function ImpressumPage() {
  return (
    <main className="legal-page">
      <p className="legal-note">
        ⚠️ Predložak — popuniti stvarne podatke tvrtke/obrta prije objave.
      </p>
      <h1>Impressum</h1>

      <h2>Pružatelj usluge</h2>
      <p>
        [NAZIV TVRTKE/OBRTA]
        <br />
        [ADRESA, POŠTANSKI BROJ, GRAD]
        <br />
        OIB: [OIB]
        <br />
        E-pošta: [EMAIL]
        <br />
        Telefon: [TELEFON]
      </p>

      <h2>Registracija</h2>
      <p>
        [Trgovački sud / nadležni registar i MBS, ili podaci o obrtu i obrtnom registru].
        Temeljni kapital i članovi uprave (za d.o.o.): [PODACI].
      </p>

      <h2>Odgovorna osoba</h2>
      <p>[IME I PREZIME].</p>

      <p className="muted" style={{ marginTop: 24 }}>
        Za pitanja o zaštiti podataka vidjeti <a href="/pravila-privatnosti">Pravila privatnosti</a>,
        a o korištenju stranice <a href="/uvjeti-koristenja">Uvjete korištenja</a>.
      </p>
    </main>
  );
}
