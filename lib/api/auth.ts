import type { VendorListParams } from "./client";

/**
 * Klijentski auth pozivi (Faza 3). Sve ide na relativni /api/auth/* i /api/me — kroz Next.js
 * rewrite na .NET (isti origin), pa httpOnly session cookie radi automatski.
 * VAŽNO: credentials "include" da se cookie šalje/prima i u dev-u (localhost:3000 → :5080 preko rewritea
 * je isti origin, ali include ne škodi i pokriva izravne pozive).
 */

export interface Me {
  id: string;
  email: string;
  displayName?: string;
  emailConfirmed: boolean;
  roles: string[];
}

export interface AuthProviders {
  password: boolean;
  magicLink: boolean;
  google: boolean;
}

export class AuthError extends Error {
  constructor(public code: string, public status: number, public details?: string[]) {
    super(code);
  }
}

async function call<T>(path: string, body?: unknown, method = "POST"): Promise<T> {
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

export const authApi = {
  me: async (): Promise<Me | null> => {
    const res = await fetch("/api/me", { credentials: "include" });
    if (res.status === 401) return null;
    if (!res.ok) throw new AuthError("me_failed", res.status);
    return res.json();
  },
  providers: () => call<AuthProviders>("/auth/providers", undefined, "GET"),

  register: (email: string, password: string, displayName?: string) =>
    call<{ message: string }>("/auth/register", { email, password, displayName }),
  login: (email: string, password: string) => call<Me>("/auth/login", { email, password }),
  logout: () => call<{ message: string }>("/auth/logout", {}),

  requestMagic: (email: string) => call<{ message: string }>("/auth/magic/request", { email }),
  consumeMagic: (token: string) => call<Me>("/auth/magic/consume", { token }),

  verifyEmail: (token: string) => call<Me>("/auth/verify-email", { token }),
  forgotPassword: (email: string) => call<{ message: string }>("/auth/password/forgot", { email }),
  resetPassword: (email: string, token: string, password: string) =>
    call<{ message: string }>("/auth/password/reset", { email, token, password }),

  /** URL za Google prijavu (redirect). returnTo = putanja natrag nakon prijave. */
  googleUrl: (returnTo = "/") => `/api/auth/google?returnTo=${encodeURIComponent(returnTo)}`,
};
