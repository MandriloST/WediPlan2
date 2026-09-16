import type { Metadata } from "next";
import { Suspense } from "react";
import AuthShell from "@/components/AuthShell";
import TokenActionClient from "@/components/TokenActionClient";

export const metadata: Metadata = { title: "Potvrda e-maila — Wediplan", robots: { index: false, follow: false } };
export const dynamic = "force-dynamic";

export default function VerifyPage() {
  return (
    <AuthShell title="Potvrda e-maila">
      <Suspense fallback={<div className="auth-sent" aria-busy />}>
        <TokenActionClient kind="verify" />
      </Suspense>
    </AuthShell>
  );
}
