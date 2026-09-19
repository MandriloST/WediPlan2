import Image from "next/image";
import Link from "next/link";
import { categoryImage } from "@/lib/images";

/** 1 pružatelj · 2 pružatelja · 5 pružatelja · 21 pružatelj */
export function providersLabel(n: number): string {
  return n % 10 === 1 && n % 100 !== 11 ? `${n} pružatelj` : `${n} pružatelja`;
}

interface Props {
  slug: string;
  label: string;
  href: string;
  /** undefined = brojač se još učitava (ne prikazuje se) */
  count?: number;
  /** kategorija bez pružatelja (u regiji) — prigušena, ali klikabilna (SEO + prazno stanje nudi širenje) */
  empty?: boolean;
  title?: string;
  priority?: boolean;
}

/** Foto-pločica kategorije — ista na naslovnici i na /kategorije. Slika: public/images/categories/<slug>.jpg */
export default function CategoryTile({ slug, label, href, count, empty, title, priority }: Props) {
  return (
    <Link href={href} className={`ctile${empty ? " is-empty" : ""}`} title={title}>
      <Image
        src={categoryImage(slug)}
        alt=""
        fill
        sizes="(max-width: 700px) 50vw, (max-width: 900px) 33vw, 180px"
        priority={priority}
      />
      <span className="ctile-txt">
        <strong>{label}</strong>
        {count !== undefined && <span>{providersLabel(count)}</span>}
      </span>
    </Link>
  );
}
