import { useEffect, useRef, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Search, Sparkles } from "lucide-react";
import { Input } from "@/components/ui/input";
import { discoveryApi } from "@/api/shopPlus";

interface Props {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
}

/** Search input with debounced, typo-tolerant suggestions (prefix + fuzzy "did you mean"). */
export function SuggestBox({ value, onChange, placeholder = "Search products..." }: Props) {
  const [open, setOpen] = useState(false);
  const [debounced, setDebounced] = useState(value);
  const wrapRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const t = setTimeout(() => setDebounced(value.trim()), 200);
    return () => clearTimeout(t);
  }, [value]);

  useEffect(() => {
    const onDoc = (e: MouseEvent) => {
      if (wrapRef.current && !wrapRef.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener("mousedown", onDoc);
    return () => document.removeEventListener("mousedown", onDoc);
  }, []);

  const { data } = useQuery({
    queryKey: ["search-suggest", debounced],
    queryFn: () => discoveryApi.suggest(debounced),
    enabled: debounced.length >= 2,
    staleTime: 30_000,
  });

  const show = open && debounced.length >= 2 && !!data && data.length > 0;

  return (
    <div ref={wrapRef} className="relative flex-1 min-w-[10rem]">
      <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
      <Input
        className="pl-8"
        placeholder={placeholder}
        value={value}
        onFocus={() => setOpen(true)}
        onChange={(e) => { onChange(e.target.value); setOpen(true); }}
        onKeyDown={(e) => { if (e.key === "Escape" || e.key === "Enter") setOpen(false); }}
      />
      {show && (
        <ul className="absolute z-30 mt-1 w-full rounded-md border bg-popover shadow-md overflow-hidden">
          {data!.map((s) => (
            <li key={`${s.kind}:${s.text}`}>
              <button
                type="button"
                className="flex w-full items-center gap-2 px-3 py-2 text-left text-sm hover:bg-muted"
                onClick={() => { onChange(s.text); setOpen(false); }}
              >
                {s.kind === "correction"
                  ? <><Sparkles className="h-3.5 w-3.5 text-primary" /> Did you mean <b>{s.text}</b>?</>
                  : <>{s.text}</>}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
