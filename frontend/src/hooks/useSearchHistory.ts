import { useCallback, useState } from "react";
import { clearSearchHistory, getSearchHistory, pushSearch, removeSearch } from "@/lib/recents";

export function useSearchHistory(scope = "global") {
  const [history, setHistory] = useState<string[]>(() => getSearchHistory(scope));
  const add = useCallback((term: string) => setHistory(pushSearch(term, scope)), [scope]);
  const remove = useCallback((term: string) => setHistory(removeSearch(term, scope)), [scope]);
  const clear = useCallback(() => {
    clearSearchHistory(scope);
    setHistory([]);
  }, [scope]);
  const refresh = useCallback(() => setHistory(getSearchHistory(scope)), [scope]);
  return { history, add, remove, clear, refresh };
}
