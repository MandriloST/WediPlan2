import type { Metadata } from "next";
import { Suspense } from "react";
import AuthShell from "@/components/AuthShell";
import LoginForm from "@/components/LoginForm";
import Link from "next/link";

export const metadata: Metadata = { title: "Prijava — Wediplan", robots: { index: false, follow: false } };
export const dynamic = "force-dynamic";

export default function LoginPage() {
  return (
    <AuthShell
      title="Prijava"
      footer={<>Nemate račun? <Link href="/prijava/registracija">Registrirajte se</Link></>}
    >
      <Suspense fallback={<div className="auth-form" aria-busy />}>
        <LoginForm />
      </Suspense>
    </AuthShell>
  );
}
