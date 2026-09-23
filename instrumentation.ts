import * as Sentry from "@sentry/nextjs";

/**
 * Monitoring (§Zadatak 8) — server + edge strana, preko Next.js-evog vlastitog instrumentation
 * hooka (`register()`), koji @sentry/nextjs v11 sad traži umjesto starijih `sentry.server.config.ts`
 * / `sentry.edge.config.ts` fajlova (v. instrumentation-client.ts za klijentsku stranu i napomenu
 * o promjeni konvencije). `next.config.mjs`'s `withSentryConfig` automatski uključuje
 * `experimental.instrumentationHook` za Next 14 (ova verzija projekta) — ništa dodatno ovdje
 * nije potrebno postaviti ručno.
 *
 * Aktivan SAMO ako je DSN postavljen — bez njega `register()` odmah vraća, nula promjene
 * ponašanja (isti "aktivno samo s ključem" etos kao Resend/backend Sentry).
 */
export async function register() {
  const dsn = process.env.NEXT_PUBLIC_SENTRY_DSN;
  if (!dsn) return;

  Sentry.init({
    dsn,
    tracesSampleRate: 0.1,
    // Napomena: JS/Node SDK (za razliku od .NET Sentry.AspNetCore) nema "sendDefaultPii" opciju —
    // IP/PII se ne šalje automatski osim ako se eksplicitno ne uključi posebnom integracijom.
  });
}

// Hvata greške iz Server Componenata/Route Handlera koje Next.js inače samo interno logira.
export const onRequestError = Sentry.captureRequestError;
