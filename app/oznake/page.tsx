import type { Metadata } from "next";
import { TOP_RATED_MIN_RATING, TOP_RATED_MIN_REVIEWS } from "@/lib/badges";

export const metadata: Metadata = {
  title: "Kako dodjeljujemo oznake — Wediplan",
  description:
    "Javni kriteriji za sve oznake na Wediplanu. Oznake se izvode iz podataka i ne mogu se kupiti.",
};

export default function BadgesPage() {
  return (
    <main className="container page" style={{ maxWidth: 760 }}>
      <h1>Kako dodjeljujemo oznake</h1>
      <p className="sub">
        Svaka oznaka na Wediplanu ima javni kriterij i izvodi se automatski iz podataka. Oznake se
        ne mogu kupiti niti dogovoriti — ni sponzorstvom, ni partnerstvom. Sponzorirani sadržaj je
        uvijek zasebno označen kao „Istaknuto” i ne utječe na oznake, ocjene ni redoslijed
        organskih rezultata.
      </p>

      <h2>Oznake povjerenja</h2>
      <p>Opisuju stanje profila — svaki profil nosi najviše jednu.</p>
      <p>
        <span className="badge verified">✓ Verificirani profil</span> — vlasnik je preuzeo profil i
        sam potvrdio podatke. Najjača razina: ono što piše, napisao je pružatelj.
      </p>
      <p>
        <span className="badge verified">✓ Provjereni podaci</span> — Wediplan je potvrdio kontakt
        (web, e-mail ili telefon) i osnovne podatke pružatelja. Pružatelj još nije preuzeo profil.
      </p>
      <p>
        <span className="badge new">Novi na WediPlanu</span> — nedavno dodan profil čiji se podaci
        još dopunjuju. Manjak recenzija na Wediplanu ne znači da je pružatelj nepouzdan.
      </p>
      <p className="muted">
        Profili bez ovih oznaka sadrže podatke prikupljene iz javnih izvora koje pružatelj još nije
        potvrdio. Vlasnik takav profil može besplatno preuzeti i dopuniti.
      </p>

      <h2>Zarađene oznake</h2>
      <p>
        <span className="badge top">★ Top ocijenjen</span> — ocjena {TOP_RATED_MIN_RATING}+ uz
        najmanje {TOP_RATED_MIN_REVIEWS} recenzija. Izvor ocjene (npr. Google recenzije) uvijek je
        naveden uz oznaku. Dok Wediplan ne skupi vlastite recenzije, koristimo javno dostupne
        ocjene s navedenim izvorom.
      </p>
      <p className="muted">
        U pripremi, uz kalendar i CRM za pružatelje: „Brzo odgovara” (prosječan odgovor unutar 24
        h) i „Popularan” (broj spremanja i upita). I te će oznake imati javne pragove na ovoj
        stranici.
      </p>

      <h2>Što nikad nećemo raditi</h2>
      <p>
        Nema negativnih javnih oznaka. Ako za profil utvrdimo da je zastario, netočan ili da
        pružatelj više ne posluje, profil uklanjamo s platforme — ne označavamo ga javno. Prijave
        netočnih podataka provjeravamo prije bilo kakve odluke.
      </p>
    </main>
  );
}
