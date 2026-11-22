import { currentUserScope, safeRead, safeWrite, scopedKey } from "@/lib/safeStorage";

export interface RecentItem {
  /** Stable id, typically `${kind}:${entityId}`. */
  id: string;
  kind: string;
  title: string;
  to: string;
  subtitle?: string;
  visitedAt: number;
}

export const CAP_RECENTS = 12;
export const CAP_SEARCH = 10;
export const CAP_FAVORITES = 100;
export const CAP_FILTERS = 20;

function listKey(ns: string, scope = "default") {
  return scopedKey(ns, scope, currentUserScope());
}

// ---- Recent items ----
export function getRecents(): RecentItem[] {
  return safeRead<RecentItem[]>(listKey("recents"), []);
}
export function pushRecent(item: Omit<RecentItem, "visitedAt">): RecentItem[] {
  const next = [{ ...item, visitedAt: Date.now() }, ...getRecents().filter((r) => r.id !== item.id)].slice(0, CAP_RECENTS);
  safeWrite(listKey("recents"), next);
  return next;
}
export function clearRecents(): void {
  safeWrite(listKey("recents"), []);
}

// ---- Search history (per scope, e.g. "global" or "store") ----
export function getSearchHistory(scope = "global"): string[] {
  return safeRead<string[]>(listKey("search", scope), []);
}
export function pushSearch(term: string, scope = "global"): string[] {
  const q = term.trim();
  if (q.length < 2) return getSearchHistory(scope);
  const next = [q, ...getSearchHistory(scope).filter((s) => s.toLowerCase() !== q.toLowerCase())].slice(0, CAP_SEARCH);
  safeWrite(listKey("search", scope), next);
  return next;
}
export function removeSearch(term: string, scope = "global"): string[] {
  const next = getSearchHistory(scope).filter((s) => s !== term);
  safeWrite(listKey("search", scope), next);
  return next;
}
export function clearSearchHistory(scope = "global"): void {
  safeWrite(listKey("search", scope), []);
}

// ---- Favorites (kind-scoped set of ids) ----
export interface FavoriteEntry { id: string; title?: string; to?: string; addedAt: number }
export function getFavorites(kind: string): FavoriteEntry[] {
  return safeRead<FavoriteEntry[]>(listKey("fav", kind), []);
}
export function isFavorite(kind: string, id: string): boolean {
  return getFavorites(kind).some((f) => f.id === id);
}
/** Toggles and returns the new state. */
export function toggleFavorite(kind: string, entry: Omit<FavoriteEntry, "addedAt">): boolean {
  const list = getFavorites(kind);
  const exists = list.some((f) => f.id === entry.id);
  const next = exists
    ? list.filter((f) => f.id !== entry.id)
    : [{ ...entry, addedAt: Date.now() }, ...list].slice(0, CAP_FAVORITES);
  safeWrite(listKey("fav", kind), next);
  return !exists;
}

// ---- Saved filters ----
export interface SavedFilter { id: string; name: string; /** URLSearchParams serialization */ query: string; createdAt: number }
export function getSavedFilters(scope: string): SavedFilter[] {
  return safeRead<SavedFilter[]>(listKey("filters", scope), []);
}
export function saveFilter(scope: string, name: string, params: URLSearchParams | string): SavedFilter[] {
  const query = typeof params === "string" ? params : params.toString();
  const entry: SavedFilter = {
    id: `${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`,
    name: name.trim().slice(0, 60),
    query,
    createdAt: Date.now(),
  };
  const next = [entry, ...getSavedFilters(scope).filter((f) => f.name !== entry.name)].slice(0, CAP_FILTERS);
  safeWrite(listKey("filters", scope), next);
  return next;
}
export function deleteSavedFilter(scope: string, id: string): SavedFilter[] {
  const next = getSavedFilters(scope).filter((f) => f.id !== id);
  safeWrite(listKey("filters", scope), next);
  return next;
}
