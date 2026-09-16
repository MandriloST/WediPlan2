#!/usr/bin/env node
/**
 * Uvoz pružatelja iz Excel predloška u data/vendors.json + data/profiles.json.
 *
 *   npm run import:vendors -- putanja/do/datoteke.xlsx
 *
 * Validira svaki redak i ispisuje greške s brojem retka; ne piše ništa dok
 * ima grešaka. Redci čiji naziv počinje s "PRIMJER" se preskaču.
 */
import { readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import path from "node:path";
import xlsx from "xlsx";
import { applyImages } from "./sync-images.mjs";

const ROOT = path.join(path.dirname(fileURLToPath(import.meta.url)), "..");
const file = process.argv[2] ?? path.join(ROOT, "data", "vendors-template.xlsx");

/* ---- šifrarnici (moraju pratiti lib/data.ts) ---- */
const REGIONS = {
  istra: "Istra", kvarner: "Kvarner", dalmacija: "Dalmacija",
  zagreb: "Zagreb i okolica", slavonija: "Slavonija",
};
const CATEGORIES = {
  "restorani-i-sale": "Restorani i sale",
  "konobe-i-prostori": "Konobe i prostori za proslavu",
  "najam-kuce": "Najam kuće za proslavu",
  "najam-satora": "Najam šatora i opreme",
  "catering": "Catering",
  "torte-i-kolaci": "Torte i kolači",
  "foto-i-video": "Foto i Video",
  "foto-kabine": "Audio, foto kabine i selfie mirror",
  "glazba-bendovi": "Glazba — bendovi",
  "dj": "DJ",
  "glazba-za-crkvu": "Glazba za crkvu",
  "harmonikasi": "Harmonikaši",
  "cvijece-i-dekoracije": "Cvijeće, dekoracije i baloni",
  "vjencanice": "Vjenčanice i dodaci",
  "muska-odijela": "Muška odijela i dodaci",
  "nakit-i-prstenje": "Nakit i prstenje",
  "frizerski-saloni": "Frizerski saloni",
  "sminka-nokti": "Šminka, nokti i trepavice",
  "najam-limuzina": "Najam auta, limuzina i oldtimera",
  "prijevoz-i-transferi": "Prijevoz i transferi",
  "organizatori": "Organizatori vjenčanja",
  "pozivnice": "Pozivnice, zahvalnice i popis gostiju",
  "rasvjeta-i-razglas": "Rasvjeta, razglas i efekti",
  "cuvanje-djece": "Čuvanje i animacija djece",
  "skole-plesa": "Škole plesa",
  "bracna-putovanja": "Bračna putovanja",
  "dodaci-za-djevojacke": "Dodaci za djevojačke i momačke",
  "reveri-i-dodaci": "Reveri, narukvice, podvezice i dodaci",
};

const norm = (s) =>
  String(s ?? "").toLowerCase().replace(/đ/g, "d").normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "").replace(/\s+/g, " ").trim();

const slugify = (s) => norm(s).replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");

const CAT_LOOKUP = new Map();
for (const [slug, name] of Object.entries(CATEGORIES)) {
  CAT_LOOKUP.set(norm(name), slug);
  CAT_LOOKUP.set(slug, slug);
}
// tolerantno: "glazba - bendovi" / "glazba bendovi" (crtica varira)
CAT_LOOKUP.set(norm("Glazba - bendovi"), "glazba-bendovi");
CAT_LOOKUP.set(norm("Glazba bendovi"), "glazba-bendovi");
CAT_LOOKUP.set(norm("Čuvanje i animacije djece"), "cuvanje-djece");
// spojene/ukinute kategorije -> nasljednica (stari Excel redci i dalje rade)
const MERGED = new Map([[norm("Auto za mladence (rent a car)"), "najam-limuzina"], [norm("Auto za mladence"), "najam-limuzina"]]);

const REG_LOOKUP = new Map();
for (const [id, name] of Object.entries(REGIONS)) {
  REG_LOOKUP.set(norm(name), id);
  REG_LOOKUP.set(id, id);
}

const yn = (v) => norm(v) === "da";

// IG/FB: prihvati "@handle", "handle", "instagram.com/handle", puni URL → kanonski URL
function socialUrl(raw, kind) {
  let s = String(raw ?? "").trim();
  if (!s) return undefined;
  s = s.replace(/^@/, "").replace(/^https?:\/\//i, "").replace(/^www\./i, "");
  const host = kind === "instagram" ? "instagram.com" : "facebook.com";
  // već sadrži domenu (bilo koje varijante) → zadrži putanju
  const m = s.match(/(?:instagram\.com|facebook\.com|fb\.com|fb\.me)\/(.+)/i);
  const path = (m ? m[1] : s).replace(/^\/+/, "").replace(/\/+$/, "").split(/[?#]/)[0];
  if (!path) return undefined;
  return `https://${host}/${path}`;
}

// Kategorije kojima je fizička lokacija bit ponude — grad i koordinate OBAVEZNI
const VENUE_CATEGORIES = new Set(["restorani-i-sale", "konobe-i-prostori", "najam-kuce"]);

// "Dalmacija; Kvarner" | "cijela Hrvatska" → ["dalmacija","kvarner"] | "hr"
function parseCoverage(raw, errFn) {
  const s = String(raw ?? "").trim();
  if (!s) return undefined;
  if (["cijela hrvatska", "hrvatska", "hr", "sve"].includes(norm(s))) return "hr";
  const ids = [];
  for (const part of s.split(/[;,]/).map((p) => p.trim()).filter(Boolean)) {
    const id = REG_LOOKUP.get(norm(part));
    if (!id) errFn(`pokriva_regije: nepoznata regija „${part}” — dozvoljeno: ${Object.values(REGIONS).join(", ")} ili „cijela Hrvatska”`);
    else if (!ids.includes(id)) ids.push(id);
  }
  return ids.length ? ids : undefined;
}

function parseCoords(raw) {
  const m = String(raw ?? "").replace(/;/g, ",").match(/(-?\d+[.,]?\d*)\s*,\s*(-?\d+[.,]?\d*)/);
  if (!m) return null;
  let lat = parseFloat(m[1].replace(",", "."));
  let lng = parseFloat(m[2].replace(",", "."));
  // Google Maps kopira "lat, lng"; auto-swap ako je zalijepljeno obrnuto
  if (lat >= 13 && lat <= 20 && lng >= 42 && lng <= 47) [lat, lng] = [lng, lat];
  if (lat < 42 || lat > 47 || lng < 13 || lng > 20) return { lat, lng, outOfCroatia: true };
  return { lat, lng };
}

/* ---------------- čitanje ---------------- */
let wb;
try {
  wb = xlsx.read(readFileSync(file));
} catch (e) {
  console.error(`✗ Ne mogu otvoriti: ${file}\n  ${e.message}`);
  process.exit(1);
}
const sheet = wb.Sheets["Pružatelji"] ?? wb.Sheets["Pruzatelji"];
if (!sheet) {
  console.error("✗ Nedostaje list 'Pružatelji'.");
  process.exit(1);
}
const rows = xlsx.utils.sheet_to_json(sheet, { defval: "" });
const revSheet = wb.Sheets["Recenzije"];
const revRows = revSheet ? xlsx.utils.sheet_to_json(revSheet, { defval: "" }) : [];

const col = (row, name) => {
  // "naziv*" ili "naziv" — toleriraj zvjezdicu i velika/mala slova
  for (const key of Object.keys(row)) {
    if (norm(key).replace(/\*$/, "") === name) return row[key];
  }
  return "";
};

const errors = [];
const warnings = [];
const vendors = [];
const profiles = {};
const bySlug = new Map();
const byNormName = new Map();

const hidden = [];
rows.forEach((row, i) => {
  const rowNo = i + 2; // 1 = zaglavlje
  const name = String(col(row, "naziv")).trim();
  if (!name) return; // prazan redak
  if (norm(name).startsWith("primjer")) return;
  if (norm(col(row, "status")) === "skriveno") { hidden.push(name); return; } // interno uklonjen — nikad javna negativna oznaka

  const err = (msg) => errors.push(`  red ${rowNo} (${name || "?"}): ${msg}`);
  const warn = (msg) => warnings.push(`  red ${rowNo} (${name}): ${msg}`);

  const catRaw = col(row, "kategorija");
  let category = CAT_LOOKUP.get(norm(catRaw));
  if (!category && MERGED.has(norm(catRaw))) {
    category = MERGED.get(norm(catRaw));
    warn(`kategorija „${catRaw}” je spojena u „Najam auta, limuzina i oldtimera” — automatski preusmjereno`);
  }
  if (!category) err(`nepoznata kategorija „${catRaw}”`);

  // §4.3: dodatne kategorije — max 2, iz šifrarnika, bez ponavljanja primarne
  const extraCategories = [];
  const extraRaw = String(col(row, "dodatne_kategorije") ?? "").trim();
  if (extraRaw) {
    for (const part of extraRaw.split(";").map((p) => p.trim()).filter(Boolean)) { // SAMO ';' — imena kategorija sadrže zareze
      const c = CAT_LOOKUP.get(norm(part)) ?? MERGED.get(norm(part));
      if (!c) err(`dodatne_kategorije: nepoznata kategorija „${part}”`);
      else if (c === category) warn(`dodatne_kategorije ponavlja primarnu („${part}”) — preskačem`);
      else if (!extraCategories.includes(c)) extraCategories.push(c);
    }
    if (extraCategories.length > 2) err(`dodatne_kategorije: najviše 2 (uneseno ${extraCategories.length}) — više od 3 ukupno ide preko admin odobrenja`);
  }

  const regRaw = col(row, "regija");
  const region = REG_LOOKUP.get(norm(regRaw));
  if (!region) err(`nepoznata regija „${regRaw}”`);

  const city = String(col(row, "grad")).trim();
  const coordsRaw = String(col(row, "koordinate") ?? "").trim();
  const isVenue = category && VENUE_CATEGORIES.has(category);

  let coords = null;
  if (coordsRaw) {
    coords = parseCoords(coordsRaw);
    if (!coords) err(`neispravne koordinate „${coordsRaw}” — očekujem npr. „43.5081, 16.4402”`);
    else if (coords.outOfCroatia) { err(`koordinate (${coords.lat}, ${coords.lng}) izvan Hrvatske`); coords = null; }
  }

  // sjedište ≠ pokrivanje: preciznost lokacije se IZVODI, nikad ne izmišljamo koordinatu
  let locationPrecision;
  if (coords) locationPrecision = "exact";
  else if (city) {
    locationPrecision = "city";
    warn("grad bez koordinata — bez pina na karti dok se ne geokodira (Faza 1, .NET import)");
  } else {
    locationPrecision = "region";
    warn("bez grada i koordinata — prikazuje se u listi regije, bez pina na karti");
  }
  if (isVenue && (!city || !coords)) {
    err("sale/konobe/kuće za proslavu moraju imati grad I koordinate — lokacija im je bit ponude");
  }

  const coverage = parseCoverage(col(row, "pokriva_regije"), err);
  const coverageNote = String(col(row, "pokrivanje_napomena") ?? "").trim();
  if (coverage && Array.isArray(coverage) && region && coverage.length === 1 && coverage[0] === region) {
    warn("pokriva_regije sadrži samo vlastitu regiju — polje može ostati prazno");
  }

  const modeRaw = norm(col(row, "nacin_cijene"));
  const from = Number(col(row, "cijena_od"));
  const to = Number(col(row, "cijena_do"));
  let price = null;
  if (modeRaw.startsWith("na upit")) {
    price = { kind: "onRequest" };
    if (from || to) warn("cijena_od/cijena_do se ignoriraju uz \u201ena upit\u201d");
  } else if (modeRaw.startsWith("po osobi")) {
    if (!from || !to) err("po osobi (raspon) traži cijena_od i cijena_do");
    else if (from >= to) err(`cijena_od (${from}) mora biti manja od cijena_do (${to})`);
    else price = { kind: "perPerson", from, to };
  } else if (modeRaw.startsWith("od")) {
    if (!from) err("nedostaje cijena_od");
    else price = { kind: "from", from };
    if (to) warn("cijena_do se ignorira uz „od (paušal)”");
  } else {
    err(`nepoznat nacin_cijene „${col(row, "nacin_cijene")}”`);
  }

  let rating = Number(col(row, "ocjena")) || 0;
  let reviewCount = Number(col(row, "broj_recenzija")) || 0;
  const ratingSource = String(col(row, "izvor_ocjene")).trim();
  if (rating) {
    if (rating < 0 || rating > 5) err(`ocjena ${rating} izvan raspona 0–5`);
    if (!ratingSource) warn("ocjena bez izvor_ocjene — prenesene ocjene moraju imati izvor");
    if (!reviewCount) reviewCount = 1;
  } else {
    reviewCount = 0;
  }

  const slug = slugify(name);
  if (bySlug.has(slug)) err(`duplikat naziva/sluga s retkom ${bySlug.get(slug)}`);
  bySlug.set(slug, rowNo);
  byNormName.set(norm(name), slug);

  const instagram = socialUrl(col(row, "instagram"), "instagram");
  const facebook = socialUrl(col(row, "facebook"), "facebook");
  const social = instagram || facebook
    ? { ...(instagram && { instagram }), ...(facebook && { facebook }) }
    : undefined;

  if (errors.length) return;

  const styleTags = String(col(row, "stil")).split(",").map((s) => s.trim()).filter(Boolean);
  const services = String(col(row, "usluge")).split(",").map((s) => s.trim()).filter(Boolean);
  const about = String(col(row, "o_pruzatelju")).trim();

  vendors.push({
    id: slug,
    slug,
    name,
    category, // primarna (§4.3)
    ...(extraCategories.length && { categories: [category, ...extraCategories] }),
    region,
    city, // "" = poznata samo regija
    lng: coords ? coords.lng : null,
    lat: coords ? coords.lat : null,
    locationPrecision,
    ...(coverage && { coverage }),
    ...(coverageNote && { coverageNote }),
    price,
    rating,
    reviewCount,
    ratingSource: ratingSource || undefined,
    verified: yn(col(row, "provjereno")),
    liveCalendar: yn(col(row, "kalendar_uzivo")),
    styleTags,
    ...(social && { social }),
    // web/telefon/email NAMJERNO izostavljeni — interni podaci (feature #7: zasad ne)
  });
  if (about || services.length) {
    profiles[slug] = { ...(about && { about }), ...(services.length && { services }) };
  }
});

/* ---------------- recenzije ---------------- */
revRows.forEach((row, i) => {
  const rowNo = i + 2;
  const ref = String(col(row, "naziv_pruzatelja")).trim();
  if (!ref || norm(ref).startsWith("primjer")) return;
  const slug = byNormName.get(norm(ref));
  if (!slug) {
    errors.push(`  Recenzije, red ${rowNo}: pružatelj „${ref}” ne postoji na listu Pružatelji`);
    return;
  }
  const rating = Number(col(row, "ocjena"));
  const text = String(col(row, "tekst")).trim();
  const source = String(col(row, "izvor")).trim();
  if (!rating || rating < 1 || rating > 5) errors.push(`  Recenzije, red ${rowNo}: ocjena mora biti 1–5`);
  if (!text) errors.push(`  Recenzije, red ${rowNo}: nedostaje tekst`);
  if (!source) errors.push(`  Recenzije, red ${rowNo}: nedostaje izvor (obavezno za prenesene recenzije)`);
  if (!rating || !text || !source) return;

  profiles[slug] ??= {};
  profiles[slug].reviews ??= [];
  profiles[slug].reviews.push({
    author: String(col(row, "autor")).trim() || "Anonimno",
    rating,
    text,
    source,
    year: Number(col(row, "godina")) || new Date().getFullYear(),
  });
});

/* ---------------- ispis + zapis ---------------- */
if (warnings.length) {
  console.log(`⚠ Upozorenja (${warnings.length}):`);
  warnings.forEach((w) => console.log(w));
}
if (errors.length) {
  console.error(`\n✗ Greške (${errors.length}) — ništa nije zapisano:`);
  errors.forEach((e) => console.error(e));
  process.exit(1);
}
if (!vendors.length) {
  console.error("✗ Nijedan valjan redak (primjeri se preskaču). Ništa nije zapisano.");
  process.exit(1);
}

const img = applyImages(vendors);
if (img.warnings.length) {
  console.log("⚠ Slike:");
  img.warnings.forEach((w) => console.log(w));
}

writeFileSync(path.join(ROOT, "data", "vendors.json"), JSON.stringify(vendors, null, 2) + "\n");
writeFileSync(path.join(ROOT, "data", "profiles.json"), JSON.stringify(profiles, null, 2) + "\n");

const perRegion = {};
for (const v of vendors) perRegion[v.region] = (perRegion[v.region] ?? 0) + 1;
console.log(`\n✓ Uvezeno ${vendors.length} pružatelja → data/vendors.json (${img.withPhotos} sa stvarnim slikama)`);
console.log(`✓ Profili (o pružatelju/usluge/recenzije): ${Object.keys(profiles).length} → data/profiles.json`);
if (hidden.length) console.log(`  Skriveno (interno uklonjeno): ${hidden.length} — ${hidden.join(", ")}`);
console.log("  Po regijama:", Object.entries(perRegion).map(([r, n]) => `${REGIONS[r]} ${n}`).join(" · "));
console.log("\nSljedeće: pregledaj `npm run dev`, pa git commit data/*.json");
