import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { ChevronLeft, ChevronRight, Video, Building2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { vetsApi, type TimeSlot } from "@/api/vets";

interface Props {
  doctorId: string;
  selectedSlotId?: string | null;
  consultationType?: "Online" | "Offline";
  onSelect: (slot: TimeSlot) => void;
  daysToShow?: number;
}

/**
 * 7-day horizontal date picker + slot grid for the selected day. Slots are
 * fetched once for the 7-day window and grouped by date. The user navigates
 * forward/back in 7-day windows; selecting a date filters in-memory.
 */
export function SlotPicker({ doctorId, selectedSlotId, consultationType, onSelect, daysToShow = 7 }: Props) {
  const [windowStart, setWindowStart] = useState<Date>(() => {
    const d = new Date(); d.setHours(0, 0, 0, 0); return d;
  });

  const windowEnd = useMemo(() => {
    const d = new Date(windowStart); d.setDate(d.getDate() + daysToShow - 1); return d;
  }, [windowStart, daysToShow]);

  const fromStr = toYmd(windowStart);
  const toStr   = toYmd(windowEnd);

  const { data: slots, isLoading } = useQuery({
    queryKey: ["vet-slots", doctorId, fromStr, toStr, consultationType],
    queryFn: () => vetsApi.slots(doctorId, fromStr, toStr, consultationType)
  });

  const [activeDate, setActiveDate] = useState<string>(fromStr);

  const byDate = useMemo(() => {
    const map: Record<string, TimeSlot[]> = {};
    for (const s of slots ?? []) {
      const k = s.startUtc.substring(0, 10);
      (map[k] ??= []).push(s);
    }
    return map;
  }, [slots]);

  const days = useMemo(() =>
    Array.from({ length: daysToShow }, (_, i) => {
      const d = new Date(windowStart); d.setDate(d.getDate() + i);
      const ymd = toYmd(d);
      return { ymd, date: d, count: byDate[ymd]?.length ?? 0 };
    }), [windowStart, daysToShow, byDate]);

  const todaySlots = byDate[activeDate] ?? [];

  return (
    <div className="space-y-3">
      {/* Date navigator */}
      <div className="flex items-center gap-2">
        <Button variant="ghost" size="icon" onClick={() => setWindowStart((d) => { const c = new Date(d); c.setDate(c.getDate() - daysToShow); return c; })}>
          <ChevronLeft className="h-4 w-4" />
        </Button>
        <div className="flex-1 grid grid-cols-7 gap-1">
          {days.map((d) => {
            const active = d.ymd === activeDate;
            return (
              <button key={d.ymd}
                onClick={() => setActiveDate(d.ymd)}
                disabled={d.count === 0}
                className={`rounded-md border text-center py-2 ${active ? "border-primary bg-primary/10" : "hover:bg-accent"} ${d.count === 0 ? "opacity-50 cursor-not-allowed" : ""}`}
              >
                <p className="text-[10px] uppercase text-muted-foreground">{d.date.toLocaleDateString(undefined, { weekday: "short" })}</p>
                <p className="text-sm font-semibold">{d.date.getDate()}</p>
                <p className="text-[10px] text-muted-foreground">{d.count} slot{d.count === 1 ? "" : "s"}</p>
              </button>
            );
          })}
        </div>
        <Button variant="ghost" size="icon" onClick={() => setWindowStart((d) => { const c = new Date(d); c.setDate(c.getDate() + daysToShow); return c; })}>
          <ChevronRight className="h-4 w-4" />
        </Button>
      </div>

      {/* Slots */}
      {isLoading ? (
        <div className="grid grid-cols-3 sm:grid-cols-4 gap-2">{[...Array(8)].map((_, i) => <Skeleton key={i} className="h-9 rounded-md" />)}</div>
      ) : todaySlots.length === 0 ? (
        <p className="text-sm text-muted-foreground text-center py-6">No slots for this day.</p>
      ) : (
        <div className="grid grid-cols-3 sm:grid-cols-4 gap-2">
          {todaySlots.map((s) => {
            const selected = s.id === selectedSlotId;
            const start = new Date(s.startUtc);
            const Icon = s.consultationType === "Online" ? Video : Building2;
            return (
              <button key={s.id}
                onClick={() => onSelect(s)}
                className={`rounded-md border px-2 py-2 text-sm flex items-center justify-center gap-1.5 ${selected ? "border-primary bg-primary text-primary-foreground" : "hover:bg-accent"}`}
              >
                <Icon className="h-3 w-3" />
                {start.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}
              </button>
            );
          })}
        </div>
      )}

      {/* Legend */}
      <div className="flex justify-end gap-3 text-xs text-muted-foreground">
        <span className="flex items-center gap-1"><Video className="h-3 w-3" /> Online</span>
        <span className="flex items-center gap-1"><Building2 className="h-3 w-3" /> In clinic</span>
      </div>
    </div>
  );
}

function toYmd(d: Date) {
  const y = d.getFullYear(), m = String(d.getMonth() + 1).padStart(2, "0"), day = String(d.getDate()).padStart(2, "0");
  return `${y}-${m}-${day}`;
}
