import "server-only";
import vendorsJson from "@/data/vendors.json";
import type { Vendor } from "@/lib/types";

/**
 * MOCK katalog (data/vendors.json, generira scripts/import-vendors.mjs).
 * Koristi se SAMO kad API_URL nije postavljen (lokalni rad bez .NET-a) — iz
 * mock ruta app/api/* i lib/api/server.ts. `server-only` jamči da JSON
 * nikad ne završi u klijentskom bundleu.
 */
export const VENDORS: Vendor[] = vendorsJson as unknown as Vendor[];
