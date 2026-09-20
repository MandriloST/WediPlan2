import { IMAGE_BASE } from "./images";
import { CATEGORIES } from "./data";

/**
 * Sadržaj naslovnice (redizajn 3a). Samo prikaz — slugovi su ugovor (lib/data.ts).
 *
 * HERO SLIKA (jedino mjesto): HERO_IMAGE ispod.
 * HERO_IMAGE: dok nema prave fotografije (uz dozvolu autora), hero je jednobojna
 * jadranska pozadina. Kad fotografija stigne: spremi je kao
 * public/images/hero/naslovnica.jpg (≥ 2000 px širine, motiv lijevo/desno od sredine
 * jer je tekst centriran) i postavi HERO_IMAGE = `${IMAGE_BASE}/hero/naslovnica.jpg`.
 */
// export const HERO_IMAGE: string | null = null;
export const HERO_IMAGE = `${IMAGE_BASE}/hero/naslovnica.jpg`;
export const HERO_IMAGE_EXAMPLE = `${IMAGE_BASE}/hero/naslovnica.jpg`;

/** 6 foto-pločica "Istražite kategorije" (redoslijed = prikaz). label = kratki naziv na pločici. */
export const LANDING_CATEGORIES: { slug: string; label: string }[] = [
  { slug: "restorani-i-sale", label: "Dvorane i restorani" },
  { slug: "foto-i-video", label: "Foto i video" },
  { slug: "glazba-bendovi", label: "Bendovi" },
  { slug: "cvijece-i-dekoracije", label: "Cvijeće i dekoracije" },
  { slug: "vjencanice", label: "Vjenčanice" },
  { slug: "catering", label: "Catering" },
];

/**
 * "Najbolje ocijenjeni": prvi po rangu (/api/vendors je rangiran po ocjeni) iz svake
 * od ovih kategorija. Organski odabir — kad dođe plaćeni "Izlog na naslovnici" (§M.2),
 * mijenja se samo izvor podataka, ne komponenta, i naslov postaje "Izdvojeno".
 */
export const TOP_RATED_CATEGORIES = ["restorani-i-sale", "foto-i-video", "glazba-bendovi"];

/**
 * SVI TEKSTOVI NASLOVNICE — mijenjaj ovdje, komponenta (components/LandingShell.tsx) ih samo prikazuje.
 */
export const LANDING_TEXT = {
  heroTitle: "Pronađite sve za vaše vjenčanje",
  heroLead: `3000+ pružatelja usluga! Od dvorane do fotografa.`,
  // perkPrice: "Cijena uvijek vidljiva",
  perkCompare: "Usporedite do 4 pružatelja",
  perkBudget: "Izračunajte budžet",
  categoriesTitle: "Istražite kategorije",
  categoriesMore: `Sve kategorije (${CATEGORIES.length})`,
  topTitle: "Najbolje ocijenjeni",
  topNote: "prvi po ocjeni među dvoranama, fotografima i bendovima",
  mapTitle: "Istražite Hrvatsku",
  mapLead:
    "Kliknite regiju na karti ili na popisu — vidjet ćete sve kategorije i koliko pružatelja radi u toj regiji.",
  mapAll: "Cijela Hrvatska",
};
