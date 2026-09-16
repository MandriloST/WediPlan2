/**
 * Migracija localStorage → account nakon prijave (§5: "merge, ne pregazi").
 *
 * Backend endpointi za favorite/plan (`/api/favorites`, `/api/budget-plans`) dolaze u zasebnom
 * koraku. Do tada je ovo siguran no-op: prijava NIKAD ne smije pasti zbog sinkronizacije, pa je
 * sve u try/catch i tiho izlazi ako endpointa nema (404). Kad backend bude spreman, popuni se
 * tijelo — storeovi i oblik su već ovdje.
 */

const FAV_KEY = "wediplan.favorites";
const PLAN_KEY = "wediplan.budget";

function readLocal<T>(key: string): T | null {
  try {
    const raw = localStorage.getItem(key);
    return raw ? (JSON.parse(raw) as T) : null;
  } catch {
    return null;
  }
}

/**
 * Gura lokalne favorite i plan u account (merge na serveru). Vraća true ako je nešto poslano.
 * Trenutno: priprema podatke i pokušava POST; 404 (endpoint još ne postoji) se tiho ignorira.
 */
export async function syncLocalToAccount(): Promise<void> {
  try {
    const fav = readLocal<{ state?: { ids?: string[] } }>(FAV_KEY);
    const favIds = fav?.state?.ids ?? [];
    const plan = readLocal<{ state?: { plan?: unknown } }>(PLAN_KEY);

    if (favIds.length === 0 && !plan?.state?.plan) return;

    // Best-effort; endpoint možda još ne postoji (Faza 3 nastavak).
    await fetch("/api/favorites/merge", {
      method: "POST",
      credentials: "include",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ favoriteIds: favIds, plan: plan?.state?.plan ?? null }),
    }).catch(() => {});
  } catch {
    /* prijava se nastavlja bez obzira na sync */
  }
}
