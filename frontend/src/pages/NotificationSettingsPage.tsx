import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { BellRing } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { PageHeader } from "@/components/common/PageHeader";
import { toast } from "@/components/ui/sonner";
import { pushSettingsApi, type NotificationPreferences } from "@/api/pushSettings";
import { useI18n } from "@/lib/i18n";
import { currentPushEndpoint, isPushSupported, subscribeToPush, unsubscribeFromPush } from "@/lib/pwa";

const CATEGORY_KEYS = new Set(["messages", "social", "appointments", "reminders", "orders", "adoption", "lost_found", "community", "system"]);

const toTime = (minute: number) =>
  `${String(Math.floor(minute / 60)).padStart(2, "0")}:${String(minute % 60).padStart(2, "0")}`;
const fromTime = (v: string) => {
  const [h, m] = v.split(":").map(Number);
  return (h || 0) * 60 + (m || 0);
};

export function NotificationSettingsPage() {
  const { t } = useI18n();
  const catLabel = (c: string) => (CATEGORY_KEYS.has(c) ? t(`notifSettings.cat.${c}`) : c);
  const qc = useQueryClient();
  const { data, isLoading } = useQuery({ queryKey: ["notification-preferences"], queryFn: pushSettingsApi.getPreferences });
  const [draft, setDraft] = useState<NotificationPreferences | null>(null);
  const [pushOn, setPushOn] = useState(false);
  const [busy, setBusy] = useState(false);

  useEffect(() => { if (data) setDraft(data); }, [data]);
  useEffect(() => { currentPushEndpoint().then((e) => setPushOn(!!e)).catch(() => setPushOn(false)); }, []);

  const save = useMutation({
    mutationFn: (p: NotificationPreferences) => pushSettingsApi.putPreferences(p),
    onSuccess: (p) => { toast.success(t("notifSettings.saved")); qc.setQueryData(["notification-preferences"], p); },
  });

  const togglePush = async () => {
    setBusy(true);
    try {
      if (pushOn) { await unsubscribeFromPush(); setPushOn(false); toast.success(t("notifSettings.pushDisabledToast")); }
      else { await subscribeToPush(); setPushOn(true); toast.success(t("notifSettings.pushEnabledToast")); }
    } catch (e) {
      toast.error(e instanceof Error ? e.message : t("notifSettings.pushError"));
    } finally { setBusy(false); }
  };

  if (isLoading || !draft) return <Skeleton className="h-64" aria-busy="true" aria-label={t("common.loading")} />;

  const setCat = (category: string, key: "inApp" | "push" | "email", value: boolean) =>
    setDraft({ ...draft, categories: draft.categories.map((c) => (c.category === category ? { ...c, [key]: value } : c)) });
  const q = draft.quietHours;
  const setQuiet = (patch: Partial<typeof q>) => setDraft({ ...draft, quietHours: { ...q, ...patch } });

  return (
    <div className="space-y-6 max-w-3xl">
      <PageHeader title={t("notifSettings.title")} description={t("notifSettings.subtitle")} icon={BellRing} />

      <Card>
        <CardHeader><CardTitle className="text-base">{t("notifSettings.push")}</CardTitle></CardHeader>
        <CardContent className="flex items-center justify-between gap-3 flex-wrap">
          <p className="text-sm text-muted-foreground">
            {isPushSupported() ? (pushOn ? t("notifSettings.pushOn") : t("notifSettings.pushOff")) : t("notifSettings.pushUnsupported")}
          </p>
          <Button disabled={busy || !isPushSupported()} variant={pushOn ? "outline" : "default"} onClick={togglePush}>
            {pushOn ? t("notifSettings.disablePush") : t("notifSettings.enablePush")}
          </Button>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle className="text-base">{t("notifSettings.categories")}</CardTitle></CardHeader>
        <CardContent>
          <div className="grid grid-cols-[1fr_auto_auto_auto] gap-x-6 gap-y-3 items-center text-sm">
            <span />
            <span className="text-xs text-muted-foreground">{t("notifSettings.inApp")}</span>
            <span className="text-xs text-muted-foreground">{t("notifSettings.channelPush")}</span>
            <span className="text-xs text-muted-foreground">{t("notifSettings.email")}</span>
            {draft.categories.map((c) => (
              <div key={c.category} className="contents">
                <span>{catLabel(c.category)}</span>
                <Checkbox aria-label={t("notifSettings.catChannel", { category: catLabel(c.category), channel: t("notifSettings.inApp") })} checked={c.inApp} onChange={(e) => setCat(c.category, "inApp", e.target.checked)} />
                <Checkbox aria-label={t("notifSettings.catChannel", { category: catLabel(c.category), channel: t("notifSettings.channelPush") })} checked={c.push} onChange={(e) => setCat(c.category, "push", e.target.checked)} />
                <Checkbox aria-label={t("notifSettings.catChannel", { category: catLabel(c.category), channel: t("notifSettings.email") })} checked={c.email} onChange={(e) => setCat(c.category, "email", e.target.checked)} />
              </div>
            ))}
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle className="text-base">{t("notifSettings.quietHours")}</CardTitle></CardHeader>
        <CardContent className="space-y-3">
          <label className="flex items-center gap-2 text-sm">
            <Checkbox
              checked={q.enabled}
              onChange={(e) => setQuiet({ enabled: e.target.checked, timezoneOffsetMinutes: -new Date().getTimezoneOffset() })}
            />
            {t("notifSettings.quietLabel")}
          </label>
          <div className="flex items-center gap-3 flex-wrap">
            <Input type="time" aria-label={t("notifSettings.quietStart")} className="w-36" disabled={!q.enabled} value={toTime(q.startMinute)} onChange={(e) => setQuiet({ startMinute: fromTime(e.target.value) })} />
            <span className="text-sm text-muted-foreground">{t("notifSettings.to")}</span>
            <Input type="time" aria-label={t("notifSettings.quietEnd")} className="w-36" disabled={!q.enabled} value={toTime(q.endMinute)} onChange={(e) => setQuiet({ endMinute: fromTime(e.target.value) })} />
          </div>
          <p className="text-xs text-muted-foreground">
            {t("notifSettings.timezone", { offset: `UTC${q.timezoneOffsetMinutes >= 0 ? "+" : "-"}${toTime(Math.abs(q.timezoneOffsetMinutes))}` })}
          </p>
        </CardContent>
      </Card>

      <Button disabled={save.isPending} onClick={() => save.mutate(draft)}>{t("notifSettings.savePrefs")}</Button>
    </div>
  );
}
