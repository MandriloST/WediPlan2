"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { useAuth } from "@/stores/auth";

/** Glavna navigacija (redizajn 3a). match = rute koje označavaju stavku aktivnom. */
const NAV: { href: string; label: string; match: (p: string) => boolean }[] = [
  { href: "/kategorije", label: "Kategorije", match: (p) => p === "/kategorije" },
  { href: "/karta", label: "Lokacije", match: (p) => p.startsWith("/karta") },
  { href: "/inspiracija", label: "Inspiracija", match: (p) => p.startsWith("/inspiracija") },
  { href: "/partner", label: "Za pružatelje", match: (p) => p.startsWith("/partner") },
];

export default function Header() {
  const pathname = usePathname() ?? "/";

  return (
    <header className="header">
      <div className="container header-in">
        <Link href="/" className="logo" aria-label="Wediplan — početna">
          <span className="wordmark">
            WEDI<span className="boxed">PLAN</span>
          </span>
        </Link>
        <nav className="nav" aria-label="Glavna navigacija">
          {NAV.map((n) => {
            const active = n.match(pathname);
            return (
              <Link
                key={n.href}
                href={n.href}
                className={active ? "active" : undefined}
                aria-current={active ? "page" : undefined}
              >
                {n.label}
              </Link>
            );
          })}
        </nav>
        <div className="header-right">
          <UserMenu />
        </div>
      </div>
    </header>
  );
}

/** Prijava-dugme (gost) ili ime + izbornik (prijavljen). Ne trepće dok traje boot. */
function UserMenu() {
  const { user, loading, logout } = useAuth();
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  const router = useRouter();
  const pathname = usePathname();

  useEffect(() => {
    if (!open) return;
    const close = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener("mousedown", close);
    return () => document.removeEventListener("mousedown", close);
  }, [open]);

  if (loading) return <span className="user-skel" aria-hidden />;

  if (!user) {
    const next = pathname && pathname !== "/" ? `?next=${encodeURIComponent(pathname)}` : "";
    return (
      <Link className="btn btn-sm" href={`/prijava${next}`}>
        Prijava
      </Link>
    );
  }

  const label = user.displayName || user.email;
  const initials = label.slice(0, 2).toUpperCase();

  return (
    <div className="user-menu" ref={ref}>
      <button className="user-btn" aria-expanded={open} onClick={() => setOpen((v) => !v)}>
        <span className="avatar" aria-hidden>
          {initials}
        </span>
        <span className="user-label">{label}</span>
      </button>
      {open && (
        <div className="nav-menu user-dropdown" role="menu">
          <Link href="/profil" role="menuitem" onClick={() => setOpen(false)}>
            Moji favoriti i plan
          </Link>
          {user.roles.includes("provider") && (
            <Link href="/partner" role="menuitem" onClick={() => setOpen(false)}>
              Moj profil pružatelja
            </Link>
          )}
          {user.roles.includes("admin") && (
            <Link href="/admin" role="menuitem" onClick={() => setOpen(false)}>
              Admin
            </Link>
          )}
          <button
            role="menuitem"
            onClick={async () => {
              setOpen(false);
              await logout();
              router.push("/");
            }}
          >
            Odjava
          </button>
        </div>
      )}
    </div>
  );
}
