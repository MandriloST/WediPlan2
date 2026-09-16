"use client";

import { create } from "zustand";
import { authApi, AuthError, type AuthProviders, type Me } from "@/lib/api/auth";

/**
 * Auth stanje (Faza 3). Sesija je httpOnly cookie na serveru — ovdje se NE čuva token,
 * samo kopija korisnika (Me) za UI. Izvor istine je uvijek /api/me; store se puni pri
 * boot-u (bootstrap) i nakon svake prijave/odjave.
 */
interface AuthState {
  user: Me | null;
  providers: AuthProviders | null;
  /** true dok prvi /api/me nije gotov — UI tad ne trepće između "prijava" i "odjava" */
  loading: boolean;
  bootstrapped: boolean;

  bootstrap: () => Promise<void>;
  setUser: (u: Me | null) => void;
  logout: () => Promise<void>;
  refresh: () => Promise<Me | null>;
}

export const useAuth = create<AuthState>((set, get) => ({
  user: null,
  providers: null,
  loading: true,
  bootstrapped: false,

  bootstrap: async () => {
    if (get().bootstrapped) return;
    set({ bootstrapped: true });
    try {
      const [user, providers] = await Promise.all([
        authApi.me().catch(() => null),
        authApi.providers().catch(() => ({ password: true, magicLink: true, google: false })),
      ]);
      set({ user, providers, loading: false });
    } catch {
      set({ loading: false });
    }
  },

  setUser: (user) => set({ user }),

  refresh: async () => {
    const user = await authApi.me().catch(() => null);
    set({ user });
    return user;
  },

  logout: async () => {
    try {
      await authApi.logout();
    } catch {
      /* i ako padne, lokalno odjavi */
    }
    set({ user: null });
  },
}));

/** Prijateljske HR poruke iz AuthError koda (za forme). */
export function authMessage(e: unknown): string {
  if (e instanceof AuthError) {
    switch (e.code) {
      case "invalid_credentials":
        return "Neispravan e-mail ili lozinka.";
      case "email_not_confirmed":
        return "E-mail nije potvrđen. Provjerite poštu (i spam).";
      case "locked_out":
        return "Previše pokušaja. Pokušajte ponovno za 15 minuta.";
      case "weak_password":
        return "Lozinka je preslaba (najmanje 8 znakova).";
      case "invalid_or_expired":
        return "Poveznica je istekla ili je već iskorištena.";
      default:
        return "Nešto nije u redu. Pokušajte ponovno.";
    }
  }
  return "Nešto nije u redu. Pokušajte ponovno.";
}
