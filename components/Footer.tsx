import Link from "next/link";

/** Podnožje s pravnim poveznicama (§9, faza 6). */
export default function Footer() {
  const year = new Date().getFullYear();
  return (
    <footer className="site-footer">
      <div className="footer-inner">
        <div className="footer-brand">
          <strong>Wediplan</strong>
          <span className="muted">Sve za vjenčanje u Hrvatskoj</span>
        </div>
        <nav className="footer-links" aria-label="Podnožje">
          <Link href="/pravila-privatnosti">Pravila privatnosti</Link>
          <Link href="/uvjeti-koristenja">Uvjeti korištenja</Link>
          <Link href="/impressum">Impressum</Link>
          <Link href="/partner">Za partnere</Link>
        </nav>
      </div>
      <div className="footer-copy muted">© {year} Wediplan. Sva prava pridržana.</div>
    </footer>
  );
}
