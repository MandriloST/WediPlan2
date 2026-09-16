import "server-only";
import type { ImportedReview, Vendor, VendorProfileData } from "@/lib/types";
import profilesJson from "@/data/profiles.json";
import { VENDORS } from "./vendors";

/** MOCK GET /api/vendors/{slug}. Iz Excel predloška (scripts/import-vendors.mjs): slug → { about, services, reviews } */
const PROFILES = profilesJson as Record<
  string,
  { about?: string; services?: string[]; reviews?: ImportedReview[] }
>;

const VENDOR_BY_SLUG = Object.fromEntries(VENDORS.map((v) => [v.slug, v])) as Record<string, Vendor>;

/* Ručni demo unosi (mock). Prazan about/services popunjava withProfileDefaults (lib/profile). */
const ABOUT_OVERRIDES: Record<string, string> = {
  "foto-studio-anic":
    "Vjenčanja fotografiramo od 2014. — reportažno, bez namještanja, s naglaskom na svjetlo i emociju. Radimo u cijeloj Dalmaciji, a paket uvijek uključuje dvoje fotografa i online galeriju u roku od 30 dana.",
  "villa-dalmacija":
    "Terasa uz more za do 220 gostiju, vlastita kuhinja i parking. Cijena po osobi uključuje meni od 5 slijedova, piće prva 4 sata i osnovnu dekoraciju stolova.",
  "dvorac-bezanec":
    "Barokni dvorac iz 17. stoljeća s perivojem za ceremonije na otvorenom. Kapacitet 180 gostiju, smještaj za mladence i najužu obitelj uključen u paket.",
  "tamburasi-zlatne-zice":
    "Peteročlani tamburaški sastav — od tradicijskih slavonskih do modernih obrada. Sviramo do zadnjeg gosta, ozvučenje je naše.",
  "atelier-foto-zg":
    "Foto + video tim iz Zagreba. Snimamo diskretno, u filmskom stilu; highlight film od 5 minuta isporučujemo unutar 6 tjedana.",
};

/* Prenesene recenzije (feature #4) — mock za nekoliko pružatelja. */
const IMPORTED_REVIEWS: Record<string, ImportedReview[]> = {
  "foto-studio-anic": [
    { author: "Marija i Ivan", rating: 5, text: "Fotografije su prekrasne, a njih dvoje se cijeli dan gotovo nisu ni primijetili. Galerija stigla ranije od dogovorenog.", source: "Google recenzije", year: 2025 },
    { author: "Petra K.", rating: 5, text: "Profesionalni, brzi i topli ljudi. Preporuka svima u Dalmaciji.", source: "Google recenzije", year: 2024 },
  ],
  "villa-dalmacija": [
    { author: "Ana i Marko", rating: 5, text: "Terasa uz more je spektakularna, hrana odlična, osoblje na razini. Gosti još pričaju o zalasku sunca.", source: "Google recenzije", year: 2025 },
    { author: "L. Perić", rating: 4, text: "Sve pohvale za organizaciju, jedino je parking bio pretijesan za veći broj auta.", source: "Facebook", year: 2024 },
  ],
  "dvorac-bezanec": [
    { author: "Ivana i Tomislav", rating: 5, text: "Vjenčanje iz bajke — ceremonija u perivoju, večera u dvorani s freskama. Vrijedno svakog eura.", source: "Google recenzije", year: 2024 },
  ],
  "atelier-foto-zg": [
    { author: "Dora i Filip", rating: 5, text: "Highlight film nas je rasplakao. Diskretni na dan vjenčanja, a materijali vrhunski.", source: "Google recenzije", year: 2025 },
  ],
  "tamburasi-zlatne-zice": [
    { author: "Svatovi iz Đakova", rating: 5, text: "Digli su cijelu salu na noge i svirali dok je zadnji gost stajao. Majstori.", source: "Facebook", year: 2024 },
  ],
};

/** Oblik identičan .NET-u: about "" i services [] kad nisu uneseni. */
export function getProfile(slug: string): VendorProfileData | null {
  const vendor = VENDOR_BY_SLUG[slug];
  if (!vendor) return null;
  const p = PROFILES[slug];
  return {
    vendor,
    about: p?.about ?? ABOUT_OVERRIDES[slug] ?? "",
    services: p?.services ?? [],
    importedReviews: p?.reviews ?? IMPORTED_REVIEWS[slug] ?? [],
  };
}
