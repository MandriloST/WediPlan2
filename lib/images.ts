import type { Vendor } from "./types";

/**
 * Konvencija slika:
 *   {BASE}/vendors/<slug>/01.jpg, 02.jpg, 03.jpg   ← stvarne slike (max 3, uz dozvolu!)
 *   {BASE}/defaults/<kategorija-slug>.jpg          ← default PRUŽATELJA kad nema slika
 *                                                      (npr. defaults/foto-i-video.jpg; ili jedna
 *                                                      defaults/pruzatelj.jpg — VENDOR_DEFAULT_MODE)
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
/*  "per-category" → public/images/defaults/<kategorija>.jpg (kao dosad) */
/*  "single"       → public/images/defaults/pruzatelj.jpg za sve        */
/* ------------------------------------------------------------------ */
export const VENDOR_DEFAULT_MODE: "per-category" | "single" = "per-category";
export const VENDOR_DEFAULT_SINGLE = "pruzatelj";

export function vendorDefaultImage(category: string): string {
  const file = VENDOR_DEFAULT_MODE === "single" ? VENDOR_DEFAULT_SINGLE : category;
  return `${IMAGE_BASE}/defaults/${file}${DEFAULT_EXT}`;
}

export interface VendorImage {
  src: string;
  isDefault: boolean;
}

export type Imaged = Pick<Vendor, "slug" | "category" | "photos">;

export function vendorImages(vendor: Imaged): VendorImage[] {
  if (vendor.photos && vendor.photos.length > 0) {
    return vendor.photos.map((file) => ({
      // Uploadane slike (Faza 5) dolaze kao apsolutan URL (http(s)://… ili /uploads/…) → koristi izravno.
      // Seed slike iz importa su gola imena datoteka → primijeni konvenciju {BASE}/vendors/<slug>/<file>.
      src: isAbsolute(file) ? file : `${IMAGE_BASE}/vendors/${vendor.slug}/${file}`,
      isDefault: false,
    }));
  }
  return [{ src: vendorDefaultImage(vendor.category), isDefault: true }];
}

function isAbsolute(src: string): boolean {
  return /^https?:\/\//i.test(src) || src.startsWith("/uploads/");
}

export function coverImage(vendor: Imaged): VendorImage {
  return vendorImages(vendor)[0];
}
