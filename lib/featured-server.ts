import "server-only";
import featuredJson from "@/data/featured.json";
import { getProfile } from "@/lib/api/server";
import { activePlacements, todayInZagreb, type FeaturedItem } from "@/lib/featured";

/**
 * Izdvojeni za naslovnicu (sekcija "Izdvajamo"): aktivni unosi iz data/featured.json
 * spojeni s profilom pružatelja. Radi isto nad mockom i nad pravim API-jem (getProfile).
 *
 * Unos se preskače ako je neispravan, izvan datuma, ili pružatelj ne postoji / skriven / opt-out
 * (getProfile → null). Greška dohvata jednog profila ne ruši ostale.
 */
export async function getFeatured(now: Date = new Date()): Promise<FeaturedItem[]> {
  const placements = activePlacements(
    (featuredJson as { placements?: unknown }).placements,
    todayInZagreb(now),
    (i, errors) => console.warn(`[featured] unos #${i + 1} preskočen: ${errors.join("; ")}`)
  );
  if (placements.length === 0) return [];

  const results = await Promise.allSettled(placements.map((p) => getProfile(p.slug)));
  const items: FeaturedItem[] = [];
  results.forEach((r, i) => {
    const p = placements[i];
    if (r.status === "rejected") {
      console.error(`[featured] profil "${p.slug}" nije dohvaćen:`, r.reason);
      return;
    }
    if (!r.value) {
      console.warn(`[featured] "${p.slug}" ne postoji ili je skriven — preskočen`);
      return;
    }
    items.push({ vendor: r.value.vendor, about: r.value.about ?? "", kind: p.kind });
  });
  return items;
}
