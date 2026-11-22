import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { CalendarClock, Copy } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { toast } from "@/components/ui/sonner";
import { calendarShareApi, type CalendarShare } from "@/api/clinicPlus";
import { apiBase } from "@/api/client";

/** Create / revoke read-only subscribable calendar links (.ics). The secret URL is shown once, at creation. */
export function CalendarSharePanel() {
  const qc = useQueryClient();
  const [label, setLabel] = useState("");
  const [created, setCreated] = useState<{ url: string } | null>(null);
  const { data } = useQuery({ queryKey: ["calendar", "shares"], queryFn: calendarShareApi.list });

  const create = useMutation({
    mutationFn: () => calendarShareApi.create("Patient", label.trim() || undefined, 90),
    onSuccess: (s: CalendarShare) => {
      const path = s.feedPath ?? "";
      const base = apiBase.startsWith("http") ? apiBase.replace(/\/api$/, "") : window.location.origin;
      setCreated({ url: base + path });
      setLabel("");
      qc.invalidateQueries({ queryKey: ["calendar", "shares"] });
    }
  });
  const revoke = useMutation({
    mutationFn: (id: string) => calendarShareApi.revoke(id),
    onSuccess: () => { toast.success("Link revoked."); qc.invalidateQueries({ queryKey: ["calendar", "shares"] }); }
  });

  const active = (data ?? []).filter((s) => !s.revokedAt && (!s.expiresAt || new Date(s.expiresAt) > new Date()));

  return (
    <Card>
      <CardHeader><CardTitle className="flex items-center gap-2 text-base"><CalendarClock className="h-4 w-4" /> Share my calendar</CardTitle></CardHeader>
      <CardContent className="space-y-3 text-sm">
        <p className="text-muted-foreground">Anyone with the link can subscribe to a read-only feed of your appointments (no symptoms or notes). Links expire after 90 days and can be revoked anytime.</p>
        <div className="flex gap-2">
          <Input placeholder="Label (e.g. Family)" value={label} onChange={(e) => setLabel(e.target.value)} aria-label="Link label" />
          <Button onClick={() => create.mutate()} disabled={create.isPending}>Create link</Button>
        </div>
        {created && (
          <div className="rounded-md border p-2 space-y-1">
            <p className="text-xs">Copy it now. It will not be shown again.</p>
            <div className="flex gap-2">
              <Input readOnly value={created.url} onFocus={(e) => e.currentTarget.select()} aria-label="Calendar feed URL" />
              <Button type="button" variant="outline" onClick={() => navigator.clipboard?.writeText(created.url).then(() => toast.success("Copied."))}><Copy className="h-4 w-4" /></Button>
            </div>
          </div>
        )}
        {active.length > 0 && (
          <ul className="space-y-1">
            {active.map((s) => (
              <li key={s.id} className="flex items-center justify-between gap-2 border rounded-md px-2 py-1">
                <span>{s.label ?? "Calendar link"} <span className="text-xs text-muted-foreground">({s.tokenHint}… · expires {s.expiresAt ? new Date(s.expiresAt).toLocaleDateString() : "never"})</span></span>
                <Button size="sm" variant="outline" onClick={() => revoke.mutate(s.id)}>Revoke</Button>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}
