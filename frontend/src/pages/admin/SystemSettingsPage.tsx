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
import { QueryBoundary } from "@/components/common/QueryBoundary";
import { EmptyState } from "@/components/common/EmptyState";
import { DraftBanner } from "@/components/common/DraftBanner";
import { adminApi, type SystemSettingRow } from "@/api/adminV2";
import { ConfirmDialog } from "@/components/admin/ConfirmDialog";
import { toast } from "@/components/ui/sonner";
import { CapacityCards } from "@/components/admin/CapacityCards";
import { useDraft } from "@/hooks/useDraft";
import { useI18n } from "@/lib/i18n";
import { formatDateTime } from "@/lib/format";

export function AdminSystemSettingsPage() {
  const { t } = useI18n();
  const qc = useQueryClient();
  const [creatingOpen, setCreatingOpen] = useState(false);
  const [toDelete, setToDelete] = useState<SystemSettingRow | null>(null);

  const query = useQuery({
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
      toast.success(t("adm.saved"));
      qc.invalidateQueries({ queryKey: ["admin-settings"] });
    },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? t("adm.saveFailed"))
  });

  const remove = useMutation({
    mutationFn: (key: string) => adminApi.settings.remove(key),
    onSuccess: () => { toast.success(t("adm.deleted")); qc.invalidateQueries({ queryKey: ["admin-settings"] }); }
  });

  return (
    <div className="space-y-4">
      <CapacityCards />
      <PageHeader title={t("adm.settings.title")} icon={Settings}
        description={t("adm.settings.desc")}
        actions={
          <Dialog open={creatingOpen} onOpenChange={setCreatingOpen}>
            <DialogTrigger asChild>
              <Button><Plus aria-hidden="true" className="h-4 w-4 me-1" /> {t("adm.settings.new")}</Button>
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
          <QueryBoundary
            query={query}
            empty={
              <EmptyState
                icon={Settings}
                title={t("adm.settings.emptyTitle")}
                description={t("adm.settings.emptyBody")}
                action={<Button size="sm" onClick={() => setCreatingOpen(true)}>{t("adm.settings.new")}</Button>}
              />
            }
            className="space-y-3"
          >
            {(data) => (
              <>
                {data.map((row) => (
                  <SettingRow key={row.key} row={row}
                    onSave={(updated) => upsert.mutate(updated)}
                    onDelete={() => setToDelete(row)} />
                ))}
              </>
            )}
          </QueryBoundary>
        </CardContent>
      </Card>

      <ConfirmDialog
        open={!!toDelete}
        onOpenChange={(v) => !v && setToDelete(null)}
        destructive
        title={toDelete ? t("adm.settings.deleteTitle", { key: toDelete.key }) : ""}
        description={t("adm.settings.deleteBody")}
        isLoading={remove.isPending}
        onConfirm={async () => { if (toDelete) await remove.mutateAsync(toDelete.key); }}
      />
    </div>
  );
}

/** Inline editor: each row is independently editable so a slow API does not block the others. */
function SettingRow({
  row, onSave, onDelete
}: { row: SystemSettingRow; onSave: (r: SystemSettingRow) => void; onDelete: () => void }) {
  const { t } = useI18n();
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
              <Lock aria-hidden="true" className="h-3 w-3" /> {t("adm.settings.secret")}
            </Badge>
          )}
        </div>
      </div>
      <Textarea rows={3} className="font-mono text-xs" value={value}
                aria-label={t("adm.settings.valueJson", { key: row.key })}
                onChange={(e) => setValue(e.target.value)}
                disabled={row.isSecret} />
      <div className="flex justify-between items-center">
        <p className="text-xs text-muted-foreground">
          {row.updatedAt ? t("adm.settings.updated", { when: formatDateTime(row.updatedAt) }) : t("adm.settings.neverUpdated")}
        </p>
        <div className="flex gap-2">
          <Button size="sm" variant="ghost" onClick={onDelete}>
            <Trash2 aria-hidden="true" className="h-3 w-3 me-1 text-destructive" /> {t("common.delete")}
          </Button>
          <Button size="sm" disabled={!dirty || row.isSecret}
            onClick={() => onSave({ ...row, valueJson: value })}>
            <Save aria-hidden="true" className="h-3 w-3 me-1" /> {t("common.save")}
          </Button>
        </div>
      </div>
    </div>
  );
}

interface NewSettingDraft {
  key: string;
  category: string;
  description: string;
  valueJson?: string;
  isSecret: boolean;
}

function NewSettingForm({
  onUpsert, onCreated
}: { onUpsert: (row: SystemSettingRow) => Promise<unknown>; onCreated: () => void }) {
  const { t } = useI18n();
  const [key, setKey] = useState("");
  const [category, setCategory] = useState("general");
  const [description, setDescription] = useState("");
  const [valueJson, setValueJson] = useState("\"\"");
  const [isSecret, setIsSecret] = useState(false);
  const [pending, setPending] = useState(false);
  // Secret values are NEVER written to the draft store.
  const draft = useDraft<NewSettingDraft>(
    "admin-setting-new",
    { key, category, description, valueJson, isSecret },
    {
      enabled: !pending,
      sanitize: (v: NewSettingDraft) => ({
        key: v.key, category: v.category, description: v.description, isSecret: v.isSecret,
        ...(v.isSecret ? {} : { valueJson: v.valueJson })
      })
    }
  );

  async function submit() {
    if (!key.trim()) return;
    setPending(true);
    try {
      await onUpsert({
        key: key.trim(), category, valueJson, description: description || undefined, isSecret
      } as SystemSettingRow);
      draft.clear();
      onCreated();
    } finally { setPending(false); }
  }

  return (
    <>
      <DialogHeader><DialogTitle>{t("adm.settings.new")}</DialogTitle></DialogHeader>
      <div className="space-y-3">
        <DraftBanner<NewSettingDraft>
          draft={draft}
          onRestore={(d) => {
            setKey(d.key ?? ""); setCategory(d.category || "general"); setDescription(d.description ?? "");
            setIsSecret(!!d.isSecret);
            if (typeof d.valueJson === "string") setValueJson(d.valueJson);
          }}
        />
        <div>
          <Label htmlFor="set-key">{t("adm.settings.key")}</Label>
          <Input id="set-key" value={key} onChange={(e) => setKey(e.target.value)} placeholder="feature.dark_mode" />
        </div>
        <div>
          <Label htmlFor="set-cat">{t("adm.settings.category")}</Label>
          <Input id="set-cat" value={category} onChange={(e) => setCategory(e.target.value)} />
        </div>
        <div>
          <Label htmlFor="set-desc">{t("adm.settings.description")}</Label>
          <Input id="set-desc" value={description} onChange={(e) => setDescription(e.target.value)} />
        </div>
        <div>
          <Label htmlFor="set-val">{t("adm.settings.valueLabel")}</Label>
          <Textarea id="set-val" rows={3} value={valueJson} onChange={(e) => setValueJson(e.target.value)}
                    className="font-mono text-xs" />
        </div>
        <label className="flex items-center gap-2 text-sm">
          <Checkbox checked={isSecret} onChange={(e) => setIsSecret(e.currentTarget.checked)} />
          {t("adm.settings.secretLabel")}
        </label>
        <Button onClick={submit} disabled={pending || !key.trim()} className="w-full">
          {pending ? t("adm.creating") : t("adm.create")}
        </Button>
      </div>
    </>
  );
}
