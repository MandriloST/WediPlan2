/**
 * npm run featured:check [YYYY-MM-DD]
 *
 * Provjeri data/featured.json (sekcija "Izdvajamo" na naslovnici) i ispiši tko je izdvojen
 * danas (ili na zadani datum). Upute: IZDVOJENI.md.
 *
 * Koristi istu logiku kao naslovnica (lib/featured.ts) — Node ≥ 22.18 izvodi .ts izravno.
 * Provjerava i postoji li slug u data/vendors.json (mock podaci). Kad je spojen pravi
 * backend, pružatelj iz baze kojeg nema u vendors.json bit će prijavljen kao upozorenje, ne greška.
 */
import { readFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import path from "node:path";

const [major, minor] = process.versions.node.split(".").map(Number);
if (major < 22 || (major === 22 && minor < 18)) {
  console.error(`Treba Node 22.18 ili noviji (imaš ${process.versions.node}).`);
  process.exit(2);
}

const root = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
const { activePlacements, validatePlacement, isActiveOn, splitTiers, todayInZagreb } = await import(
  "../lib/featured.ts"
);

const arg = process.argv[2];
if (arg && !/^\d{4}-\d{2}-\d{2}$/.test(arg)) {
  console.error(`Datum mora biti YYYY-MM-DD (dobiveno: ${arg})`);
  process.exit(2);
}
const day = arg ?? todayInZagreb();

let file;
try {
  file = JSON.parse(await readFile(path.join(root, "data", "featured.json"), "utf8"));
} catch (e) {
  console.error("data/featured.json se ne može pročitati (neispravan JSON?):\n  " + e.message);
  process.exit(1);
}
const vendors = JSON.parse(await readFile(path.join(root, "data", "vendors.json"), "utf8"));
const known = new Map(vendors.map((v) => [v.slug, v]));

const list = Array.isArray(file.placements) ? file.placements : [];
let errors = 0;
let warnings = 0;

console.log(`data/featured.json — ${list.length} unosa\n`);
list.forEach((p, i) => {
  const errs = validatePlacement(p);
  const label = `#${i + 1} ${p?.slug ?? "?"}`;
  if (errs.length) {
    errors++;
    console.log(`  ✗ ${label}: ${errs.join("; ")}`);
    return;
  }
  const notes = [];
  if (!known.has(p.slug.trim())) {
    warnings++;
    notes.push("NEMA ga u data/vendors.json (krivo napisan slug? — na mocku se neće prikazati)");
  }
  const status = isActiveOn(p, day) ? "aktivan" : p.from > day ? `počinje ${p.from}` : `istekao ${p.to}`;
  console.log(`  ✓ ${label} — poz. ${p.position}, ${p.from} → ${p.to ?? "bez kraja"}, ${p.kind}, ${status}${notes.length ? `\n      ⚠ ${notes.join("; ")}` : ""}`);
});

const active = activePlacements(list, day).filter((p) => known.has(p.slug));
const { hero, medium, small } = splitTiers(active);
const name = (p) => `${known.get(p.slug)?.name ?? p.slug}${p.kind === "sponsored" ? " (sponzorirano)" : ""}`;

console.log(`\nNa naslovnici ${day}:`);
if (!hero) console.log("  (nitko — sekcija Izdvajamo se ne prikazuje)");
else {
  console.log(`  veliki:  ${name(hero)}`);
  if (medium.length) console.log(`  srednji: ${medium.map(name).join(", ")}`);
  if (small.length) console.log(`  mali:    ${small.map(name).join(", ")}`);
}

const activeAll = list.filter((p) => !validatePlacement(p).length && isActiveOn(p, day));
if (activeAll.length > 6) {
  warnings++;
  console.log(`\n  ⚠ aktivno je ${activeAll.length} unosa, prikazuje se samo prvih 6 po poziciji`);
}

console.log(`\n${errors ? `✗ ${errors} grešaka` : "✓ bez grešaka"}${warnings ? `, ⚠ ${warnings} upozorenja` : ""}`);
process.exit(errors ? 1 : 0);
