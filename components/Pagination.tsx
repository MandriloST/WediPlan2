"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect } from "react";

interface Props {
  page: number;
  pageCount: number;
  hrefFor: (page: number) => string;
}

/** 1 … 4 5 [6] 7 8 … 20 — uvijek prva i zadnja, ±2 oko trenutne. */
function pageList(page: number, count: number): (number | "…")[] {
  const out: (number | "…")[] = [];
  const from = Math.max(2, page - 2);
  const to = Math.min(count - 1, page + 2);
  out.push(1);
  if (from > 2) out.push("…");
  for (let p = from; p <= to; p++) out.push(p);
  if (to < count - 1) out.push("…");
  if (count > 1) out.push(count);
  return out;
}

/**
 * Paginacija rezultata (12 po stranici). Pravi <a> linkovi s rel=prev/next (SEO, dijeljenje).
 * Desktop: brojevi stranica + strelice lijevo/desno + tipkovnica ←/→.
 * Mobitel: kompaktno "‹ 3 / 9 ›" s velikim metama za dodir (CSS skriva brojeve).
 */
export default function Pagination({ page, pageCount, hrefFor }: Props) {
  const router = useRouter();
  const prev = page > 1 ? hrefFor(page - 1) : undefined;
  const next = page < pageCount ? hrefFor(page + 1) : undefined;

  // tipkovnica ←/→ (ne dok korisnik piše u polje)
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.altKey || e.ctrlKey || e.metaKey || e.shiftKey) return;
      const t = e.target as HTMLElement | null;
      if (t && (t.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(t.tagName))) return;
      if (e.key === "ArrowLeft" && prev) router.push(prev, { scroll: false });
      if (e.key === "ArrowRight" && next) router.push(next, { scroll: false });
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [prev, next, router]);

  if (pageCount <= 1) return null;

  return (
    <nav className="pager" aria-label="Stranice rezultata">
      {prev ? (
        <Link className="pager-step" href={prev} rel="prev" scroll={false} aria-label="Prethodna stranica">
          <span aria-hidden>‹</span>
          <span className="pager-step-txt">Prethodna</span>
        </Link>
      ) : (
        <span className="pager-step is-off" aria-hidden>
          <span>‹</span>
          <span className="pager-step-txt">Prethodna</span>
        </span>
      )}

      <ol className="pager-nums">
        {pageList(page, pageCount).map((p, i) =>
          p === "…" ? (
            <li key={`e${i}`} className="pager-gap" aria-hidden>
              …
            </li>
          ) : (
            <li key={p}>
              <Link
                href={hrefFor(p)}
                scroll={false}
                className={p === page ? "is-current" : undefined}
                aria-current={p === page ? "page" : undefined}
                aria-label={`Stranica ${p}`}
              >
                {p}
              </Link>
            </li>
          )
        )}
      </ol>
      <span className="pager-compact" aria-live="polite">
        {page} / {pageCount}
      </span>

      {next ? (
        <Link className="pager-step" href={next} rel="next" scroll={false} aria-label="Sljedeća stranica">
          <span className="pager-step-txt">Sljedeća</span>
          <span aria-hidden>›</span>
        </Link>
      ) : (
        <span className="pager-step is-off" aria-hidden>
          <span className="pager-step-txt">Sljedeća</span>
          <span>›</span>
        </span>
      )}
    </nav>
  );
}

/** Velike strelice lijevo/desno uz mrežu rezultata (samo široki ekrani — CSS). */
export function SideArrows({ page, pageCount, hrefFor }: Props) {
  if (pageCount <= 1) return null;
  return (
    <>
      {page > 1 && (
        <Link className="side-arrow left" href={hrefFor(page - 1)} scroll={false} tabIndex={-1} aria-hidden title="Prethodna stranica (←)">
          ‹
        </Link>
      )}
      {page < pageCount && (
        <Link className="side-arrow right" href={hrefFor(page + 1)} scroll={false} tabIndex={-1} aria-hidden title="Sljedeća stranica (→)">
          ›
        </Link>
      )}
    </>
  );
}
