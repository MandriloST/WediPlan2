import type { Metadata } from "next";
import AuthShell from "@/components/AuthShell";
import ForgotForm from "@/components/ForgotForm";
import Link from "next/link";

export const metadata: Metadata = { title: "Zaboravljena lozinka — Wediplan", robots: { index: false, follow: false } };

export default function ForgotPage() {
  return (
    <AuthShell title="Zaboravljena lozinka" footer={<Link href="/prijava">← Natrag na prijavu</Link>}>
      <ForgotForm />
    </AuthShell>
  );
}
