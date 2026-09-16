/**
 * Deterministički raspored pinova koji dijele istu točku (§L, odluka d).
 *
 * Problem: import geokodira ~3170 pružatelja po GRADU (precision=city) → svi fotografi
 * iz Splita dobiju istu koordinatu (centroid) i pinovi se slažu jedan na drugi.
 *
 * Rješenje: pružatelje iste točke rasporedimo po Vogelovoj (suncokret) spirali oko
 * centroida. Za razliku od nasumičnog pomaka, spirala daje ravnomjeran razmak pa se
 * susjedni pinovi ne preklapaju, a polumjer raste s √n (Zagreb s 100 fotografa ≈ 3 km,
 * mjesto s 2 pružatelja ≈ 0,4 km).
 *
 * Determinizam: redoslijed unutar skupine je po hashu sluga → isti skup pružatelja
 * uvijek daje iste pozicije (pin ne "skače" pri re-renderu, osvježavanju ili dijeljenju
 * linka). Pozicija se mijenja samo ako se promijeni sastav skupine na toj točki.
 *
 * Pravila:
 *  - precision "city"  → uvijek spirala (razmak CITY_SPACING_M), i kad je sam (mali
 *    pomak od centroida da pin ne sjedi točno na oznaci grada — iskreno "negdje u gradu")
 *  - precision "exact" (ili izostavljeno) → spirala SAMO ako ≥ 2 imaju identične
 *    koordinate (razmak EXACT_SPACING_M — npr. dva pružatelja u istoj zgradi)
 *  - bez koordinata → nema pina (kao i prije)
 */

export const CITY_SPACING_M = 300;
export const EXACT_SPACING_M = 40;
const GOLDEN_ANGLE = Math.PI * (3 - Math.sqrt(5)); // ≈ 137,5°
const M_PER_DEG_LAT = 111_320;

export interface JitterInput {
  slug: string;
  lng: number | null;
  lat: number | null;
  locationPrecision?: "exact" | "city" | "region";
}

/** FNV-1a 32-bit — mali, brz, stabilan hash (bez ovisnosti). */
export function hash32(s: string): number {
  let h = 0x811c9dc5;
  for (let i = 0; i < s.length; i++) {
    h ^= s.charCodeAt(i);
    h = Math.imul(h, 0x01000193);
  }
  return h >>> 0;
}

function offset(lng: number, lat: number, meters: number, angle: number): [number, number] {
  const dLat = (meters * Math.sin(angle)) / M_PER_DEG_LAT;
  const dLng = (meters * Math.cos(angle)) / (M_PER_DEG_LAT * Math.cos((lat * Math.PI) / 180));
  return [lng + dLng, lat + dLat];
}

/**
 * Vraća mapu slug → [lng, lat] za prikaz. Pružatelji bez koordinata se izostavljaju.
 * Ulazne koordinate se ne mijenjaju (podaci ostaju istiniti; pomak je samo prikaz).
 */
export function spreadPositions<T extends JitterInput>(items: T[]): Map<string, [number, number]> {
  const groups = new Map<string, T[]>();
  for (const v of items) {
    if (v.lng == null || v.lat == null) continue;
    const isCity = v.locationPrecision === "city";
    const key = isCity
      ? `c|${v.lng.toFixed(4)}|${v.lat.toFixed(4)}`
      : `e|${v.lng.toFixed(5)}|${v.lat.toFixed(5)}`;
    const g = groups.get(key);
    if (g) g.push(v);
    else groups.set(key, [v]);
  }

  const out = new Map<string, [number, number]>();
  for (const [key, members] of Array.from(groups)) {
    const isCity = key.startsWith("c|");
    const lng = members[0].lng as number;
    const lat = members[0].lat as number;

    if (!isCity && members.length === 1) {
      out.set(members[0].slug, [lng, lat]); // točna, jedinstvena lokacija — ne diraj
      continue;
    }

    const spacing = isCity ? CITY_SPACING_M : EXACT_SPACING_M;
    // stabilan redoslijed: hash sluga (slug kao tie-break za rijetke kolizije)
    const ordered = members
      .map((m) => ({ m, h: hash32(m.slug) }))
      .sort((a, b) => a.h - b.h || a.m.slug.localeCompare(b.m.slug));
    // zakretanje cijele spirale po točki → susjedni gradovi ne izgledaju identično
    const rot = (hash32(key) / 0xffffffff) * 2 * Math.PI;

    ordered.forEach(({ m }, i) => {
      const r = spacing * Math.sqrt(i + 0.5);
      out.set(m.slug, offset(lng, lat, r, i * GOLDEN_ANGLE + rot));
    });
  }
  return out;
}
