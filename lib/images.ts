import type { Vendor } from "./types";

/**
 * Konvencija slika:
 *   {BASE}/vendors/<slug>/01.jpg, 02.jpg, 03.jpg   ← stvarne slike (max 3, uz dozvolu!)
 *   {BASE}/defaults/<kategorija-slug>.jpg          ← default kategorije kad vendor nema slika
 *                                                      (npr. defaults/foto-i-video.jpg)
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

export interface VendorImage {
  src: string;
  isDefault: boolean;
}

export type Imaged = Pick<Vendor, "slug" | "category" | "photos">;

export function vendorImages(vendor: Imaged): VendorImage[] {
  if (vendor.photos && vendor.photos.length > 0) {
    return vendor.photos.map((file) => ({
      src: `${IMAGE_BASE}/vendors/${vendor.slug}/${file}`,
      isDefault: false,
    }));
  }
  return [{ src: `${IMAGE_BASE}/defaults/${vendor.category}${DEFAULT_EXT}`, isDefault: true }];
}

export function coverImage(vendor: Imaged): VendorImage {
  return vendorImages(vendor)[0];
}
