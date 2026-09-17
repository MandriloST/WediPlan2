import type { Metadata } from "next";
import AuthShell from "@/components/AuthShell";
import RegisterForm from "@/components/RegisterForm";
import Link from "next/link";

export const metadata: Metadata = { title: "Registracija — Wediplan", robots: { index: false, follow: false } };

export default function RegisterPage() {
  return (
    <AuthShell
      title="Napravite račun"
      footer={<>Već imate račun? <Link href="/prijava">Prijavite se</Link></>}
    >
      <RegisterForm />
    </AuthShell>
  );
}
