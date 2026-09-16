"use client";

import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { CATEGORIES, REGIONS } from "@/lib/data";
import type { RegionId } from "@/lib/types";
import { api } from "@/lib/api/client";
import { useWeddingDate } from "@/stores";

interface Props {
  initialQ?: string;
  initialRegion?: RegionId;
  /** trenutna kategorija — tekst pretraga je unutar nje (category-first §L) */
  category?: string;
}

const norm = (x: string) =>
  x.toLowerCase().normalize("NFD").replace(/[\u0300-\u036f]/g, "").replace(/đ/g, "d").trim();

/** "fotografi" / "Foto i Video" → foto-i-video (upis točnog naziva ide ravno u kategoriju). */
function exactCategory(q: string): string | undefined {
  const n = norm(q);
  return CATEGORIES.find((c) => norm(c.name) === n || (c.short && norm(c.short) === n))?.slug;
}

export default function SearchBar({ initialQ = "", initialRegion, category }: Props) {
  const router = useRouter();
  const [q, setQ] = useState(initialQ);
  const [region, setRegion] = useState<string>(initialRegion ?? "");
  const { date, setDate } = useWeddingDate();
  const [focused, setFocused] = useState(false);
  const wrapRef = useRef<HTMLDivElement>(null);

  const { data: suggestions } = useQuery({
    queryKey: ["suggest", q.trim()],
    queryFn: ({ signal }) => api.suggest(q.trim(), signal),
    enabled: focused && q.trim().length >= 2,
    placeholderData: (prev) => prev,
  });

  useEffect(() => {
    const close = (e: MouseEvent) => {
      if (wrapRef.current && !wrapRef.current.contains(e.target as Node)) setFocused(false);
    };
    document.addEventListener("mousedown", close);
    return () => document.removeEventListener("mousedown", close);
  }, []);

  function submit() {
    const text = q.trim();
    const cat = text ? exactCategory(text) : undefined;
    // upisan točan naziv kategorije → ta kategorija (bez teksta); inače tekst unutar trenutne kategorije
    const segs = [region, cat ?? category].filter(Boolean);
    const path = "/" + segs.join("/");
    const sp = new URLSearchParams();
    if (text && !cat) sp.set("q", text);
    const qs = sp.toString();
    router.push(qs ? `${path}?${qs}` : path);
    setFocused(false);
  }

  const showSuggest = focused && q.trim().length >= 2 && (suggestions?.length ?? 0) > 0;

  return (
    <div className="searchbar" ref={wrapRef}>
      <div className="field">
        <label htmlFor="search-q">Što tražite?</label>
        <input
          id="search-q"
          placeholder={category ? "naziv, grad, stil…" : "fotograf, dvorana, bend…"}
          value={q}
          autoComplete="off"
          onChange={(e) => setQ(e.target.value)}
          onFocus={() => setFocused(true)}
          onKeyDown={(e) => e.key === "Enter" && submit()}
        />
        {showSuggest && (
          <div className="suggest" role="listbox">
            {suggestions!.map((s, i) => (
              <button
                key={i}
                role="option"
                aria-selected={false}
                onClick={() => {
                  setFocused(false);
                  router.push(s.href);
                }}
              >
                <span>{s.label}</span>
                <span className="sub">{s.sub}</span>
              </button>
            ))}
          </div>
        )}
      </div>
      <div className="field">
        <label htmlFor="search-region">Regija</label>
        <select id="search-region" value={region} onChange={(e) => setRegion(e.target.value)}>
          <option value="">Cijela Hrvatska</option>
          {REGIONS.map((r) => (
            <option key={r.id} value={r.id}>
              {r.name}
            </option>
          ))}
        </select>
      </div>
      <div className="field">
        <label htmlFor="search-date">Datum (opcionalno)</label>
        <input
          id="search-date"
          type="date"
          value={date ?? ""}
          onChange={(e) => setDate(e.target.value || null)}
        />
      </div>
      <button className="submit" onClick={submit}>
        Traži
      </button>
    </div>
  );
}
