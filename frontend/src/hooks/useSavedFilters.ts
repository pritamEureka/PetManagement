import { useCallback, useState } from "react";
import { deleteSavedFilter, getSavedFilters, saveFilter, type SavedFilter } from "@/lib/recents";

export function useSavedFilters(scope: string) {
  const [filters, setFilters] = useState<SavedFilter[]>(() => getSavedFilters(scope));
  const save = useCallback((name: string, params: URLSearchParams | string) => setFilters(saveFilter(scope, name, params)), [scope]);
  const remove = useCallback((id: string) => setFilters(deleteSavedFilter(scope, id)), [scope]);
  return { filters, save, remove };
}
