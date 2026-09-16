#!/usr/bin/env node
/**
 * Skenira public/images/vendors/<slug>/ i puni `photos` u data/vendors.json.
 *
 *   npm run sync:images        ← nakon dodavanja/brisanja slika (bez Excela)
 *
 * Import skripta (import-vendors.mjs) koristi isti sken automatski.
 * Konvencija: 01.jpg, 02.jpg, 03.jpg (max 3; jpg/jpeg/png/webp/avif).
 */
import { existsSync, readdirSync, readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import path from "node:path";

const ROOT = path.join(path.dirname(fileURLToPath(import.meta.url)), "..");
const IMG_DIR = path.join(ROOT, "public", "images", "vendors");
const DEFAULTS_DIR = path.join(ROOT, "public", "images", "defaults");
const DEFAULT_EXT = ".jpg"; // mora pratiti lib/images.ts
const IMG_RE = /\.(jpe?g|png|webp|avif)$/i;
const MAX_PHOTOS = 3;

/** slug → ["01.jpg", ...]; warnings za višak slika */
export function scanImages() {
  const map = new Map();
  const warnings = [];
  if (!existsSync(IMG_DIR)) return { map, warnings, dirs: [] };
  const dirs = readdirSync(IMG_DIR, { withFileTypes: true })
    .filter((e) => e.isDirectory())
    .map((e) => e.name);
  for (const slug of dirs) {
    const files = readdirSync(path.join(IMG_DIR, slug)).filter((f) => IMG_RE.test(f)).sort();
    if (files.length > MAX_PHOTOS) {
      warnings.push(
        `  ${slug}: ${files.length} slika — koristim prve ${MAX_PHOTOS} (${files.slice(0, MAX_PHOTOS).join(", ")})`
      );
    }
    if (files.length) map.set(slug, files.slice(0, MAX_PHOTOS));
  }
  return { map, warnings, dirs };
}

/** Primijeni sken na listu vendora (mutira); vraća poruke. */
export function applyImages(vendors) {
  const { map, warnings, dirs } = scanImages();
  const slugs = new Set(vendors.map((v) => v.slug));
  for (const v of vendors) v.photos = map.get(v.slug) ?? [];
  const orphans = dirs.filter((d) => !slugs.has(d));
  if (orphans.length) {
    warnings.push(`  folderi bez pružatelja (tipfeler u imenu?): ${orphans.join(", ")}`);
  }
  // default slika mora postojati za svaku kategoriju koju netko bez slika koristi
  const needDefault = new Set(vendors.filter((v) => !v.photos.length).map((v) => v.category));
  for (const cat of needDefault) {
    if (!existsSync(path.join(DEFAULTS_DIR, cat + DEFAULT_EXT))) {
      warnings.push(`  NEDOSTAJE default slika: public/images/defaults/${cat}${DEFAULT_EXT} (vendori te kategorije bez slika prikazat će razbijenu sliku!)`);
    }
  }
  const withPhotos = vendors.filter((v) => v.photos.length).length;
  return { warnings, withPhotos };
}

/* ---- standalone: npm run sync:images ---- */
if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const file = path.join(ROOT, "data", "vendors.json");
  const vendors = JSON.parse(readFileSync(file, "utf8"));
  const { warnings, withPhotos } = applyImages(vendors);
  if (warnings.length) {
    console.log(`⚠ Upozorenja:`);
    warnings.forEach((w) => console.log(w));
  }
  writeFileSync(file, JSON.stringify(vendors, null, 2) + "\n");
  console.log(`✓ ${withPhotos}/${vendors.length} pružatelja ima stvarne slike → data/vendors.json ažuriran`);
  console.log("  Ostali prikazuju default sliku svoje kategorije (public/images/defaults/).");
}
