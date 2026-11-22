import { useMemo, useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { Bell, BellOff, Check, CheckCheck, ChevronDown, ChevronRight, Heart, Loader2, MessageSquare, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { EmptyState } from "@/components/common/EmptyState";
import { QueryBoundary } from "@/components/common/QueryBoundary";
import { notificationsApi, dismissNotification, type Notification, type NotificationPayload } from "@/api/notifications";
import { notificationMutesApi } from "@/api/pushSettings";
import { UNREAD_NOTIFICATIONS_QUERY_KEY } from "@/hooks/useNotifications";
import { useNotificationHub } from "@/hooks/useNotificationHub";
import { useUndoableAction } from "@/hooks/useUndoableAction";
import { cn } from "@/lib/utils";
import { useI18n } from "@/lib/i18n";
import { formatDate, formatNumber, formatRelativeTime } from "@/lib/format";
import { useAnnounce } from "@/components/common/a11y";
import { toast } from "@/components/ui/sonner";

function parsePayload(payload: string | null): (NotificationPayload & Record<string, unknown>) | null {
  if (!payload) return null;
  try { return JSON.parse(payload); }
  catch { return null; }
}

function getNotificationIcon(payload: NotificationPayload | null) {
  if (!payload) return <Bell aria-hidden="true" className="h-5 w-5 text-muted-foreground" />;
  switch (payload.type) {
    case "post_reaction": return <Heart aria-hidden="true" className="h-5 w-5 text-rose-500" />;
    case "post_comment": return <MessageSquare aria-hidden="true" className="h-5 w-5 text-blue-500" />;
    default: return <Bell aria-hidden="true" className="h-5 w-5 text-muted-foreground" />;
  }
}

function timeAgo(dateStr: string, justNow: string) {
  const diff = Math.floor((Date.now() - new Date(dateStr).getTime()) / 1000);
  if (diff < 60) return justNow;
  if (diff < 604800) return formatRelativeTime(dateStr);
  return formatDate(dateStr);
}

/** Mirrors the backend's NotificationMutePolicy.InferCategory (rows carry no category column). */
function inferCategory(n: Notification, payload: ReturnType<typeof parsePayload>): string {
  const type = String(payload?.type ?? "");
  if (type === "post_reaction" || type === "post_comment") return "social";
  if (type === "digest" && typeof payload?.category === "string") return payload.category as string;
  if (type.startsWith("message")) return "messages";
  const url = n.url ?? "";
  if (url.startsWith("/messages")) return "messages";
  if (url.startsWith("/appointments")) return "appointments";
  if (url.startsWith("/orders")) return "orders";
  if (url.startsWith("/adoption")) return "adoption";
  if (url.startsWith("/lost-found")) return "lost_found";
  if (url.startsWith("/communities")) return "community";
  if (url.startsWith("/feed")) return "social";
  return "general";
}

const KNOWN_CATEGORIES = new Set(["messages", "social", "appointments", "reminders", "orders", "adoption", "lost_found", "community", "system"]);
const MUTE_DAYS = 7;

function dayBucket(iso: string): "today" | "yesterday" | "earlier" {
  const d = new Date(iso);
  const start = new Date();
  start.setHours(0, 0, 0, 0);
  const diffDays = Math.floor((start.getTime() - new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime()) / 86400000);
  return diffDays <= 0 ? "today" : diffDays === 1 ? "yesterday" : "earlier";
}

export function NotificationsPage() {
  const nav = useNavigate();
  const qc = useQueryClient();
  const { t } = useI18n();
  const announce = useAnnounce();
  const undoable = useUndoableAction();
  const [filter, setFilter] = useState<"all" | "unread">("all");
  const [collapsed, setCollapsed] = useState<Record<string, boolean>>({});
  const [dismissed, setDismissed] = useState<Set<string>>(new Set());

  const query = useQuery({
    queryKey: ["notifications", "list", filter],
    queryFn: () => notificationsApi.list({
      unreadOnly: filter === "unread" ? true : undefined,
      pageSize: 100
    })
  });

  // Refresh list when a new notification arrives via SignalR
  useNotificationHub((e) => {
    if (e.kind === "notify") {
      qc.invalidateQueries({ queryKey: ["notifications", "list"] });
      qc.invalidateQueries({ queryKey: UNREAD_NOTIFICATIONS_QUERY_KEY });
    }
  });

  const refresh = () => {
    qc.invalidateQueries({ queryKey: ["notifications", "list"] });
    qc.invalidateQueries({ queryKey: UNREAD_NOTIFICATIONS_QUERY_KEY });
  };

  const markReadMutation = useMutation({
    mutationFn: (id: string) => notificationsApi.markAsRead(id),
    onSuccess: refresh,
  });

  const markAllReadMutation = useMutation({
    mutationFn: () => notificationsApi.markAllAsRead(),
    onSuccess: () => {
      announce(t("notif.allMarkedRead"));
      refresh();
    }
  });

  const muteMutation = useMutation({
    mutationFn: (n: Notification) => {
      const p = parsePayload(n.payload);
      const actor = (p?.reactorId ?? p?.commenterId ?? p?.senderId ?? p?.actorId) as string | undefined;
      const category = inferCategory(n, p);
      if (actor) return notificationMutesApi.mute({ scope: "Source", scopeId: actor, durationHours: MUTE_DAYS * 24 });
      return notificationMutesApi.mute({ scope: "Category", scopeId: category, durationHours: MUTE_DAYS * 24 });
    },
    onSuccess: () => {
      toast.success(t("notif.muted"));
      announce(t("notif.muted"));
      refresh();
    },
  });

  function dismiss(n: Notification) {
    setDismissed((s) => new Set(s).add(n.id));
    undoable({
      key: `dismiss:${n.id}`,
      message: t("notif.dismissed"),
      commit: async () => {
        await dismissNotification(n.id);
        refresh();
      },
      rollback: () => setDismissed((s) => {
        const next = new Set(s);
        next.delete(n.id);
        return next;
      }),
    });
  }

  function handleNotificationClick(notification: Notification) {
    if (!notification.isRead) {
      markReadMutation.mutate(notification.id);
    }
    const payload = parsePayload(notification.payload);
    if (payload?.postId) {
      nav(`/feed/${payload.postId}`);
    } else if (notification.url?.startsWith("/")) {
      nav(notification.url);
    }
  }

  const visible = useMemo(() => (query.data ?? []).filter((n) => !dismissed.has(n.id)), [query.data, dismissed]);
  const unreadCount = visible.filter((n) => !n.isRead).length;

  const groups = useMemo(() => {
    const map = new Map<string, { key: string; day: string; category: string; items: Notification[] }>();
    for (const n of visible) {
      const day = dayBucket(n.createdAt);
      const category = inferCategory(n, parsePayload(n.payload));
      const key = `${day}|${category}`;
      const g = map.get(key) ?? { key, day, category, items: [] };
      g.items.push(n);
      map.set(key, g);
    }
    const order = { today: 0, yesterday: 1, earlier: 2 } as const;
    return [...map.values()].sort((a, b) => order[a.day as keyof typeof order] - order[b.day as keyof typeof order]);
  }, [visible]);

  const catLabel = (c: string) => (KNOWN_CATEGORIES.has(c) ? t(`notifSettings.cat.${c}`) : t(`notif.cat.${c === "digest" ? "digest" : "general"}`));

  return (
    <div className="max-w-2xl mx-auto space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">{t("nav.notifications")}</h1>
          {unreadCount > 0 && (
            <p className="text-sm text-muted-foreground">{t("notif.unreadCount", { count: formatNumber(unreadCount) })}</p>
          )}
        </div>
        <div className="flex items-center gap-2">
          {unreadCount > 0 && (
            <Button
              variant="outline"
              size="sm"
              onClick={() => markAllReadMutation.mutate()}
              disabled={markAllReadMutation.isPending}
            >
              {markAllReadMutation.isPending ? (
                <Loader2 aria-hidden="true" className="h-4 w-4 me-1 animate-spin" />
              ) : (
                <CheckCheck aria-hidden="true" className="h-4 w-4 me-1" />
              )}
              {t("notif.markAllRead")}
            </Button>
          )}
        </div>
      </div>

      {/* Filter tabs */}
      <div className="flex gap-2" role="group" aria-label={t("notif.filter")}>
        <Button aria-pressed={filter === "all"} variant={filter === "all" ? "default" : "outline"} size="sm" className="min-h-9" onClick={() => setFilter("all")}>
          {t("notif.all")}
        </Button>
        <Button aria-pressed={filter === "unread"} variant={filter === "unread" ? "default" : "outline"} size="sm" className="min-h-9" onClick={() => setFilter("unread")}>
          {t("notif.unread")}
        </Button>
      </div>

      <QueryBoundary
        query={query}
        skeletonRows={5}
        isEmpty={() => visible.length === 0}
        empty={
          <EmptyState
            icon={Bell}
            title={filter === "unread" ? t("notif.emptyUnread") : t("notif.empty")}
            description={filter === "unread" ? t("notif.caughtUp") : t("notif.emptyDesc")}
            action={
              <Button variant="outline" size="sm" onClick={() => nav("/settings/notifications")}>
                {t("nav.notificationSettings")}
              </Button>
            }
          />
        }
      >
        {() => (
          <div className="space-y-4">
            {groups.map((g) => {
              const isCollapsed = !!collapsed[g.key];
              const label = `${t(`notif.group.${g.day}`)} · ${catLabel(g.category)}`;
              const panelId = `notif-group-${g.key.replace(/[^a-z0-9]/gi, "-")}`;
              return (
                <section key={g.key} aria-label={label}>
                  <h2 className="mb-1">
                    <button
                      type="button"
                      aria-expanded={!isCollapsed}
                      aria-controls={panelId}
                      aria-label={t(isCollapsed ? "notif.group.expand" : "notif.group.collapse", { group: label })}
                      onClick={() => setCollapsed((c) => ({ ...c, [g.key]: !c[g.key] }))}
                      className="flex min-h-9 items-center gap-1 rounded text-sm font-semibold text-muted-foreground hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring"
                    >
                      {isCollapsed ? <ChevronRight aria-hidden="true" className="h-4 w-4 rtl:rotate-180" /> : <ChevronDown aria-hidden="true" className="h-4 w-4" />}
                      {label}
                      <Badge variant="muted" className="ms-1">{formatNumber(g.items.length)}</Badge>
                    </button>
                  </h2>
                  {!isCollapsed && (
                    <Card id={panelId}>
                      <CardContent className="p-0 divide-y">
                        {g.items.map((notification) => {
                          const payload = parsePayload(notification.payload);
                          return (
                            <div
                              key={notification.id}
                              className={cn("flex items-start gap-1 transition-colors hover:bg-muted", !notification.isRead && "bg-primary/5")}
                            >
                              <button
                                type="button"
                                onClick={() => handleNotificationClick(notification)}
                                className="flex min-w-0 flex-1 items-start gap-3 p-3 sm:p-4 text-start focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring"
                              >
                                <div className="shrink-0 mt-0.5">{getNotificationIcon(payload)}</div>
                                <div className="flex-1 min-w-0 space-y-1">
                                  <div className="flex items-center gap-2">
                                    <p className={cn("text-sm truncate", !notification.isRead ? "font-semibold" : "font-medium")}>
                                      {notification.title}
                                    </p>
                                    {!notification.isRead && <Badge variant="default" className="h-2 w-2 p-0 rounded-full shrink-0" />}
                                  </div>
                                  <p className="text-sm text-muted-foreground line-clamp-2">{notification.body}</p>
                                  <p className="text-xs text-muted-foreground">{timeAgo(notification.createdAt, t("notif.justNow"))}</p>
                                </div>
                              </button>
                              <div className="flex shrink-0 items-center py-2 pe-2">
                                {!notification.isRead && (
                                  <Button variant="ghost" size="icon" className="h-10 w-10" onClick={() => markReadMutation.mutate(notification.id)} title={t("notif.markRead")} aria-label={t("notif.markRead")}>
                                    <Check aria-hidden="true" className="h-4 w-4" />
                                  </Button>
                                )}
                                <Button variant="ghost" size="icon" className="h-10 w-10" disabled={muteMutation.isPending} onClick={() => muteMutation.mutate(notification)} title={t("notif.muteSource")} aria-label={t("notif.muteSource")}>
                                  <BellOff aria-hidden="true" className="h-4 w-4" />
                                </Button>
                                <Button variant="ghost" size="icon" className="h-10 w-10" onClick={() => dismiss(notification)} title={t("notif.dismiss")} aria-label={t("notif.dismiss")}>
                                  <X aria-hidden="true" className="h-4 w-4" />
                                </Button>
                              </div>
                            </div>
                          );
                        })}
                      </CardContent>
                    </Card>
                  )}
                </section>
              );
            })}
          </div>
        )}
      </QueryBoundary>
    </div>
  );
}
