import type { Metadata } from "next";
import ProfileShell from "@/components/ProfileShell";

export const metadata: Metadata = { title: "Profil — Wediplan" };

export default function ProfilePage() {
  return <ProfileShell />;
}
