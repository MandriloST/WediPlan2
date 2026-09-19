import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Uvjeti korištenja — Wediplan",
  description: "Uvjeti korištenja platforme Wediplan.",
};

const UPDATED = "17. rujna 2026.";

export default function TermsPage() {
  return (
    <main className="legal-page">
      <p className="legal-note">
        ⚠️ Predložak — prije objave popuniti podatke tvrtke i dati na pravnu provjeru.
      </p>
      <h1>Uvjeti korištenja</h1>
      <p className="muted">Zadnja izmjena: {UPDATED}</p>

      <h2>1. Pružatelj usluge</h2>
      <p>
        Platformu Wediplan pruža [NAZIV TVRTKE/OBRTA], OIB: [OIB], [ADRESA] („mi"). Korištenjem
        stranice prihvaćate ove uvjete.
      </p>

      <h2>2. Opis usluge</h2>
      <p>
        Wediplan je imenik i alat za usporedbu pružatelja usluga za vjenčanja u Hrvatskoj. Podaci su
        informativni; ugovorni odnos i cijene dogovaraju se izravno s pružateljem. Ne jamčimo
        točnost, dostupnost ni kvalitetu usluga pružatelja.
      </p>

      <h2>3. Korisnički računi</h2>
      <p>
        Za dio funkcija potreban je račun. Odgovorni ste za točnost podataka i čuvanje pristupnih
        podataka. Zabranjeno je lažno predstavljanje i zloupotreba tuđih podataka.
      </p>

      <h2>4. Sadržaj korisnika i recenzije</h2>
      <p>
        Recenzije moraju biti istinite i temeljene na stvarnom iskustvu. Zadržavamo pravo moderacije
        i uklanjanja sadržaja koji je uvredljiv, lažan ili protivan zakonu.
      </p>

      <h2>5. Zabrana automatiziranog prikupljanja podataka (anti-scraping)</h2>
      <p>
        Zabranjeno je automatizirano prikupljanje, kopiranje, indeksiranje ili preuzimanje sadržaja
        stranice (uključujući podatke o pružateljima, cijene i slike) bez našeg izričitog pisanog
        odobrenja. To uključuje robote, „scrapere", skripte i slične alate te zaobilaženje tehničkih
        mjera zaštite i ograničenja učestalosti pristupa. Sadržaj se smije koristiti isključivo za
        osobno, nekomercijalno pregledavanje unutar stranice.
      </p>

      <h2>6. Intelektualno vlasništvo</h2>
      <p>
        Struktura, dizajn i agregirani sadržaj stranice zaštićeni su. Slike i logotipi pružatelja
        vlasništvo su odgovarajućih nositelja prava.
      </p>

      <h2>7. Ograničenje odgovornosti</h2>
      <p>
        Uslugu pružamo „kakva jest". U mjeri dopuštenoj zakonom ne odgovaramo za štetu proizašlu iz
        korištenja stranice niti iz odnosa korisnika i pružatelja.
      </p>

      <h2>8. Izmjene i mjerodavno pravo</h2>
      <p>
        Uvjete možemo mijenjati uz objavu na ovoj stranici. Primjenjuje se pravo Republike Hrvatske,
        a za sporove je nadležan stvarno nadležni sud.
      </p>
    </main>
  );
}
