import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { CalendarClock, Plus, Trash2, Sparkles, CalendarOff } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Separator } from "@/components/ui/separator";
import { PageHeader } from "@/components/common/PageHeader";
import { availabilityRuleSchema, type AvailabilityRuleInput, consultationTypes } from "@/lib/schemas";
import { vetsApi } from "@/api/vets";
import { toast } from "@/components/ui/sonner";

const DAYS = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

export function AvailabilityManagementPage() {
  const qc = useQueryClient();
  const { data: rules, isLoading: rulesLoading } = useQuery({ queryKey: ["vet-rules"], queryFn: vetsApi.rules });
  const { data: holidays } = useQuery({ queryKey: ["vet-holidays"], queryFn: vetsApi.holidays });

  const { register, handleSubmit, watch, setValue, reset, formState: { errors, isSubmitting } } =
    useForm<AvailabilityRuleInput>({
      resolver: zodResolver(availabilityRuleSchema),
      defaultValues: { dayOfWeek: 1, startTime: "09:00", endTime: "17:00", slotMinutes: 30, consultationType: "Both" }
    });

  async function addRule(v: AvailabilityRuleInput) {
    try {
      await vetsApi.addRule(v);
      toast.success("Rule added.");
      reset({ dayOfWeek: 1, startTime: "09:00", endTime: "17:00", slotMinutes: 30, consultationType: "Both" });
      qc.invalidateQueries({ queryKey: ["vet-rules"] });
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Failed to add.");
    }
  }
  async function removeRule(id: string) {
    if (!confirm("Remove this availability rule?")) return;
    await vetsApi.removeRule(id);
    qc.invalidateQueries({ queryKey: ["vet-rules"] });
  }

  const [holidayDate, setHolidayDate] = useState("");
  const [holidayReason, setHolidayReason] = useState("");
  async function addHoliday() {
    if (!holidayDate) return;
    await vetsApi.addHoliday(holidayDate, holidayReason || undefined);
    toast.success("Holiday added.");
    setHolidayDate(""); setHolidayReason("");
    qc.invalidateQueries({ queryKey: ["vet-holidays"] });
  }
  async function removeHoliday(id: string) {
    await vetsApi.removeHoliday(id);
    qc.invalidateQueries({ queryKey: ["vet-holidays"] });
  }

  const [genFrom, setGenFrom] = useState(() => new Date().toISOString().slice(0, 10));
  const [genTo, setGenTo]     = useState(() => { const d = new Date(); d.setDate(d.getDate() + 14); return d.toISOString().slice(0, 10); });
  async function generate() {
    try {
      const res = await vetsApi.generateSlots(genFrom, genTo);
      toast.success(`Generated ${res.generated} slot(s).`);
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Failed.");
    }
  }

  return (
    <div className="max-w-4xl mx-auto space-y-4">
      <PageHeader title="Availability" icon={CalendarClock}
        description="Define weekly office hours, mark holidays, then generate concrete bookable slots." />

      <Card>
        <CardContent className="pt-6 space-y-4">
          <h3 className="text-sm font-semibold">Weekly rules</h3>
          <form onSubmit={handleSubmit(addRule)} className="grid grid-cols-2 md:grid-cols-5 gap-3 items-end">
            <div>
              <Label>Day</Label>
              <Select value={String(watch("dayOfWeek"))} onValueChange={(v) => setValue("dayOfWeek", Number(v))}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>{DAYS.map((d, i) => <SelectItem key={d} value={String(i)}>{d}</SelectItem>)}</SelectContent>
              </Select>
            </div>
            <div><Label>Start</Label><Input type="time" {...register("startTime")} /></div>
            <div><Label>End</Label><Input type="time" {...register("endTime")} /></div>
            <div><Label>Slot (min)</Label><Input type="number" {...register("slotMinutes")} /></div>
            <div>
              <Label>Type</Label>
              <Select value={watch("consultationType")} onValueChange={(v) => setValue("consultationType", v as any)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>{consultationTypes.map((t) => <SelectItem key={t} value={t}>{t}</SelectItem>)}</SelectContent>
              </Select>
            </div>
            <div className="col-span-2 md:col-span-5 flex justify-end">
              <Button type="submit" disabled={isSubmitting}><Plus className="h-4 w-4 mr-1" /> Add rule</Button>
            </div>
          </form>
          {errors.startTime && <p className="text-xs text-destructive">Start/end must be HH:mm.</p>}

          <Separator />

          {rulesLoading ? (
            <p className="text-sm text-muted-foreground">Loading...</p>
          ) : !rules || rules.length === 0 ? (
            <p className="text-sm text-muted-foreground">No rules yet — add at least one so we can generate slots.</p>
          ) : (
            <div className="space-y-1.5">
              {rules.map((r) => (
                <div key={r.id} className="flex items-center justify-between rounded-md border px-3 py-2">
                  <div className="text-sm">
                    <span className="font-medium">{DAYS[r.dayOfWeek]}</span>{" "}
                    <span className="text-muted-foreground">{r.startTime.slice(0, 5)} – {r.endTime.slice(0, 5)} · {r.slotMinutes}min · {r.consultationType}</span>
                  </div>
                  <Button variant="ghost" size="icon" onClick={() => removeRule(r.id)}><Trash2 className="h-4 w-4 text-destructive" /></Button>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardContent className="pt-6 space-y-4">
          <h3 className="text-sm font-semibold flex items-center gap-2"><CalendarOff className="h-4 w-4" /> Holidays</h3>
          <div className="flex gap-2 items-end">
            <div><Label>Date</Label><Input type="date" value={holidayDate} onChange={(e) => setHolidayDate(e.target.value)} /></div>
            <div className="flex-1"><Label>Reason (optional)</Label><Input value={holidayReason} onChange={(e) => setHolidayReason(e.target.value)} /></div>
            <Button onClick={addHoliday} disabled={!holidayDate}><Plus className="h-4 w-4 mr-1" /> Add</Button>
          </div>
          {holidays && holidays.length > 0 && (
            <div className="flex flex-wrap gap-2">
              {holidays.map((h) => (
                <Badge key={h.id} variant="muted" className="gap-1 cursor-pointer" onClick={() => removeHoliday(h.id)}>
                  {h.date}{h.reason ? ` — ${h.reason}` : ""}
                  <Trash2 className="h-3 w-3" />
                </Badge>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardContent className="pt-6 space-y-3">
          <h3 className="text-sm font-semibold flex items-center gap-2"><Sparkles className="h-4 w-4" /> Generate slots</h3>
          <p className="text-xs text-muted-foreground">Materialize bookable time slots from your rules. Idempotent — running it twice doesn't create duplicates.</p>
          <div className="flex gap-2 items-end">
            <div><Label>From</Label><Input type="date" value={genFrom} onChange={(e) => setGenFrom(e.target.value)} /></div>
            <div><Label>To</Label><Input type="date" value={genTo} onChange={(e) => setGenTo(e.target.value)} /></div>
            <Button onClick={generate}>Generate</Button>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
