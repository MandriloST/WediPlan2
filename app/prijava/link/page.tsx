import type { Metadata } from "next";
import { Suspense } from "react";
import AuthShell from "@/components/AuthShell";
import TokenActionClient from "@/components/TokenActionClient";

export const metadata: Metadata = { title: "Prijava — Wediplan", robots: { index: false, follow: false } };
export const dynamic = "force-dynamic";

export default function MagicLinkPage() {
  return (
    <AuthShell title="Prijava u tijeku">
      <Suspense fallback={<div className="auth-sent" aria-busy />}>
        <TokenActionClient kind="magic" />
      </Suspense>
    </AuthShell>
  );
}
