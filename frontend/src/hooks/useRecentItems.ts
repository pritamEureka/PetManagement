import { useCallback, useEffect, useState } from "react";
import { clearRecents, getRecents, pushRecent, type RecentItem } from "@/lib/recents";

const EVT = "pawzaroo:recents";

/** Recently visited entities (user-scoped, capped at 12). `track` is safe to call in effects. */
export function useRecentItems() {
  const [items, setItems] = useState<RecentItem[]>(() => getRecents());
  useEffect(() => {
    const sync = () => setItems(getRecents());
    window.addEventListener(EVT, sync);
    window.addEventListener("storage", sync);
    return () => {
      window.removeEventListener(EVT, sync);
      window.removeEventListener("storage", sync);
    };
  }, []);
  const track = useCallback((item: Omit<RecentItem, "visitedAt">) => {
    pushRecent(item);
    window.dispatchEvent(new Event(EVT));
  }, []);
  const clear = useCallback(() => {
    clearRecents();
    window.dispatchEvent(new Event(EVT));
  }, []);
  return { items, track, clear };
}
