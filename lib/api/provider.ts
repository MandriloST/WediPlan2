import { AuthError } from "./auth";
import type {
  AdminClaim,
  AdminReview,
  Claim,
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
      case "vendor_not_found":
        return "Pružatelj nije pronađen.";
      case "invalid_price":
      case "invalid_price_kind":
        return "Cijena nije ispravna. Provjerite unos.";
      case "no_draft":
        return "Nema izmjena za objavu.";
      default:
        if (e.status === 401) return "Prijavite se za nastavak.";
        if (e.status === 403) return "Nemate ovlasti za ovu radnju.";
        return "Nešto nije u redu. Pokušajte ponovno.";
    }
  }
  return "Nešto nije u redu. Pokušajte ponovno.";
}
