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
import { PageHeader } from "@/components/common/PageHeader";
import { DataTable, type Column } from "@/components/common/DataTable";
import { adminApi, type AdminNotification } from "@/api/adminV2";
import { toast } from "@/components/ui/sonner";

export function AdminNotificationsPage() {
  const qc = useQueryClient();
  const [unreadOnly, setUnreadOnly] = useState<"all" | "unread">("all");
  const [title, setTitle] = useState("");
  const [body, setBody] = useState("");
  const [targetUserId, setTargetUserId] = useState("");

  const { data, isLoading } = useQuery({
    queryKey: ["admin-notifications", unreadOnly],
    queryFn: () => adminApi.notifications.list({
      unreadOnly: unreadOnly === "unread" || undefined,
      pageSize: 100
    })
  });

  const broadcast = useMutation({
    mutationFn: () => adminApi.notifications.broadcast({ title, body }),
    onSuccess: () => {
      toast.success("Broadcast sent to every user.");
      setTitle(""); setBody("");
      qc.invalidateQueries({ queryKey: ["admin-notifications"] });
    },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Broadcast failed.")
  });

  const direct = useMutation({
    mutationFn: () => adminApi.notifications.send({ userId: targetUserId, title, body }),
    onSuccess: () => {
      toast.success("Notification sent.");
      setTitle(""); setBody(""); setTargetUserId("");
      qc.invalidateQueries({ queryKey: ["admin-notifications"] });
    },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Send failed.")
  });

  const columns: Column<AdminNotification>[] = [
    { key: "user", header: "Recipient", render: (n) => (
      <div className="text-sm">
        <p className="font-medium">{n.userDisplayName}</p>
        <p className="text-xs text-muted-foreground font-mono">{n.userId.slice(0, 8)}…</p>
      </div>
    ) },
    { key: "title", header: "Title", render: (n) => <span className="font-medium">{n.title}</span> },
    { key: "body",  header: "Body", render: (n) => <span className="text-sm text-muted-foreground line-clamp-2 max-w-md">{n.body}</span> },
    { key: "status", header: "Read", render: (n) =>
        n.isRead ? <Badge variant="outline">Read</Badge> : <Badge variant="secondary">Unread</Badge> },
    { key: "when", header: "Sent",
      render: (n) => <span className="text-xs text-muted-foreground">{new Date(n.createdAt).toLocaleString()}</span> }
  ];

  return (
    <div className="space-y-4">
      <PageHeader title="Notifications" icon={Bell} description="Audit and trigger notifications." />

      <Tabs defaultValue="history">
        <TabsList>
          <TabsTrigger value="history">History</TabsTrigger>
          <TabsTrigger value="send">Send</TabsTrigger>
        </TabsList>

        <TabsContent value="history">
          <Card>
            <CardContent className="pt-6 space-y-4">
              <Select value={unreadOnly} onValueChange={(v) => setUnreadOnly(v as any)}>
                <SelectTrigger className="w-44"><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All</SelectItem>
                  <SelectItem value="unread">Unread only</SelectItem>
                </SelectContent>
              </Select>
              <DataTable data={data ?? []} columns={columns} rowKey={(n) => n.id} loading={isLoading} />
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="send">
          <div className="grid lg:grid-cols-2 gap-4">
            <Card>
              <CardHeader><CardTitle className="text-base">Send to one user</CardTitle></CardHeader>
              <CardContent className="space-y-3">
                <div>
                  <Label>User ID</Label>
                  <Input value={targetUserId} onChange={(e) => setTargetUserId(e.target.value.trim())} placeholder="UUID" />
                </div>
                <div>
                  <Label>Title</Label>
                  <Input value={title} onChange={(e) => setTitle(e.target.value)} />
                </div>
                <div>
                  <Label>Body</Label>
                  <Textarea rows={4} value={body} onChange={(e) => setBody(e.target.value)} />
                </div>
                <Button onClick={() => direct.mutate()}
                  disabled={direct.isPending || !title || !body || !targetUserId}>
                  <Send className="h-3 w-3 mr-1" /> {direct.isPending ? "Sending..." : "Send"}
                </Button>
              </CardContent>
            </Card>

            <Card>
              <CardHeader><CardTitle className="text-base">Broadcast to everyone</CardTitle></CardHeader>
              <CardContent className="space-y-3">
                <div>
                  <Label>Title</Label>
                  <Input value={title} onChange={(e) => setTitle(e.target.value)} />
                </div>
                <div>
                  <Label>Body</Label>
                  <Textarea rows={4} value={body} onChange={(e) => setBody(e.target.value)} />
                </div>
                <p className="text-xs text-muted-foreground">
                  Reaches every active account. Use sparingly — outage alerts, policy changes.
                </p>
                <Button variant="destructive" onClick={() => broadcast.mutate()}
                  disabled={broadcast.isPending || !title || !body}>
                  <Send className="h-3 w-3 mr-1" /> {broadcast.isPending ? "Sending..." : "Broadcast"}
                </Button>
              </CardContent>
            </Card>
          </div>
        </TabsContent>
      </Tabs>
    </div>
  );
}
