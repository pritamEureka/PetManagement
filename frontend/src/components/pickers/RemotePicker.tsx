import { useEffect, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { Combobox } from "@/components/ui/combobox";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";

export interface PickerBaseProps<T> {
  /** Selected entity id ("" / null when empty). */
  value: string | null | undefined;
  /** Receives the id (or "" when cleared) and the full item when known. */
  onChange: (id: string, item?: T) => void;
  placeholder?: string;
  disabled?: boolean;
  clearable?: boolean;
  id?: string;
  "aria-label"?: string;
  "aria-invalid"?: boolean;
  "aria-describedby"?: string;
  className?: string;
}

interface Props<T> extends PickerBaseProps<T> {
  queryKey: string;
  /** Fetch candidates for the (debounced) text. */
  fetcher: (q: string) => Promise<T[]>;
  getKey: (item: T) => string;
  getLabel: (item: T) => string;
  renderItem?: (item: T) => ReactNode;
  minChars?: number;
  debounceMs?: number;
  /** When true the full list is loaded once and filtered locally by label. */
  local?: boolean;
  /** Optional post-filter / grouping on loaded items. */
  filter?: (item: T) => boolean;
  searchPlaceholder?: string;
  emptyText?: string;
}

/** Generic id picker: debounced remote (or local) search on top of the accessible Combobox. */
export function RemotePicker<T>(p: Props<T>) {
  const { value, onChange, fetcher, getKey, getLabel, queryKey, minChars = 0, debounceMs = 300, local, filter } = p;
  const [q, setQ] = useState("");
  const debounced = useDebouncedValue(q, debounceMs);
  const enabled = local || debounced.trim().length >= minChars;
  const query = useQuery({
    queryKey: ["picker", queryKey, local ? "all" : debounced.trim()],
    queryFn: () => fetcher(local ? "" : debounced.trim()),
    enabled,
    staleTime: 30_000,
  });
  const [selected, setSelected] = useState<T | null>(null);

  const items = (query.data ?? []).filter((i) => (filter ? filter(i) : true));

  // Resolve the label for an externally supplied id from loaded data.
  useEffect(() => {
    if (!value) {
      setSelected(null);
      return;
    }
    if (selected && getKey(selected) === value) return;
    const found = items.find((i) => getKey(i) === value);
    if (found) setSelected(found);
  }, [value, query.data]); // eslint-disable-line react-hooks/exhaustive-deps

  const display: T | null =
    value && selected && getKey(selected) === value
      ? selected
      : value
        ? ({ __id: value } as unknown as T)
        : null;

  return (
    <Combobox<T>
      value={display}
      onChange={(item) => {
        setSelected(item);
        onChange(item ? getKey(item) : "", item ?? undefined);
      }}
      items={items}
      getKey={(i) => ((i as any).__id as string | undefined) ?? getKey(i)}
      getLabel={(i) => ((i as any).__id ? `${String((i as any).__id).slice(0, 8)}…` : getLabel(i))}
      renderItem={p.renderItem}
      search={local ? undefined : q}
      onSearchChange={local ? undefined : setQ}
      loading={enabled && query.isFetching && !query.data}
      error={query.isError}
      onRetry={() => void query.refetch()}
      minChars={local ? 0 : minChars}
      placeholder={p.placeholder}
      searchPlaceholder={p.searchPlaceholder}
      emptyText={p.emptyText}
      clearable={p.clearable}
      disabled={p.disabled}
      id={p.id}
      aria-label={p["aria-label"]}
      aria-invalid={p["aria-invalid"]}
      aria-describedby={p["aria-describedby"]}
      className={p.className}
    />
  );
}
