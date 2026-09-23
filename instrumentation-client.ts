import * as Sentry from "@sentry/nextjs";

/**
 * Monitoring (§Zadatak 8, PLAN-PRIORITETI-LANSIRANJE-2.md) — klijentska strana.
 *
 * NAPOMENA o konvenciji: @sentry/nextjs v11 je NAPUSTIO stariji `sentry.client.config.ts` obrazac
 * (SDK sad eksplicitno upozorava i traži brisanje tog fajla) u korist ovog fajla —
 * `instrumentation-client.ts` u rootu projekta, next/og-nezavisna Next.js konvencija koju SDK
 * prepoznaje automatski. `instrumentation.ts` (server/edge dio) je odvojen, v. taj fajl.
 *
 * Aktivan SAMO ako je DSN postavljen I u produkciji (isti "aktivno samo s ključem" etos kao
 * Resend/backend Sentry) — bez NEXT_PUBLIC_SENTRY_DSN ovo je no-op, dev ostaje netaknut.
 */
const dsn = process.env.NEXT_PUBLIC_SENTRY_DSN;

if (dsn) {
  Sentry.init({
    dsn,
    enabled: process.env.NODE_ENV === "production",
    tracesSampleRate: 0.1,
    // Napomena: JS/Node SDK (za razliku od .NET Sentry.AspNetCore) nema "sendDefaultPii" opciju —
    // IP/PII se ne šalje automatski osim ako se eksplicitno ne uključi posebnom integracijom.
  });
}

// Traži SDK (v. build upozorenje ako se izostavi) — instrumentira App Router navigacije.
export const onRouterTransitionStart = Sentry.captureRouterTransitionStart;
