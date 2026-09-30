import type { Vendor } from "./types";

/**
 * Konvencija slika:
 *   {BASE}/vendors/<slug>/01.jpg, 02.jpg, 03.jpg   ← stvarne slike (max 3, uz dozvolu!)
 *   {BASE}/defaults/<kategorija-slug>.jpg          ← default PRUŽATELJA na KARTICI (rezultati, karta,
 *                                                      usporedba) kad nema slika (ili defaults/pruzatelj.jpg
 *                                                      za sve — VENDOR_DEFAULT_MODE)
 *   {BASE}/defaults-profile/<kategorija-slug>.jpg  ← default PRUŽATELJA na PROFILU (/pruzatelj/[slug])
 *                                                      kad nema slika — vlastite datoteke, odvojene od
 *                                                      kartice (npr. širi kadar prikladan za veliki prikaz)
 *   {BASE}/categories/<kategorija-slug>.jpg        ← slika KATEGORIJE (pločice naslovnica + /kategorije)
 *
 * BASE je danas /images (public/ u repou). Selidba na Bunny CDN = postaviti
 * NEXT_PUBLIC_IMAGE_BASE=https://wediplan.b-cdn.net s istom strukturom foldera
 * (next.config.mjs automatski dodaje remotePattern) — kod se ne mijenja.
 *
 * vendors.json polje `photos` (npr. ["01.jpg","02.jpg"]) puni import skripta
 * skeniranjem public/images/vendors/<slug>/ — vidi scripts/import-vendors.mjs
 * i `npm run sync:images`.
 */
/** Ekstenzija default slika — sve default slike moraju biti u ovom formatu. */
export const DEFAULT_EXT = ".jpg";

export const IMAGE_BASE = process.env.NEXT_PUBLIC_IMAGE_BASE ?? "/images";

/* ------------------------------------------------------------------ */
/* Slike KATEGORIJA — pločice na naslovnici i /kategorije.             */
/* Odvojene od default slika pružatelja: public/images/categories/     */
/* ------------------------------------------------------------------ */
export function categoryImage(slug: string): string {
  return `${IMAGE_BASE}/categories/${slug}${DEFAULT_EXT}`;
}

/* ------------------------------------------------------------------ */
/* Default slika PRUŽATELJA (kad nema vlastitih fotografija).          */
/*  "per-category" → defaults/<kategorija>.jpg i defaults-profile/<kategorija>.jpg (kao dosad) */
/*  "single"       → jedna defaults/pruzatelj.jpg i jedna defaults-profile/pruzatelj.jpg za sve */
/*                                                                       */
/* Dva odvojena "sloja" — kartica (rezultati/karta/usporedba) i profil  */
/* (/pruzatelj/[slug]) — mogu imati različitu sliku za istu kategoriju: */
/* npr. uži kadar za malu karticu, širi/atmosferičniji za veliki profil.*/
/* Isti VENDOR_DEFAULT_MODE vrijedi za oba sloja; datoteke su zasebne.  */
/* ------------------------------------------------------------------ */
export const VENDOR_DEFAULT_MODE: "per-category" | "single" = "per-category";
export const VENDOR_DEFAULT_SINGLE = "pruzatelj";

export type ImageContext = "card" | "profile";

const DEFAULT_DIR: Record<ImageContext, string> = {
  card: "defaults",
  profile: "defaults-profile",
};

export function vendorDefaultImage(category: string, context: ImageContext = "card"): string {
  const file = VENDOR_DEFAULT_MODE === "single" ? VENDOR_DEFAULT_SINGLE : category;
  return `${IMAGE_BASE}/${DEFAULT_DIR[context]}/${file}${DEFAULT_EXT}`;
}

export interface VendorImage {
  src: string;
  isDefault: boolean;
}

export type Imaged = Pick<Vendor, "slug" | "category" | "photos">;

/**
 * Sve slike pružatelja. context bira KOJI default se koristi kad pružatelj nema vlastitih
 * fotografija: "card" (rezultati/karta/usporedba, zadano) ili "profile" (/pruzatelj/[slug]).
 * Kad pružatelj ima svoje fotografije, context nema utjecaja — iste slike posvuda.
 */
export function vendorImages(vendor: Imaged, context: ImageContext = "card"): VendorImage[] {
  if (vendor.photos && vendor.photos.length > 0) {
    return vendor.photos.map((file) => ({
      // Uploadane slike (Faza 5) dolaze kao apsolutan URL (http(s)://… ili /uploads/…) → koristi izravno.
      // Seed slike iz importa su gola imena datoteka → primijeni konvenciju {BASE}/vendors/<slug>/<file>.
      src: isAbsolute(file) ? file : `${IMAGE_BASE}/vendors/${vendor.slug}/${file}`,
      isDefault: false,
    }));
  }
  return [{ src: vendorDefaultImage(vendor.category, context), isDefault: true }];
}

function isAbsolute(src: string): boolean {
  return /^https?:\/\//i.test(src) || src.startsWith("/uploads/");
}

/** Sličica za karticu (rezultati, karta, usporedba) — uvijek context "card". */
export function coverImage(vendor: Imaged): VendorImage {
  return vendorImages(vendor, "card")[0];
}
