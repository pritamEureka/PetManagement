import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Settings, Plus, Save, Trash2, Lock } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Badge } from "@/components/ui/badge";
import { Checkbox } from "@/components/ui/checkbox";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog";
import { PageHeader } from "@/components/common/PageHeader";
import { adminApi, type SystemSettingRow } from "@/api/adminV2";
import { ConfirmDialog } from "@/components/admin/ConfirmDialog";
import { toast } from "@/components/ui/sonner";

export function AdminSystemSettingsPage() {
  const qc = useQueryClient();
  const [creatingOpen, setCreatingOpen] = useState(false);
  const [toDelete, setToDelete] = useState<SystemSettingRow | null>(null);

  const { data, isLoading } = useQuery({
    queryKey: ["admin-settings"],
    queryFn: () => adminApi.settings.list()
  });

  const upsert = useMutation({
    mutationFn: (row: SystemSettingRow) => adminApi.settings.upsert(row.key, {
      valueJson: row.valueJson,
      category: row.category,
      description: row.description ?? undefined,
      isSecret: row.isSecret
    }),
    onSuccess: () => {
      toast.success("Saved.");
      qc.invalidateQueries({ queryKey: ["admin-settings"] });
    },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Save failed.")
  });

  const remove = useMutation({
    mutationFn: (key: string) => adminApi.settings.remove(key),
    onSuccess: () => { toast.success("Deleted."); qc.invalidateQueries({ queryKey: ["admin-settings"] }); }
  });

  return (
    <div className="space-y-4">
      <PageHeader title="System settings" icon={Settings}
        description="Soft config values, feature flags and branding."
        actions={
          <Dialog open={creatingOpen} onOpenChange={setCreatingOpen}>
            <DialogTrigger asChild>
              <Button><Plus className="h-4 w-4 mr-1" /> New setting</Button>
            </DialogTrigger>
            <DialogContent>
              <NewSettingForm onCreated={() => {
                setCreatingOpen(false);
                qc.invalidateQueries({ queryKey: ["admin-settings"] });
              }} onUpsert={(row) => upsert.mutateAsync(row)} />
            </DialogContent>
          </Dialog>
        } />

      <Card>
        <CardContent className="pt-6 space-y-3">
          {isLoading ? <p className="text-sm text-muted-foreground">Loading…</p>
            : !data?.length ? <p className="text-sm text-muted-foreground">No settings configured.</p>
            : data.map((row) => (
                <SettingRow key={row.key} row={row}
                  onSave={(updated) => upsert.mutate(updated)}
                  onDelete={() => setToDelete(row)} />
              ))}
        </CardContent>
      </Card>

      <ConfirmDialog
        open={!!toDelete}
        onOpenChange={(v) => !v && setToDelete(null)}
        destructive
        title={toDelete ? `Delete ${toDelete.key}?` : ""}
        description="This setting will be removed; defaults will apply."
        isLoading={remove.isPending}
        onConfirm={() => toDelete && remove.mutateAsync(toDelete.key)}
      />
    </div>
  );
}

/** Inline editor — each row is independently editable so a slow API doesn't block the others. */
function SettingRow({
  row, onSave, onDelete
}: { row: SystemSettingRow; onSave: (r: SystemSettingRow) => void; onDelete: () => void }) {
  const [value, setValue] = useState(row.valueJson);
  const dirty = value !== row.valueJson;

  return (
    <div className="rounded-md border p-3 space-y-2">
      <div className="flex items-center justify-between flex-wrap gap-2">
        <div className="space-y-0.5">
          <p className="font-mono text-sm">{row.key}</p>
          <p className="text-xs text-muted-foreground">{row.description ?? "—"}</p>
        </div>
        <div className="flex items-center gap-2">
          <Badge variant="outline">{row.category}</Badge>
          {row.isSecret && (
            <Badge variant="secondary" className="flex items-center gap-1">
              <Lock className="h-3 w-3" /> secret
            </Badge>
          )}
        </div>
      </div>
      <Textarea rows={3} className="font-mono text-xs" value={value}
                onChange={(e) => setValue(e.target.value)}
                disabled={row.isSecret} />
      <div className="flex justify-between items-center">
        <p className="text-xs text-muted-foreground">
          {row.updatedAt ? `Updated ${new Date(row.updatedAt).toLocaleString()}` : "Never updated"}
        </p>
        <div className="flex gap-2">
          <Button size="sm" variant="ghost" onClick={onDelete}>
            <Trash2 className="h-3 w-3 mr-1 text-destructive" /> Delete
          </Button>
          <Button size="sm" disabled={!dirty || row.isSecret}
            onClick={() => onSave({ ...row, valueJson: value })}>
            <Save className="h-3 w-3 mr-1" /> Save
          </Button>
        </div>
      </div>
    </div>
  );
}

function NewSettingForm({
  onUpsert, onCreated
}: { onUpsert: (row: SystemSettingRow) => Promise<unknown>; onCreated: () => void }) {
  const [key, setKey] = useState("");
  const [category, setCategory] = useState("general");
  const [description, setDescription] = useState("");
  const [valueJson, setValueJson] = useState("\"\"");
  const [isSecret, setIsSecret] = useState(false);
  const [pending, setPending] = useState(false);

  async function submit() {
    if (!key.trim()) return;
    setPending(true);
    try {
      await onUpsert({
        key: key.trim(), category, valueJson, description: description || undefined, isSecret
      } as SystemSettingRow);
      onCreated();
    } finally { setPending(false); }
  }

  return (
    <>
      <DialogHeader><DialogTitle>New setting</DialogTitle></DialogHeader>
      <div className="space-y-3">
        <div>
          <Label>Key</Label>
          <Input value={key} onChange={(e) => setKey(e.target.value)} placeholder="feature.dark_mode" />
        </div>
        <div>
          <Label>Category</Label>
          <Input value={category} onChange={(e) => setCategory(e.target.value)} />
        </div>
        <div>
          <Label>Description</Label>
          <Input value={description} onChange={(e) => setDescription(e.target.value)} />
        </div>
        <div>
          <Label>Value (JSON)</Label>
          <Textarea rows={3} value={valueJson} onChange={(e) => setValueJson(e.target.value)}
                    className="font-mono text-xs" />
        </div>
        <label className="flex items-center gap-2 text-sm">
          <Checkbox checked={isSecret} onCheckedChange={(v) => setIsSecret(!!v)} />
          Treat value as secret (masked in list view)
        </label>
        <Button onClick={submit} disabled={pending || !key.trim()} className="w-full">
          {pending ? "Creating…" : "Create"}
        </Button>
      </div>
    </>
  );
}
