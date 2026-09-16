"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useCompare } from "@/stores";

const TABS = [
  { href: "/", ico: "🔍", label: "Istraži" },
  { href: "/karta", ico: "🗺", label: "Karta" },
  { href: "/budzet", ico: "🧮", label: "Budžet" },
  { href: "/usporedba", ico: "⇄", label: "Usporedi" },
  { href: "/profil", ico: "♡", label: "Profil" },
];

/** Bottom tab bar (5 tabs), hit targets ≥ 44px — wireframe 2b. */
export default function MobileTabBar() {
  const pathname = usePathname();
  const count = useCompare((s) => s.ids.length);

  return (
    <nav className="tabbar" aria-label="Glavna navigacija">
      {TABS.map((t) => {
        const active = t.href === "/" ? pathname === "/" : pathname.startsWith(t.href);
        return (
          <Link key={t.href} href={t.href} className={active ? "active" : ""}>
            <span className="ico" aria-hidden>
              {t.ico}
            </span>
            {t.label}
            {t.href === "/usporedba" && count > 0 ? ` (${count})` : ""}
          </Link>
        );
      })}
    </nav>
  );
}
