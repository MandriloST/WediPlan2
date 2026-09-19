import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Pravila privatnosti — Wediplan",
  description: "Kako Wediplan prikuplja, koristi i štiti osobne podatke.",
};

const UPDATED = "17. rujna 2026.";

export default function PrivacyPage() {
  return (
    <main className="legal-page">
      <p className="legal-note">
        ⚠️ Predložak — prije objave popuniti podatke tvrtke i dati na pravnu provjeru.
      </p>
      <h1>Pravila privatnosti</h1>
      <p className="muted">Zadnja izmjena: {UPDATED}</p>

      <h2>1. Voditelj obrade</h2>
      <p>
        Voditelj obrade osobnih podataka je [NAZIV TVRTKE/OBRTA], [ADRESA], OIB: [OIB],
        e-pošta: [EMAIL]. Za sva pitanja o zaštiti podataka javite se na navedenu e-poštu.
      </p>

      <h2>2. Koje podatke prikupljamo</h2>
      <p>
        Podatke o korisnicima (parovima) koje sami unesu pri registraciji: e-pošta, ime za prikaz,
        te sadržaj koji stvore (favoriti, plan budžeta, recenzije). Podatke o pružateljima usluga
        prikupljamo iz javno dostupnih izvora (mrežne stranice, javni imenici) te podatke koje
        pružatelji sami dostave nakon preuzimanja profila. Tehničke podatke (agregirana statistika
        posjeta) prikupljamo bez identifikacije pojedinca.
      </p>

      <h2>3. Svrha i pravna osnova</h2>
      <p>
        Podatke obrađujemo radi pružanja usluge pretraživanja i usporedbe pružatelja, upravljanja
        korisničkim računima te poboljšanja stranice. Pravne osnove su izvršenje ugovora (korisnički
        račun), legitimni interes (prikaz javno dostupnih poslovnih podataka pružatelja, sigurnost
        stranice) i privola (gdje je primjenjivo).
      </p>

      <h2>4. Pružatelji usluga i pravo na uklanjanje (opt-out)</h2>
      <p>
        Prikazujemo samo poslovne podatke koje su pružatelji sami javno objavili. Ne objavljujemo
        privatne mobitele fizičkih osoba bez njihove prijave. Svaki pružatelj može zatražiti
        uklanjanje svog profila putem poveznice na vlastitom profilu; profil se tada odmah skida iz
        javnog prikaza.
      </p>

      <h2>5. Kolačići</h2>
      <p>
        Koristimo isključivo nužne kolačiće (prijava, sigurnost) i vlastitu agregiranu analitiku bez
        marketinških kolačića i bez profiliranja. Detalji su u obavijesti o kolačićima.
      </p>

      <h2>6. Primatelji i obrađivači</h2>
      <p>
        Podatke dijelimo samo s obrađivačima nužnima za rad stranice (hosting, pohrana slika, slanje
        e-pošte), koji obrađuju podatke po našim uputama. Popis i lokacije obrade dostupni su na
        zahtjev.
      </p>

      <h2>7. Razdoblje čuvanja</h2>
      <p>
        Podatke čuvamo dok traje korisnički račun odnosno dok je to nužno za navedene svrhe. Nakon
        brisanja računa podatke uklanjamo ili anonimiziramo u razumnom roku.
      </p>

      <h2>8. Vaša prava</h2>
      <p>
        Imate pravo na pristup, ispravak, brisanje, ograničenje i prigovor na obradu te prenosivost
        podataka. Zahtjev šaljete na [EMAIL]. Pritužbu možete uputiti Agenciji za zaštitu osobnih
        podataka (AZOP).
      </p>

      <h2>9. Izmjene</h2>
      <p>Ova pravila možemo ažurirati; nova verzija objavljuje se na ovoj stranici s datumom izmjene.</p>
    </main>
  );
}
