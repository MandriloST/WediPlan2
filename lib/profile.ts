import { CATEGORY_BY_SLUG, REGION_BY_ID } from "./data";
import type { Vendor, VendorProfileData } from "./types";

/* ------------------------------------------------------------------ */
/* "Što pružatelj kaže o sebi" — zadani tekstovi dok pružatelj ne     */
/* ispuni vlastite (claim, Faza 4). Primjenjuju se na PODATKE S API-ja */
/* kad su about/services prazni — UI copy, zato žive u frontendu.     */
/* ------------------------------------------------------------------ */

const ABOUT_BY_GROUP: Record<string, string> = {
  sala: "Prostor za svadbene proslave s vlastitom kuhinjom i osobljem. Cijena po osobi ovisi o odabranom meniju i trajanju — točnu ponudu šaljemo na temelju broja gostiju i termina.",
  catering: "Catering za vjenčanja s posluživanjem na lokaciji po izboru. Meni slažemo zajedno s mladencima nakon degustacije.",
  foto: "Profesionalno bilježimo dan vjenčanja od priprema do zadnjeg plesa. Paketi se razlikuju po broju sati i isporučenim materijalima.",
  glazba: "Glazbeni program prilagođavamo željama mladenaca — od ceremonije do kasnih sati. Repertoar i ozvučenje dogovaramo unaprijed.",
  ostalo: "Usluga za vjenčanja s višegodišnjim iskustvom. Točnu ponudu rado šaljemo na temelju termina i želja.",
};

const SERVICES_BY_GROUP: Record<string, string[]> = {
  sala: ["Meni po osobi", "Osoblje i posluživanje", "Osnovna dekoracija", "Parking za goste"],
  catering: ["Degustacija menija", "Posluživanje na lokaciji", "Najam posuđa", "Slatki stol"],
  foto: ["Cjelodnevno praćenje", "Online galerija", "Ekspresna objava (48 h)", "Ispis / album po dogovoru"],
  glazba: ["Vlastito ozvučenje", "Repertoar po želji", "Glazba za ceremoniju", "Sviranje do kraja proslave"],
  ostalo: ["Termin po dogovoru", "Prilagodba željama", "Dolazak na lokaciju"],
};

/** Popuni prazne about/services zadanim tekstom grupe (API vraća "" / []). */
export function withProfileDefaults(data: VendorProfileData): VendorProfileData {
  const group = CATEGORY_BY_SLUG[data.vendor.category]?.group ?? "ostalo";
  return {
    ...data,
    about: data.about?.trim() ? data.about : ABOUT_BY_GROUP[group],
    services: data.services?.length ? data.services : SERVICES_BY_GROUP[group],
    importedReviews: data.importedReviews ?? [],
  };
}

type Crumbable = Pick<Vendor, "region" | "category">;

export function breadcrumb(vendor: Crumbable) {
  const region = REGION_BY_ID[vendor.region as keyof typeof REGION_BY_ID]; // undefined za inozemne (region="")
  const cat = CATEGORY_BY_SLUG[vendor.category];
  if (!region)
    return [
      { label: "Istraži", href: "/" },
      { label: cat.name, href: `/${cat.slug}` },
    ];
  return [
    { label: "Istraži", href: "/" },
    { label: region.name, href: `/${region.id}` },
    { label: cat.name, href: `/${region.id}/${cat.slug}` },
  ];
}
