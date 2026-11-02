import { useCallback, useEffect, useState } from "react";

export interface Coords { lat: number; lng: number; }
export type GeoStatus = "idle" | "locating" | "ready" | "denied" | "unavailable";

const CACHE_KEY = "pawzaroo.lastCoords";

// Session cache so returning to a page doesn't flash "locating" again. Storage
// can throw (private mode, blocked site data) — always fall back to no cache.
function readCache(): Coords | null {
  try {
    const raw = sessionStorage.getItem(CACHE_KEY);
    const v = raw ? JSON.parse(raw) : null;
    return v && typeof v.lat === "number" && typeof v.lng === "number" ? v : null;
  } catch { return null; }
}
function writeCache(c: Coords) {
  try { sessionStorage.setItem(CACHE_KEY, JSON.stringify(c)); } catch { /* ignore */ }
}

/**
 * Browser geolocation with a small state machine. `auto` asks on mount; the
 * browser remembers the user's allow/deny choice, so this only prompts once.
 */
export function useGeolocation({ auto = false }: { auto?: boolean } = {}) {
  const cached = readCache();
  const [coords, setCoords] = useState<Coords | null>(cached);
  const [status, setStatus] = useState<GeoStatus>(cached ? "ready" : "idle");

  const request = useCallback(() => {
    if (typeof navigator === "undefined" || !navigator.geolocation) {
      setStatus("unavailable");
      return;
    }
    setStatus((s) => (s === "ready" ? s : "locating"));
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        const c = { lat: pos.coords.latitude, lng: pos.coords.longitude };
        setCoords(c);
        setStatus("ready");
        writeCache(c);
      },
      (err) => setStatus(err.code === err.PERMISSION_DENIED ? "denied" : "unavailable"),
      { enableHighAccuracy: false, timeout: 10_000, maximumAge: 10 * 60_000 }
    );
  }, []);

  useEffect(() => { if (auto) request(); }, [auto, request]);

  const clear = useCallback(() => { setCoords(null); setStatus("idle"); }, []);

  return { coords, status, request, clear };
}

/** "850 m away" / "3.2 km away" / "120 km away". */
export function formatDistance(km?: number | null) {
  if (km == null) return null;
  if (km < 1) return `${Math.max(100, Math.round(km * 10) * 100)} m away`;
  if (km < 10) return `${km.toFixed(1)} km away`;
  return `${Math.round(km)} km away`;
}

/** Great-circle distance in km (same formula the API uses for sorting). */
export function haversineKm(lat1: number, lng1: number, lat2: number, lng2: number) {
  const rad = (d: number) => (d * Math.PI) / 180;
  const a = Math.sin((rad(lat2) - rad(lat1)) / 2) ** 2
    + Math.cos(rad(lat1)) * Math.cos(rad(lat2)) * Math.sin((rad(lng2) - rad(lng1)) / 2) ** 2;
  return 2 * 6371 * Math.asin(Math.sqrt(Math.min(1, a)));
}
