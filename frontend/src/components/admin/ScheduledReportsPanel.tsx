import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { CalendarClock, Download, History, Pause, Play, Plus, Trash2 } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { toast } from "@/components/ui/sonner";
import { useAuthStore } from "@/store/authStore";
import type { IntelligenceDataset } from "@/api/intelligence";
import {
  scheduledReportsApi, type ReportFrequency, type ScheduledReport, type SaveScheduledReport
} from "@/api/scheduledReports";

const DATASETS: { value: IntelligenceDataset; label: string }[] = [
  { value: "demand", label: "Appointment demand" },
  { value: "no-show", label: "No-show rates" },
  { value: "capacity", label: "Capacity utilisation" },
  { value: "vaccine-coverage", label: "Vaccine coverage" },
  { value: "lost-pets", label: "Lost pet trend" },
  { value: "store", label: "Store trends" },
  { value: "inventory", label: "Inventory run-out" },
  { value: "anomalies", label: "Anomaly flags" }
];
const FREQUENCIES: ReportFrequency[] = ["Daily", "Weekly", "Monthly"];

const selectCls =
  "flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring";

const fmt = (d: string | null) => (d ? new Date(d).toLocaleString() : "-");

function parseRecipients(text: string): string[] {
  return text.split(/[\s,;]+/).map(s => s.trim()).filter(Boolean);
}

function ScheduleDialog({ open, onOpenChange, initial }: {
  open: boolean; onOpenChange: (o: boolean) => void; initial: ScheduledReport | null;
}) {
  const qc = useQueryClient();
  const [name, setName] = useState(initial?.name ?? "");
  const [dataset, setDataset] = useState<IntelligenceDataset>(initial?.dataset ?? "demand");
  const [frequency, setFrequency] = useState<ReportFrequency>(initial?.frequency ?? "Weekly");
  const [recipients, setRecipients] = useState(initial?.recipients.join(", ") ?? "");

  const save = useMutation({
    mutationFn: () => {
      const body: SaveScheduledReport = {
        name: name.trim(), dataset, frequency, recipients: parseRecipients(recipients), isActive: initial?.isActive ?? true
      };
      return initial ? scheduledReportsApi.update(initial.id, body) : scheduledReportsApi.create(body);
    },
    onSuccess: () => {
      toast.success(initial ? "Schedule updated." : "Schedule created.");
      qc.invalidateQueries({ queryKey: ["intel-schedules"] });
      onOpenChange(false);
    }
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{initial ? "Edit schedule" : "New scheduled report"}</DialogTitle>
          <DialogDescription>The CSV is emailed to the recipients (inline in the message) and kept in the run history.</DialogDescription>
        </DialogHeader>
        <div className="space-y-3">
          <div className="space-y-1.5">
            <Label htmlFor="sr-name">Name</Label>
            <Input id="sr-name" value={name} onChange={e => setName(e.target.value)} maxLength={120} placeholder="Weekly demand" />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="sr-dataset">Dataset</Label>
              <select id="sr-dataset" className={selectCls} value={dataset} onChange={e => setDataset(e.target.value as IntelligenceDataset)}>
                {DATASETS.map(d => <option key={d.value} value={d.value}>{d.label}</option>)}
              </select>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="sr-freq">Frequency</Label>
              <select id="sr-freq" className={selectCls} value={frequency} onChange={e => setFrequency(e.target.value as ReportFrequency)}>
                {FREQUENCIES.map(f => <option key={f} value={f}>{f}</option>)}
              </select>
            </div>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="sr-rcpt">Recipients (comma separated, max 10)</Label>
            <Input id="sr-rcpt" value={recipients} onChange={e => setRecipients(e.target.value)} placeholder="ops@example.com, ceo@example.com" />
          </div>
          <p className="text-xs text-muted-foreground">Runs at 06:00 UTC.</p>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button disabled={!name.trim() || parseRecipients(recipients).length === 0 || save.isPending} onClick={() => save.mutate()}>
            {save.isPending ? "Saving..." : "Save"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function RunHistory({ report }: { report: ScheduledReport }) {
  const runs = useQuery({ queryKey: ["intel-schedule-runs", report.id], queryFn: () => scheduledReportsApi.runs(report.id) });
  if (runs.isLoading) return <p className="py-2 text-xs text-muted-foreground">Loading history...</p>;
  if (!runs.data?.length) return <p className="py-2 text-xs text-muted-foreground">No runs yet.</p>;
  return (
    <ul className="divide-y text-xs">
      {runs.data.map(r => (
        <li key={r.id} className="flex items-center justify-between py-1.5 gap-2">
          <span>{fmt(r.ranAt)}{r.manual ? " (manual)" : ""}</span>
          <span className="flex items-center gap-2">
            <span className="text-muted-foreground">{r.rowCount} rows, {r.emailsSent}/{r.recipientCount} emailed</span>
            <Badge variant={r.success ? "success" : "destructive"} title={r.error ?? undefined}>{r.success ? "ok" : "failed"}</Badge>
            {r.hasCsv && (
              <Button size="sm" variant="ghost" aria-label="Download CSV"
                onClick={() => scheduledReportsApi.downloadRun(report.id, r.id, `${report.dataset}-${r.ranAt.slice(0, 10)}`)}>
                <Download className="h-3.5 w-3.5" />
              </Button>
            )}
          </span>
        </li>
      ))}
    </ul>
  );
}

export function ScheduledReportsPanel() {
  const qc = useQueryClient();
  const canWrite = useAuthStore(s => s.hasPermission("users.edit"));
  const [editing, setEditing] = useState<ScheduledReport | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [dialogKey, setDialogKey] = useState(0);
  const [openRuns, setOpenRuns] = useState<string | null>(null);

  const list = useQuery({ queryKey: ["intel-schedules"], queryFn: scheduledReportsApi.list });
  const refresh = () => qc.invalidateQueries({ queryKey: ["intel-schedules"] });

  const toggle = useMutation({
    mutationFn: (r: ScheduledReport) => scheduledReportsApi.update(r.id, {
      name: r.name, dataset: r.dataset, frequency: r.frequency, recipients: r.recipients, isActive: !r.isActive
    }),
    onSuccess: refresh
  });
  const remove = useMutation({ mutationFn: (id: string) => scheduledReportsApi.remove(id), onSuccess: refresh });
  const runNow = useMutation({
    mutationFn: (id: string) => scheduledReportsApi.runNow(id),
    onSuccess: (run, id) => {
      toast[run.success ? "success" : "error"](run.success ? `Report sent to ${run.emailsSent} recipient(s).` : run.error ?? "Report failed.");
      refresh();
      qc.invalidateQueries({ queryKey: ["intel-schedule-runs", id] });
    }
  });

  const openDialog = (r: ScheduledReport | null) => { setEditing(r); setDialogKey(k => k + 1); setDialogOpen(true); };

  return (
    <Card>
      <CardHeader className="flex-row items-center justify-between space-y-0">
        <CardTitle className="text-base flex items-center gap-2"><CalendarClock className="h-4 w-4" /> Scheduled reports</CardTitle>
        {canWrite && (
          <Button size="sm" onClick={() => openDialog(null)}><Plus className="h-4 w-4 mr-1" /> New schedule</Button>
        )}
      </CardHeader>
      <CardContent>
        {list.isLoading ? <p className="py-6 text-center text-sm text-muted-foreground">Loading...</p>
          : !list.data?.length ? <p className="py-6 text-center text-sm text-muted-foreground">No scheduled reports yet.</p>
          : (
            <ul className="divide-y">
              {list.data.map(r => (
                <li key={r.id} className="py-3 space-y-2">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <div className="min-w-0">
                      <p className="font-medium text-sm truncate">{r.name}</p>
                      <p className="text-xs text-muted-foreground">
                        {r.dataset} - {r.frequency} - {r.recipients.join(", ")}
                      </p>
                      <p className="text-xs text-muted-foreground">Last: {fmt(r.lastRunAt)} - Next: {r.isActive ? fmt(r.nextRunAt) : "paused"}</p>
                    </div>
                    <div className="flex items-center gap-1.5">
                      {r.lastRunSuccess != null && (
                        <Badge variant={r.lastRunSuccess ? "success" : "destructive"}>{r.lastRunSuccess ? "last ok" : "last failed"}</Badge>
                      )}
                      {!r.isActive && <Badge variant="muted">paused</Badge>}
                      <Button size="sm" variant="ghost" aria-label="Run history" onClick={() => setOpenRuns(openRuns === r.id ? null : r.id)}>
                        <History className="h-4 w-4" />
                      </Button>
                      {canWrite && (
                        <>
                          <Button size="sm" variant="outline" disabled={runNow.isPending} onClick={() => runNow.mutate(r.id)}>Run now</Button>
                          <Button size="sm" variant="ghost" onClick={() => openDialog(r)}>Edit</Button>
                          <Button size="sm" variant="ghost" aria-label={r.isActive ? "Pause" : "Resume"} onClick={() => toggle.mutate(r)}>
                            {r.isActive ? <Pause className="h-4 w-4" /> : <Play className="h-4 w-4" />}
                          </Button>
                          <Button size="sm" variant="ghost" aria-label="Delete schedule"
                            onClick={() => { if (window.confirm(`Delete schedule "${r.name}"?`)) remove.mutate(r.id); }}>
                            <Trash2 className="h-4 w-4 text-destructive" />
                          </Button>
                        </>
                      )}
                    </div>
                  </div>
                  {openRuns === r.id && <RunHistory report={r} />}
                </li>
              ))}
            </ul>
          )}
      </CardContent>
      {dialogOpen && <ScheduleDialog key={dialogKey} open={dialogOpen} onOpenChange={setDialogOpen} initial={editing} />}
    </Card>
  );
}
