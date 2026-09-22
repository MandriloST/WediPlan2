import { AuthError } from "./auth";
import type {
  AdminClaim,
  AdminOptOut,
  AdminReview,
  Claim,
  ProviderPhoto,
  ProviderVendor,
  VendorDraft,
} from "@/lib/types";

/**
 * Faza 4 klijent: preuzimanje profila (claim), nadzorna ploča partnera, korisničke recenzije,
 * admin moderacija. Sve ide na relativni /api/* (rewrite na .NET), s httpOnly session cookieom
 * (credentials "include"). Greške bacaju AuthError (isti obrazac kao auth klijent).
 */

async function call<T>(path: string, method: string, body?: unknown): Promise<T> {
  const res = await fetch(`/api${path}`, {
    method,
    credentials: "include",
    headers: body ? { "Content-Type": "application/json" } : undefined,
    body: body ? JSON.stringify(body) : undefined,
  });
  const text = await res.text();
  const data = text ? JSON.parse(text) : {};
  if (!res.ok) throw new AuthError(data.error ?? "error", res.status, data.details);
  return data as T;
}

// ---------------------------------------------------------------- claim
export const claimApi = {
  create: (vendorSlug: string, message?: string) =>
    call<Claim>("/claims", "POST", { vendorSlug, message }),
  mine: () => call<Claim[]>("/claims/mine", "GET"),
};

// ---------------------------------------------------------------- provider dashboard
export const providerApi = {
  vendors: () => call<ProviderVendor[]>("/provider/vendors", "GET"),
  saveDraft: (slug: string, draft: VendorDraft) =>
    call<void>(`/provider/vendors/${encodeURIComponent(slug)}/draft`, "PUT", draft),
  publish: (slug: string) =>
    call<void>(`/provider/vendors/${encodeURIComponent(slug)}/publish`, "POST"),

  // Faza 5 — fotografije (samo odobreni vlasnik)
  uploadPhoto: async (slug: string, file: File): Promise<ProviderPhoto> => {
    const fd = new FormData();
    fd.append("file", file);
    const res = await fetch(`/api/provider/vendors/${encodeURIComponent(slug)}/photos`, {
      method: "POST",
      credentials: "include",
      body: fd, // ne postavljati Content-Type ručno — browser dodaje boundary
    });
    const text = await res.text();
    const data = text ? JSON.parse(text) : {};
    if (!res.ok) throw new AuthError(data.error ?? "error", res.status, data.details);
    return data as ProviderPhoto;
  },
  deletePhoto: (slug: string, id: string) =>
    call<void>(`/provider/vendors/${encodeURIComponent(slug)}/photos/${id}`, "DELETE"),
  reorderPhotos: (slug: string, orderedIds: string[], coverId: string | null) =>
    call<void>(`/provider/vendors/${encodeURIComponent(slug)}/photos/order`, "PUT", { orderedIds, coverId }),
};

// ---------------------------------------------------------------- korisničke recenzije
export const reviewApi = {
  create: (vendorSlug: string, rating: number, text: string) =>
    call<{ status: string; message: string }>("/reviews", "POST", { vendorSlug, rating, text }),
};

// ---------------------------------------------------------------- admin
export const adminApi = {
  claims: (status = "pending") =>
    call<AdminClaim[]>(`/admin/claims?status=${encodeURIComponent(status)}`, "GET"),
  approveClaim: (id: string) => call<{ status: string }>(`/admin/claims/${id}/approve`, "POST"),
  rejectClaim: (id: string) => call<{ status: string }>(`/admin/claims/${id}/reject`, "POST"),

  reviews: (status = "pending") =>
    call<AdminReview[]>(`/admin/reviews?status=${encodeURIComponent(status)}`, "GET"),
  approveReview: (id: string) => call<{ status: string }>(`/admin/reviews/${id}/approve`, "POST"),
  rejectReview: (id: string) => call<{ status: string }>(`/admin/reviews/${id}/reject`, "POST"),

  unpublish: (slug: string) =>
    call<{ slug: string; isPublished: boolean }>(`/admin/vendors/${encodeURIComponent(slug)}/unpublish`, "POST"),
  republish: (slug: string) =>
    call<{ slug: string; isPublished: boolean }>(`/admin/vendors/${encodeURIComponent(slug)}/publish`, "POST"),

  // Faza 6 — GDPR opt-out pregled
  optouts: () => call<AdminOptOut[]>("/admin/optouts", "GET"),
  restoreOptout: (slug: string) =>
    call<{ ok: boolean }>(`/admin/vendors/${encodeURIComponent(slug)}/restore-optout`, "POST"),
};

/** Javni GDPR opt-out (§9) — neclaimani pružatelj traži skidanje profila. Bez prijave. */
export const optOutApi = {
  submit: (slug: string, reason: string, contact: string) =>
    call<{ ok: boolean }>("/optout", "POST", { slug, reason: reason || null, contact: contact || null }),
};

/** Prijateljske HR poruke za Faza 4 kodove (nadopuna authMessage). */
export function providerMessage(e: unknown): string {
  if (e instanceof AuthError) {
    switch (e.code) {
      case "already_claimed":
        return "Ovaj profil je već preuzet.";
      case "already_reviewed":
        return "Već ste ostavili recenziju za ovog pružatelja.";
      case "own_vendor":
        return "Ne možete recenzirati vlastiti profil.";
      case "email_not_confirmed":
        return "Za pisanje recenzije prvo potvrdite svoju e-mail adresu (poveznica u e-mailu koji smo poslali pri registraciji).";
      case "vendor_not_found":
        return "Pružatelj nije pronađen.";
      case "invalid_price":
      case "invalid_price_kind":
        return "Cijena nije ispravna. Provjerite unos.";
      case "no_draft":
        return "Nema izmjena za objavu.";
      case "file_too_large":
        return "Slika je prevelika (maks. 10 MB).";
      case "not_an_image":
      case "invalid_image":
        return "Datoteka nije valjana slika.";
      case "too_many_photos":
        return "Dosegnut je maksimalan broj fotografija.";
      default:
        if (e.status === 401) return "Prijavite se za nastavak.";
        if (e.status === 403) return "Nemate ovlasti za ovu radnju.";
        return "Nešto nije u redu. Pokušajte ponovno.";
    }
  }
  return "Nešto nije u redu. Pokušajte ponovno.";
}
