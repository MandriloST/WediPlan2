#!/usr/bin/env node
/**
 * Kreira public/images/vendors/<slug>/ za SVAKOG pružatelja iz data/vendors.json.
 * Ne dira postojeće foldere ni slike — samo doda one koji nedostaju.
 *
 *   npm run scaffold:images
 *
 * Nakon ovoga: ubaci 01.jpg (i po želji 02.jpg, 03.jpg) u odgovarajući folder,
 * pa pokreni `npm run sync:images` da se upišu u data/vendors.json.
 */
import { existsSync, mkdirSync, readdirSync, readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import path from "node:path";

const ROOT = path.join(path.dirname(fileURLToPath(import.meta.url)), "..");
const IMG_DIR = path.join(ROOT, "public", "images", "vendors");
const vendors = JSON.parse(readFileSync(path.join(ROOT, "data", "vendors.json"), "utf8"));

mkdirSync(IMG_DIR, { recursive: true });
const existing = new Set(existsSync(IMG_DIR) ? readdirSync(IMG_DIR) : []);

let created = 0;
for (const v of vendors) {
  const dir = path.join(IMG_DIR, v.slug);
  if (existing.has(v.slug)) continue;
  mkdirSync(dir, { recursive: true });
  writeFileSync(
    path.join(dir, "STAVI_SLIKE_OVDJE.txt"),
    `${v.name}\n\nUbaci ovdje: 01.jpg (glavna), po želji 02.jpg, 03.jpg.\n` +
      `Zatim: npm run sync:images\n\nOva se datoteka može obrisati.\n`
  );
  created++;
}

console.log(`✓ Napravljeno ${created} novih foldera u public/images/vendors/`);
console.log(created ? "  Idi tamo, ubaci 01.jpg u svaki, pa pokreni: npm run sync:images" : "  Svi folderi već postoje.");
