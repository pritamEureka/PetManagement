import { useMemo, useState } from "react";
import { Check, ChevronsUpDown } from "lucide-react";
import { Country, State, City } from "country-state-city";
import { Button } from "@/components/ui/button";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { Command, CommandInput, CommandList, CommandEmpty, CommandGroup, CommandItem } from "@/components/ui/command";
import { cn } from "@/lib/utils";

/**
 * Cascading searchable picker for country / state / city. Backed by the
 * country-state-city dataset (offline, no network calls). Each combobox is a
 * cmdk Command inside a Radix Popover — opens on click, supports keyboard
 * navigation, and filters as you type.
 *
 * The component is **uncontrolled at the picker level but controlled by name**
 * — callers pass current `country` / `state` / `city` string values and an
 * `onChange` that receives the new triple whenever any field changes. Selecting
 * a new country resets the dependent state + city to keep them in sync with
 * the parent jurisdiction.
 *
 * For datasets the size of "all cities of India" (~14k), we cap visible items
 * at 250 — the user can still type to filter the whole set, but the DOM never
 * renders more than the cap.
 */

const MAX_VISIBLE = 250;

export interface CountryCityValue {
  country: string;
  state: string;
  city: string;
}

interface Props {
  value: CountryCityValue;
  onChange: (v: CountryCityValue) => void;
  showState?: boolean;
  required?: boolean;
  className?: string;
}

export function CountryCityPicker({ value, onChange, showState = true, className }: Props) {
  const countries = useMemo(() => Country.getAllCountries(), []);
  const selectedCountry = useMemo(
    () => countries.find((c) => c.name === value.country),
    [countries, value.country]
  );

  const states = useMemo(
    () => (selectedCountry ? State.getStatesOfCountry(selectedCountry.isoCode) : []),
    [selectedCountry]
  );
  const selectedState = useMemo(
    () => states.find((s) => s.name === value.state),
    [states, value.state]
  );

  const cities = useMemo(() => {
    if (!selectedCountry) return [];
    if (selectedState) return City.getCitiesOfState(selectedCountry.isoCode, selectedState.isoCode);
    return City.getCitiesOfCountry(selectedCountry.isoCode) ?? [];
  }, [selectedCountry, selectedState]);

  return (
    <div className={cn("grid gap-3", showState ? "sm:grid-cols-3" : "sm:grid-cols-2", className)}>
      <Combo
        label="Country"
        placeholder="Choose country…"
        empty="No country matches."
        items={countries.map((c) => ({ value: c.name, label: c.name, hint: c.isoCode }))}
        selected={value.country}
        onSelect={(country) =>
          // Reset state + city — they belong to the previous country.
          onChange({ country, state: "", city: "" })
        }
      />
      {showState && (
        <Combo
          label="State / region"
          placeholder={selectedCountry ? "Choose state…" : "Pick a country first"}
          empty="No state matches."
          items={states.map((s) => ({ value: s.name, label: s.name, hint: s.isoCode }))}
          selected={value.state}
          disabled={!selectedCountry || states.length === 0}
          onSelect={(state) => onChange({ ...value, state, city: "" })}
        />
      )}
      <Combo
        label="City"
        placeholder={selectedCountry ? "Choose city…" : "Pick a country first"}
        empty={selectedCountry ? "No city matches." : "Pick a country first."}
        items={cities.map((c) => ({ value: c.name, label: c.name }))}
        selected={value.city}
        disabled={!selectedCountry}
        onSelect={(city) => onChange({ ...value, city })}
      />
    </div>
  );
}

interface ComboItem { value: string; label: string; hint?: string }

interface ComboProps {
  label: string;
  placeholder: string;
  empty: string;
  items: ComboItem[];
  selected: string;
  disabled?: boolean;
  onSelect: (v: string) => void;
}

function Combo({ label, placeholder, empty, items, selected, disabled, onSelect }: ComboProps) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");

  const visible = useMemo(() => {
    if (!query.trim()) return items.slice(0, MAX_VISIBLE);
    const q = query.toLowerCase();
    const filtered = items.filter((it) => it.label.toLowerCase().includes(q));
    return filtered.slice(0, MAX_VISIBLE);
  }, [items, query]);

  return (
    <div className="space-y-1.5">
      <label className="text-sm font-medium">{label}</label>
      <Popover open={open} onOpenChange={(o) => { setOpen(o); if (!o) setQuery(""); }}>
        <PopoverTrigger asChild>
          <Button
            type="button"
            variant="outline"
            role="combobox"
            aria-expanded={open}
            disabled={disabled}
            className="w-full justify-between font-normal"
          >
            <span className={cn("truncate", !selected && "text-muted-foreground")}>
              {selected || placeholder}
            </span>
            <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
          </Button>
        </PopoverTrigger>
        <PopoverContent className="w-[var(--radix-popover-trigger-width)] p-0" align="start">
          <Command shouldFilter={false}>
            <CommandInput placeholder={`Search ${label.toLowerCase()}…`} value={query} onValueChange={setQuery} />
            <CommandList>
              <CommandEmpty>{empty}</CommandEmpty>
              <CommandGroup>
                {visible.map((it) => (
                  <CommandItem
                    key={`${it.value}-${it.hint ?? ""}`}
                    value={it.value}
                    onSelect={(v) => { onSelect(v); setOpen(false); setQuery(""); }}
                  >
                    <Check className={cn("mr-2 h-4 w-4", selected === it.value ? "opacity-100" : "opacity-0")} />
                    <span className="flex-1 truncate">{it.label}</span>
                    {it.hint && <span className="ml-2 text-xs text-muted-foreground">{it.hint}</span>}
                  </CommandItem>
                ))}
                {!query && items.length > MAX_VISIBLE && (
                  <p className="px-2 py-1 text-xs text-muted-foreground">
                    Showing first {MAX_VISIBLE} of {items.length}. Type to search the rest.
                  </p>
                )}
              </CommandGroup>
            </CommandList>
          </Command>
        </PopoverContent>
      </Popover>
    </div>
  );
}
