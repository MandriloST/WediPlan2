import type { Metadata } from "next";
import { Suspense } from "react";
import AuthShell from "@/components/AuthShell";
import ClaimVerifyClient from "@/components/ClaimVerifyClient";

export const metadata: Metadata = { title: "Potvrda vlasništva — Wediplan", robots: { index: false, follow: false } };
export const dynamic = "force-dynamic";

export default function ClaimVerifyPage() {
  return (
    <AuthShell title="Potvrda vlasništva profila">
      <Suspense fallback={<div className="auth-sent" aria-busy />}>
        <ClaimVerifyClient />
      </Suspense>
    </AuthShell>
  );
}
