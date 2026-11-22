import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Bell, Send } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Badge } from "@/components/ui/badge";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { UserPicker } from "@/components/pickers";
import { PageHeader } from "@/components/common/PageHeader";
import { DataTable, type Column } from "@/components/common/DataTable";
import { QueryBoundary } from "@/components/common/QueryBoundary";
import { EmptyState } from "@/components/common/EmptyState";
import { DraftBanner } from "@/components/common/DraftBanner";
import { adminApi, type AdminNotification } from "@/api/adminV2";
import { toast } from "@/components/ui/sonner";
import { useDraft } from "@/hooks/useDraft";
import { useI18n } from "@/lib/i18n";
import { formatDateTime } from "@/lib/format";

interface ComposeDraft { title: string; body: string; targetUserId: string }

export function AdminNotificationsPage() {
  const { t } = useI18n();
  const qc = useQueryClient();
  const [tab, setTab] = useState("history");
  const [unreadOnly, setUnreadOnly] = useState<"all" | "unread">("all");
  const [title, setTitle] = useState("");
  const [body, setBody] = useState("");
  const [targetUserId, setTargetUserId] = useState("");
  const draft = useDraft<ComposeDraft>("admin-notification-compose", { title, body, targetUserId });

  const query = useQuery({
    queryKey: ["admin-notifications", unreadOnly],
    queryFn: () => adminApi.notifications.list({
      unreadOnly: unreadOnly === "unread" || undefined,
      pageSize: 100
    })
  });

  const broadcast = useMutation({
    mutationFn: () => adminApi.notifications.broadcast({ title, body }),
    onSuccess: () => {
      toast.success(t("adm.notif.broadcastSent"));
      draft.clear();
      setTitle(""); setBody("");
      qc.invalidateQueries({ queryKey: ["admin-notifications"] });
    },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? t("adm.notif.broadcastFailed"))
  });

  const direct = useMutation({
    mutationFn: () => adminApi.notifications.send({ userId: targetUserId, title, body }),
    onSuccess: () => {
      toast.success(t("adm.notif.sentOk"));
      draft.clear();
      setTitle(""); setBody(""); setTargetUserId("");
      qc.invalidateQueries({ queryKey: ["admin-notifications"] });
    },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? t("adm.notif.sendFailed"))
  });

  const columns: Column<AdminNotification>[] = [
    { key: "user", header: t("adm.notif.recipient"), render: (n) => (
      <div className="text-sm">
        <p className="font-medium">{n.userDisplayName}</p>
        <p className="text-xs text-muted-foreground font-mono">{n.userId.slice(0, 8)}…</p>
      </div>
    ) },
    { key: "title", header: t("adm.notif.titleLabel"), render: (n) => <span className="font-medium">{n.title}</span> },
    { key: "body",  header: t("adm.notif.bodyLabel"), render: (n) => <span className="text-sm text-muted-foreground line-clamp-2 max-w-md">{n.body}</span> },
    { key: "status", header: t("adm.notif.read"), render: (n) =>
        n.isRead ? <Badge variant="outline">{t("adm.notif.read")}</Badge> : <Badge variant="secondary">{t("adm.notif.unread")}</Badge> },
    { key: "when", header: t("adm.notif.sent"),
      render: (n) => <span className="text-xs text-muted-foreground">{formatDateTime(n.createdAt)}</span> }
  ];

  return (
    <div className="space-y-4">
      <PageHeader title={t("adm.notif.title")} icon={Bell} description={t("adm.notif.desc")} />

      <Tabs value={tab} onValueChange={setTab}>
        <TabsList>
          <TabsTrigger value="history">{t("adm.notif.history")}</TabsTrigger>
          <TabsTrigger value="send">{t("adm.notif.send")}</TabsTrigger>
        </TabsList>

        <TabsContent value="history">
          <Card>
            <CardContent className="pt-6 space-y-4">
              <Select value={unreadOnly} onValueChange={(v) => setUnreadOnly(v as "all" | "unread")}>
                <SelectTrigger className="w-full sm:w-44" aria-label={t("adm.status")}><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">{t("adm.notif.all")}</SelectItem>
                  <SelectItem value="unread">{t("adm.notif.unreadOnly")}</SelectItem>
                </SelectContent>
              </Select>
              <QueryBoundary
                query={query}
                empty={
                  <EmptyState
                    icon={Bell}
                    title={t("adm.notif.emptyTitle")}
                    description={t("adm.notif.emptyBody")}
                    action={<Button size="sm" onClick={() => setTab("send")}>{t("adm.notif.emptyCta")}</Button>}
                  />
                }
              >
                {(rows) => <DataTable data={rows} columns={columns} rowKey={(n) => n.id} caption={t("adm.notif.history")} />}
              </QueryBoundary>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="send">
          <DraftBanner<ComposeDraft>
            draft={draft}
            className="mb-4"
            onRestore={(d) => { setTitle(d.title ?? ""); setBody(d.body ?? ""); setTargetUserId(d.targetUserId ?? ""); }}
          />
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Card>
              <CardHeader><CardTitle className="text-base">{t("adm.notif.sendOne")}</CardTitle></CardHeader>
              <CardContent className="space-y-3">
                <div>
                  <Label htmlFor="notif-target-user">{t("adm.notif.recipient")}</Label>
                  <UserPicker id="notif-target-user" value={targetUserId} onChange={(id) => setTargetUserId(id)} />
                </div>
                <div>
                  <Label htmlFor="notif-title-1">{t("adm.notif.titleLabel")}</Label>
                  <Input id="notif-title-1" value={title} onChange={(e) => setTitle(e.target.value)} />
                </div>
                <div>
                  <Label htmlFor="notif-body-1">{t("adm.notif.bodyLabel")}</Label>
                  <Textarea id="notif-body-1" rows={4} value={body} onChange={(e) => setBody(e.target.value)} />
                </div>
                <Button onClick={() => direct.mutate()}
                  disabled={direct.isPending || !title || !body || !targetUserId}>
                  <Send aria-hidden="true" className="h-3 w-3 me-1" /> {direct.isPending ? t("adm.notif.sending") : t("adm.notif.send")}
                </Button>
              </CardContent>
            </Card>

            <Card>
              <CardHeader><CardTitle className="text-base">{t("adm.notif.broadcastTitle")}</CardTitle></CardHeader>
              <CardContent className="space-y-3">
                <div>
                  <Label htmlFor="notif-title-2">{t("adm.notif.titleLabel")}</Label>
                  <Input id="notif-title-2" value={title} onChange={(e) => setTitle(e.target.value)} />
                </div>
                <div>
                  <Label htmlFor="notif-body-2">{t("adm.notif.bodyLabel")}</Label>
                  <Textarea id="notif-body-2" rows={4} value={body} onChange={(e) => setBody(e.target.value)} />
                </div>
                <p className="text-xs text-muted-foreground">{t("adm.notif.broadcastWarn")}</p>
                <Button variant="destructive" onClick={() => broadcast.mutate()}
                  disabled={broadcast.isPending || !title || !body}>
                  <Send aria-hidden="true" className="h-3 w-3 me-1" /> {broadcast.isPending ? t("adm.notif.sending") : t("adm.notif.broadcast")}
                </Button>
              </CardContent>
            </Card>
          </div>
        </TabsContent>
      </Tabs>
    </div>
  );
}
