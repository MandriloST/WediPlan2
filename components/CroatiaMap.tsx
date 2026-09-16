"use client";

import maplibregl, { Map as MLMap, Marker, Popup, LngLatBoundsLike } from "maplibre-gl";
import { useEffect, useRef } from "react";
import { REGION_BY_ID } from "@/lib/data";
import { pinPrice, formatPrice, formatRating } from "@/lib/format";
import type { PinVendor, RegionId } from "@/lib/types";
import { useCompare } from "@/stores";
import { track } from "@/lib/analytics";
import { coverImage } from "@/lib/images";
import { spreadPositions } from "@/lib/jitter";
import { canAddToCompare, COMPARE_INCOMPATIBLE_HINT, vendorCategories } from "@/lib/categories";

const CROATIA_BOUNDS: LngLatBoundsLike = [
  [13.0, 42.2],
  [19.6, 46.7],
];

const OSM_STYLE: maplibregl.StyleSpecification = {
  version: 8,
  sources: {
    osm: {
      type: "raster",
      // OSM javni tile server je OK za demo; za produkciju postavi
      // NEXT_PUBLIC_TILE_URL (npr. MapTiler/Stadia s API ključem) — vidi DEPLOY.md
      tiles: [process.env.NEXT_PUBLIC_TILE_URL ?? "https://tile.openstreetmap.org/{z}/{x}/{y}.png"],
      tileSize: 256,
      attribution: "© OpenStreetMap contributors",
      maxzoom: 19,
    },
  },
  layers: [{ id: "osm", type: "raster", source: "osm" }],
};

interface Props {
  /** pinovi (category-first: samo odabrana kategorija); [] = karta samo s regijama */
  vendors: PinVendor[];
  selectedRegion?: RegionId;
  onRegionClick?: (id: RegionId) => void;
  className?: string;
}

function toFC(vendors: PinVendor[]): GeoJSON.FeatureCollection {
  // pin samo za pružatelje s poznatim koordinatama — region preciznost ostaje u listi;
  // pružatelji iste točke (geokodirani centroid grada) raspoređeni po spirali (lib/jitter)
  const pos = spreadPositions(vendors);
  return {
    type: "FeatureCollection",
    features: vendors
      .filter((v) => pos.has(v.slug))
      .map((v) => ({
        type: "Feature",
        geometry: { type: "Point", coordinates: pos.get(v.slug)! },
        properties: {
          id: v.id,
          slug: v.slug,
          name: v.name,
          city: v.city,
          category: v.category,
          // MapLibre ionako serijalizira nizove u properties — eksplicitni JSON
          cats: JSON.stringify(vendorCategories(v)),
          price: pinPrice(v.price),
          upit: v.price.kind === "onRequest" ? "1" : "",
          priceFull: formatPrice(v.price),
          rating: v.reviewCount > 0 ? `★ ${formatRating(v.rating)}` : "još bez ocjene",
          cover: coverImage(v).src,
          approx: v.locationPrecision === "city" ? "1" : "",
        },
      })),
  };
}

function popupContent(p: Record<string, string>): HTMLElement {
  const el = document.createElement("div");
  el.className = "popup-card";
  el.innerHTML = `
    <img class="pimg" alt="" />
    <div class="body">
      <div class="name"></div>
      <div><span class="price"></span> · <span class="r"></span></div>
      <div class="approx" hidden>lokacija približna (grad)</div>
      <label class="compare-box"><input type="checkbox" /> usporedi</label>
      <a class="more" href="#">detalji →</a>
    </div>`;
  el.querySelector(".name")!.textContent = p.name;
  (el.querySelector(".pimg") as HTMLImageElement).src = p.cover;
  (el.querySelector(".more") as HTMLAnchorElement).href = `/pruzatelj/${p.slug}`;
  el.querySelector(".price")!.textContent = p.priceFull;
  el.querySelector(".r")!.textContent = p.rating;
  if (p.approx) (el.querySelector(".approx") as HTMLElement).hidden = false;

  const candidate = {
    id: p.id,
    slug: p.slug,
    name: p.name,
    category: p.category,
    categories: (() => {
      try {
        return JSON.parse(p.cats) as string[];
      } catch {
        return undefined;
      }
    })(),
  };
  const label = el.querySelector(".compare-box") as HTMLLabelElement;
  const box = el.querySelector("input")! as HTMLInputElement;
  const refresh = () => {
    const { ids, meta } = useCompare.getState();
    const cats = Object.fromEntries(Object.entries(meta).map(([k, m]) => [k, m.cats]));
    const checked = ids.includes(p.id);
    const disabled = !checked && !canAddToCompare(candidate, ids, cats);
    box.checked = checked;
    box.disabled = disabled;
    label.classList.toggle("disabled", disabled);
    label.title = disabled ? COMPARE_INCOMPATIBLE_HINT : "";
  };
  refresh();
  box.addEventListener("change", () => {
    useCompare.getState().toggle(candidate);
    refresh();
  });
  return el;
}

export default function CroatiaMap({ vendors, selectedRegion, onRegionClick, className }: Props) {
  const container = useRef<HTMLDivElement>(null);
  const mapRef = useRef<MLMap | null>(null);
  const markers = useRef<Record<string, Marker>>({});
  const vendorsRef = useRef(vendors);
  vendorsRef.current = vendors;
  const regionCb = useRef(onRegionClick);
  regionCb.current = onRegionClick;

  /* init once */
  useEffect(() => {
    if (!container.current || mapRef.current) return;
    const map = new MLMap({
      container: container.current,
      style: OSM_STYLE,
      bounds: CROATIA_BOUNDS,
      fitBoundsOptions: { padding: 24 },
      attributionControl: { compact: true },
    });
    mapRef.current = map;
    map.addControl(new maplibregl.NavigationControl({ showCompass: false }), "top-right");

    map.on("load", () => {
      /* region polygons — click to filter (README: klik na regiju filtrira i zumira) */
      map.addSource("regions", { type: "geojson", data: "/data/croatia-regions.geojson", promoteId: "id" });
      map.addLayer({
        id: "region-fill",
        type: "fill",
        source: "regions",
        paint: {
          "fill-color": "#2470cc",
          "fill-opacity": ["case", ["boolean", ["feature-state", "hover"], false], 0.14, 0.05],
        },
      });
      map.addLayer({
        id: "region-line",
        type: "line",
        source: "regions",
        paint: { "line-color": "#2470cc", "line-opacity": 0.35, "line-width": 1.2 },
      });

      let hovered: string | number | undefined;
      map.on("mousemove", "region-fill", (e) => {
        map.getCanvas().style.cursor = "pointer";
        const id = e.features?.[0]?.id;
        if (hovered !== undefined) map.setFeatureState({ source: "regions", id: hovered }, { hover: false });
        if (id !== undefined) {
          hovered = id;
          map.setFeatureState({ source: "regions", id }, { hover: true });
        }
      });
      map.on("mouseleave", "region-fill", () => {
        map.getCanvas().style.cursor = "";
        if (hovered !== undefined) map.setFeatureState({ source: "regions", id: hovered }, { hover: false });
        hovered = undefined;
      });
      map.on("click", "region-fill", (e) => {
        const id = e.features?.[0]?.properties?.id as RegionId | undefined;
        if (id) regionCb.current?.(id);
      });

      /* vendor pins — clustered source rendered as HTML markers (price on the pin) */
      map.addSource("vendors", {
        type: "geojson",
        data: toFC(vendorsRef.current),
        cluster: true,
        clusterRadius: 46,
        clusterMaxZoom: 13, // spirala (lib/jitter) se razmotava tek na z14+
      });
      // invisible layer so querySourceFeatures works after tiles load
      map.addLayer({ id: "vendors-dot", type: "circle", source: "vendors", paint: { "circle-radius": 0, "circle-opacity": 0 } });

      const sync = () => {
        if (!map.getSource("vendors") || !map.isSourceLoaded("vendors")) return;
        const feats = map.querySourceFeatures("vendors");
        const keep = new Set<string>();

        for (const f of feats) {
          const coords = (f.geometry as GeoJSON.Point).coordinates as [number, number];
          const props = f.properties as Record<string, any>;
          const key = props.cluster ? `c${props.cluster_id}` : `v${props.id}`;
          if (keep.has(key)) continue;
          keep.add(key);
          if (markers.current[key]) continue;

          const el = document.createElement("button");
          if (props.cluster) {
            el.className = "pin-cluster";
            el.textContent = String(props.point_count);
            el.setAttribute("aria-label", `${props.point_count} pružatelja — približi`);
            el.addEventListener("click", (ev) => {
              ev.stopPropagation();
              const src = map.getSource("vendors") as maplibregl.GeoJSONSource;
              src.getClusterExpansionZoom(props.cluster_id).then((zoom) => {
                map.easeTo({ center: coords, zoom: zoom + 0.3 });
              });
            });
          } else {
            el.className = props.upit ? "pin upit" : "pin"; // "na upit" prigušen (§10.4)
            el.textContent = props.price;
            el.setAttribute("aria-label", `${props.name}, ${props.priceFull}`);
            el.addEventListener("click", (ev) => {
              ev.stopPropagation();
              track("map_pin_clicked", { slug: props.slug, category: props.category });
              new Popup({ offset: 14, closeButton: false, maxWidth: "240px" })
                .setLngLat(coords)
                .setDOMContent(popupContent(props))
                .addTo(map);
            });
          }
          markers.current[key] = new Marker({ element: el }).setLngLat(coords).addTo(map);
        }
        for (const key of Object.keys(markers.current)) {
          if (!keep.has(key)) {
            markers.current[key].remove();
            delete markers.current[key];
          }
        }
      };
      map.on("render", sync);
      map.on("moveend", sync);
    });

    return () => {
      map.remove();
      mapRef.current = null;
      markers.current = {};
    };
  }, []);

  /* update pins when the filtered vendor set changes */
  useEffect(() => {
    const map = mapRef.current;
    const src = map?.getSource("vendors") as maplibregl.GeoJSONSource | undefined;
    if (!src) return;
    Object.values(markers.current).forEach((m) => m.remove());
    markers.current = {};
    src.setData(toFC(vendors) as any);
  }, [vendors]);

  /* zoom to region when it changes (list click ⇄ map are synchronized) */
  useEffect(() => {
    const map = mapRef.current;
    if (!map) return;
    if (selectedRegion) {
      map.fitBounds(REGION_BY_ID[selectedRegion].bounds, { padding: 30, duration: 700 });
    } else {
      map.fitBounds(CROATIA_BOUNDS, { padding: 24, duration: 700 });
    }
  }, [selectedRegion]);

  return <div ref={container} className={className} style={{ position: "absolute", inset: 0 }} />;
}
