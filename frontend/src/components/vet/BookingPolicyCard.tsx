import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Wallet } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { toast } from "@/components/ui/sonner";
import { policyApi, type BookingPolicy } from "@/api/clinicPlus";

const DEFAULT: BookingPolicy = {
  depositPercent: null, depositFixed: null, cancellationWindowHours: 24, lateCancelFeePercent: 0,
  noShowFeePercent: 0, allowRescheduleHours: 0, requiresQuestionnaire: false
};

/** Vet self-service: deposit, cancellation, no-show and reschedule rules plus the questionnaire requirement. */
export function BookingPolicyCard() {
  const qc = useQueryClient();
  const { data } = useQuery({ queryKey: ["vets", "me", "booking-policy"], queryFn: policyApi.mine });
  const [p, setP] = useState<BookingPolicy>(DEFAULT);
  useEffect(() => { if (data) setP({ ...DEFAULT, ...data }); }, [data]);

  const save = useMutation({
    mutationFn: () => policyApi.save(p),
    onSuccess: () => { toast.success("Booking policy saved."); qc.invalidateQueries({ queryKey: ["vets", "me", "booking-policy"] }); }
  });
  const num = (v: string) => (v === "" ? null : Number(v));
  const field = (id: string, label: string, value: number | null | undefined, onChange: (v: number | null) => void, suffix: string) => (
    <div><Label htmlFor={id}>{label}</Label>
      <div className="flex items-center gap-1"><Input id={id} type="number" min={0} value={value ?? ""} onChange={(e) => onChange(num(e.target.value))} /><span className="text-xs text-muted-foreground">{suffix}</span></div></div>
  );

  return (
    <Card>
      <CardHeader><CardTitle className="flex items-center gap-2 text-base"><Wallet className="h-4 w-4" /> Booking and cancellation policy</CardTitle></CardHeader>
      <CardContent className="space-y-3 text-sm">
        <div className="grid gap-3 sm:grid-cols-2">
          {field("bp-dp", "Deposit (percent of fee)", p.depositPercent, (v) => setP({ ...p, depositPercent: v }), "%")}
          {field("bp-df", "Deposit (fixed, overrides percent)", p.depositFixed, (v) => setP({ ...p, depositFixed: v }), "amount")}
          {field("bp-cw", "Free cancellation window", p.cancellationWindowHours, (v) => setP({ ...p, cancellationWindowHours: v ?? 0 }), "hours before")}
          {field("bp-lf", "Late cancellation fee", p.lateCancelFeePercent, (v) => setP({ ...p, lateCancelFeePercent: v ?? 0 }), "% of fee")}
          {field("bp-nf", "No-show fee", p.noShowFeePercent, (v) => setP({ ...p, noShowFeePercent: v ?? 0 }), "% of fee")}
          {field("bp-rs", "Reschedule allowed until", p.allowRescheduleHours, (v) => setP({ ...p, allowRescheduleHours: v ?? 0 }), "hours before")}
        </div>
        <label className="flex items-center gap-2"><input type="checkbox" checked={p.requiresQuestionnaire} onChange={(e) => setP({ ...p, requiresQuestionnaire: e.target.checked })} /> Require the pre-visit questionnaire before online check-in</label>
        <Button onClick={() => save.mutate()} disabled={save.isPending}>Save policy</Button>
      </CardContent>
    </Card>
  );
}
