import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Bell, BellOff, BookmarkPlus, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { toast } from "@/components/ui/sonner";
import { discoveryApi, type FacetSearchRequest } from "@/api/shopPlus";
import { useAuthStore } from "@/store/authStore";

interface Props {
  /** The currently applied filters (what "Save" stores). */
  current: FacetSearchRequest;
  onApply: (query: FacetSearchRequest) => void;
}

export function SavedSearches({ current, onApply }: Props) {
  const qc = useQueryClient();
  const signedIn = useAuthStore((s) => !!s.user?.id);
  const [name, setName] = useState("");

  const { data } = useQuery({
    queryKey: ["saved-searches"], queryFn: () => discoveryApi.savedSearches(), enabled: signedIn,
  });
  const refresh = () => qc.invalidateQueries({ queryKey: ["saved-searches"] });
  const onError = (e: any) => toast.error(e?.response?.data?.error?.message ?? "Request failed.");

  const save = useMutation({
    mutationFn: () => discoveryApi.createSavedSearch(
      name.trim(), { ...current, page: undefined, lat: undefined, lng: undefined }, true),
    onSuccess: () => { setName(""); toast.success("Search saved with alerts on"); refresh(); },
    onError,
  });
  const toggle = useMutation({
    mutationFn: (s: { id: string; alertEnabled: boolean }) =>
      discoveryApi.updateSavedSearch(s.id, { alertEnabled: s.alertEnabled }),
    onSuccess: refresh, onError,
  });
  const remove = useMutation({ mutationFn: (id: string) => discoveryApi.deleteSavedSearch(id), onSuccess: refresh, onError });

  if (!signedIn) return null;

  return (
    <div className="space-y-2">
      <div className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Saved searches</div>
      <div className="flex gap-2">
        <Input placeholder="Name this search" value={name} onChange={(e) => setName(e.target.value)} />
        <Button size="icon" variant="outline" disabled={!name.trim() || save.isPending}
                onClick={() => save.mutate()} title="Save current search">
          <BookmarkPlus className="h-4 w-4" />
        </Button>
      </div>
      {(data ?? []).map((s) => (
        <div key={s.id} className="flex items-center justify-between gap-1 text-sm">
          <button type="button" className="truncate text-left hover:underline" onClick={() => onApply(s.query)}>
            {s.name}
          </button>
          <div className="flex shrink-0">
            <Button size="icon" variant="ghost" className="h-7 w-7"
                    title={s.alertEnabled ? "Alerts on" : "Alerts off"}
                    onClick={() => toggle.mutate({ id: s.id, alertEnabled: !s.alertEnabled })}>
              {s.alertEnabled ? <Bell className="h-4 w-4 text-primary" /> : <BellOff className="h-4 w-4" />}
            </Button>
            <Button size="icon" variant="ghost" className="h-7 w-7" title="Delete" onClick={() => remove.mutate(s.id)}>
              <Trash2 className="h-4 w-4" />
            </Button>
          </div>
        </div>
      ))}
    </div>
  );
}
