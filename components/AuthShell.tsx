"use client";

import Link from "next/link";
import { ReactNode } from "react";

/** Zajednički okvir za auth stranice — kartica, naslov, natpis ispod. */
export default function AuthShell({
  title,
  children,
  footer,
}: {
  title: string;
  children: ReactNode;
  footer?: ReactNode;
}) {
  return (
    <main className="container auth-page">
      <div className="auth-card">
        <Link href="/" className="auth-logo" aria-label="Wediplan">
          WEDI<span className="boxed">PLAN</span>
        </Link>
        <h1>{title}</h1>
        {children}
      </div>
      {footer && <p className="auth-foot">{footer}</p>}
    </main>
  );
}
