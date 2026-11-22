import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { vetsApi, type DoctorSummary } from "@/api/vets";

/**
 * Searchable picker for approved doctors (no raw ids). The vets search endpoint has no free-text parameter, so the
 * approved-doctor list is loaded once (up to 4 pages x 50, rating order) and filtered client-side by name/clinic/city/specialty.
 */
export function DoctorPicker({ value, onChange }: { value: DoctorSummary | null; onChange: (d: DoctorSummary | null) => void }) {
  const [q, setQ] = useState("");
  const term = useDebouncedValue(q.trim().toLowerCase(), 250);
  const { data: all = [], isLoading } = useQuery({
    queryKey: ["vets", "picker-list"],
    staleTime: 5 * 60_000,
    queryFn: async () => {
      const out: DoctorSummary[] = [];
      let cursor: string | undefined;
      for (let i = 0; i < 4; i++) {
        const page = await vetsApi.search({ pageSize: 50, cursor });
        out.push(...page.items);
        if (!page.nextCursor) break;
        cursor = page.nextCursor;
      }
      return out;
    },
  });

  if (value) {
    return (
      <div className="flex items-center justify-between gap-2 rounded-md border px-3 py-2 text-sm">
        <span><span className="font-medium">{value.name}</span>{value.clinicName ? ` · ${value.clinicName}` : ""}{value.city ? ` · ${value.city}` : ""}</span>
        <Button type="button" size="sm" variant="ghost" onClick={() => onChange(null)}>Change</Button>
      </div>
    );
  }
  const matches = (term
    ? all.filter((d) => [d.name, d.clinicName, d.city, d.primarySpecialty].some((x) => x?.toLowerCase().includes(term)))
    : all).slice(0, 8);

  return (
    <div className="space-y-1">
      <Input aria-label="Search doctors" placeholder="Search by doctor, clinic or city…" value={q} onChange={(e) => setQ(e.target.value)} />
      <ul className="rounded-md border divide-y max-h-48 overflow-y-auto text-sm" role="listbox" aria-label="Doctors">
        {isLoading && <li className="px-3 py-2 text-muted-foreground">Loading doctors…</li>}
        {!isLoading && matches.length === 0 && <li className="px-3 py-2 text-muted-foreground">No doctors found.</li>}
        {matches.map((d) => (
          <li key={d.id}>
            <button type="button" role="option" aria-selected={false} className="w-full text-left px-3 py-2 hover:bg-muted" onClick={() => onChange(d)}>
              <span className="font-medium">{d.name}</span>
              <span className="text-xs text-muted-foreground block">{[d.primarySpecialty, d.clinicName, d.city].filter(Boolean).join(" · ")}</span>
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}
