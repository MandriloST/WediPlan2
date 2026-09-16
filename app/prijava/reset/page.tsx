import type { Metadata } from "next";
import { Suspense } from "react";
import AuthShell from "@/components/AuthShell";
import ResetForm from "@/components/ResetForm";

export const metadata: Metadata = { title: "Nova lozinka — Wediplan", robots: { index: false, follow: false } };
export const dynamic = "force-dynamic";

export default function ResetPage() {
  return (
    <AuthShell title="Nova lozinka">
      <Suspense fallback={<div className="auth-form" aria-busy />}>
        <ResetForm />
      </Suspense>
    </AuthShell>
  );
}
