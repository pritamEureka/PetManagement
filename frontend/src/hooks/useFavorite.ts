import { useCallback, useState } from "react";
import { isFavorite, toggleFavorite } from "@/lib/recents";

export function useFavorite(kind: string, id: string, meta?: { title?: string; to?: string }) {
  const [favorite, setFav] = useState(() => isFavorite(kind, id));
  const title = meta?.title;
  const to = meta?.to;
  const toggle = useCallback(() => setFav(toggleFavorite(kind, { id, title, to })), [kind, id, title, to]);
  return { favorite, toggle };
}
