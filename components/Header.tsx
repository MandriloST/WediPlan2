"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { CATEGORIES } from "@/lib/data";

export default function Header() {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const close = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener("mousedown", close);
    return () => document.removeEventListener("mousedown", close);
  }, [open]);

  return (
    <header className="header">
      <div className="container header-in">
        <Link href="/" className="logo" aria-label="Wediplan — početna">
          <span className="wordmark">
            WEDI<span className="boxed">PLAN</span>
          </span>
        </Link>
        <nav className="nav" aria-label="Kategorije">
          <Link href="/restorani-i-sale">Dvorane</Link>
          <Link href="/foto-i-video">Fotografi</Link>
          <Link href="/glazba-bendovi">Glazba</Link>
          <div className="nav-more" ref={ref}>
            <button aria-expanded={open} onClick={() => setOpen((v) => !v)}>
              Sve ▾
            </button>
            {open && (
              <div className="nav-menu" role="menu">
                {CATEGORIES.map((c) => (
                  <Link key={c.slug} href={`/${c.slug}`} onClick={() => setOpen(false)} role="menuitem">
                    {c.name}
                  </Link>
                ))}
              </div>
            )}
          </div>
        </nav>
        <div className="header-right">
          <a className="partner" href="#partneri">
            Za partnere
          </a>
          <button className="btn btn-sm">Prijava</button>
        </div>
      </div>
    </header>
  );
}
