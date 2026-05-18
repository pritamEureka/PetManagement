import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useSearchParams } from "react-router-dom";
import { Send, MessageSquare, Search, Archive, ArchiveX, BellOff, Bell, Ban, MoreHorizontal } from "lucide-react";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Skeleton } from "@/components/ui/skeleton";
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger
} from "@/components/ui/dropdown-menu";
import { messagesApi, type ChatMessage, type ConversationSummary, type Attachment } from "@/api/messages";
import { useAuthStore } from "@/store/authStore";
import { toast } from "@/components/ui/sonner";
import { cn } from "@/lib/utils";
import { useChatHub } from "@/hooks/useChatHub";
import { PresenceDot } from "@/components/messaging/PresenceDot";
import { TypingIndicator } from "@/components/messaging/TypingIndicator";
import { MessageBubble } from "@/components/messaging/MessageBubble";
import { AttachmentUpload } from "@/components/messaging/AttachmentUpload";
import { MessageReportDialog } from "@/components/messaging/MessageReportDialog";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";

const TYPING_DEBOUNCE = 800;

export function MessagingPage() {
  const qc = useQueryClient();
  const me = useAuthStore((s) => s.user);
  const [search, setSearch] = useState("");
  const [includeArchived, setIncludeArchived] = useState(false);
  const [draft, setDraft] = useState("");
  const [attachments, setAttachments] = useState<Attachment[]>([]);
  const [reportingId, setReportingId] = useState<string | null>(null);
  const [blockTarget, setBlockTarget] = useState<{ userId: string; displayName: string } | null>(null);
  const [deletingMessageId, setDeletingMessageId] = useState<string | null>(null);
  const [searchParams, setSearchParams] = useSearchParams();

  const { data: convos, isLoading: loadingList } = useQuery({
    queryKey: ["conversations", includeArchived, search],
    queryFn: () => messagesApi.conversations({ includeArchived, search: search || undefined })
  });
  const activeId = searchParams.get("c");
  const active = useMemo(() => convos?.find((c) => c.id === activeId), [convos, activeId]);

  useEffect(() => {
    if (!activeId && convos && convos.length > 0) setSearchParams({ c: convos[0].id }, { replace: true });
  }, [convos, activeId, setSearchParams]);

  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [loadingHistory, setLoadingHistory] = useState(false);
  useEffect(() => {
    if (!activeId) return;
    setLoadingHistory(true);
    messagesApi.history(activeId)
      .then((p) => setMessages(p.items))
      .finally(() => setLoadingHistory(false));
  }, [activeId]);

  const [typingUsers, setTypingUsers] = useState<Record<string, true>>({});
  const [presence, setPresence] = useState<Record<string, boolean>>({});

  const onEvent = useCallback((e: any) => {
    if (e.kind === "message") {
      const m = e.payload as ChatMessage;
      if (m.conversationId === activeId) {
        setMessages((prev) => prev.some((x) => x.id === m.id) ? prev : [...prev, m]);
        if (m.senderId !== me?.id) hub.ackDelivered(m.id);
      }
      qc.invalidateQueries({ queryKey: ["conversations"] });
    } else if (e.kind === "typing") {
      const { conversationId, userId, isTyping } = e.payload;
      if (conversationId !== activeId || userId === me?.id) return;
      setTypingUsers((prev) => {
        const next = { ...prev };
        if (isTyping) next[userId] = true;
        else delete next[userId];
        return next;
      });
    } else if (e.kind === "read") {
      const { conversationId, userId, readAt } = e.payload;
      if (conversationId !== activeId || userId === me?.id) return;
      setMessages((prev) => prev.map((m) =>
        m.senderId === me?.id && !m.readAt ? { ...m, readAt } : m));
    } else if (e.kind === "delivered") {
      const { messageId, userId, at } = e.payload;
      if (userId === me?.id) return;
      setMessages((prev) => prev.map((m) => m.id === messageId && !m.deliveredAt ? { ...m, deliveredAt: at } : m));
    } else if (e.kind === "presence") {
      const { userId, online } = e.payload;
      setPresence((prev) => ({ ...prev, [userId]: online }));
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [activeId, me?.id, qc]);

  const hub = useChatHub(onEvent);

  useEffect(() => {
    if (!activeId) return;
    hub.joinConversation(activeId);
    const last = messages[messages.length - 1];
    if (last) hub.markRead(activeId, last.id);
    return () => { hub.leaveConversation(activeId); };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [activeId]);

  const endRef = useRef<HTMLDivElement>(null);
  useEffect(() => { endRef.current?.scrollIntoView({ behavior: "smooth" }); }, [messages.length]);

  const typingTimer = useRef<number | null>(null);
  function onDraftChange(value: string) {
    setDraft(value);
    if (!activeId) return;
    hub.sendTyping(activeId, true);
    if (typingTimer.current) window.clearTimeout(typingTimer.current);
    typingTimer.current = window.setTimeout(() => hub.sendTyping(activeId, false), TYPING_DEBOUNCE);
  }

  async function send() {
    if (!activeId || (!draft.trim() && attachments.length === 0)) return;
    const text = draft.trim();
    setDraft(""); setAttachments([]);
    try {
      const sent = await messagesApi.send(activeId, {
        type: attachments.length > 0 && !text
          ? (attachments[0].mimeType.startsWith("image/") ? "Image" : "File")
          : "Text",
        content: text || undefined,
        attachments: attachments.length > 0 ? attachments : undefined
      });
      setMessages((prev) => prev.some((m) => m.id === sent.id) ? prev : [...prev, sent]);
      qc.invalidateQueries({ queryKey: ["conversations"] });
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Send failed.");
    }
  }

  async function toggleArchive() {
    if (!active) return;
    await messagesApi.archive(active.id, !active.isArchived);
    qc.invalidateQueries({ queryKey: ["conversations"] });
    toast.success(active.isArchived ? "Unarchived" : "Archived");
  }
  async function toggleMute() {
    if (!active) return;
    await messagesApi.mute(active.id, !active.isMuted);
    qc.invalidateQueries({ queryKey: ["conversations"] });
  }
  function blockOther() {
    const other = active?.participants[0];
    if (!other) return;
    setBlockTarget({ userId: other.userId, displayName: other.displayName });
  }
  async function confirmBlock() {
    if (!blockTarget) return;
    try {
      await messagesApi.block(blockTarget.userId);
      qc.invalidateQueries({ queryKey: ["conversations"] });
      toast.success("Blocked.");
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Failed.");
    }
  }

  function deleteMessage(id: string) {
    setDeletingMessageId(id);
  }
  async function confirmDeleteMessage() {
    if (!deletingMessageId) return;
    const id = deletingMessageId;
    try {
      await messagesApi.deleteMessage(id);
      setMessages((prev) => prev.map((m) => m.id === id ? { ...m, isDeletedForAll: true, content: null } : m));
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Failed.");
    }
  }

  const typingNames = Object.keys(typingUsers).map((uid) =>
    active?.participants.find((p) => p.userId === uid)?.displayName ?? "Someone");

  return (
    <div className="h-[calc(100vh-6rem)] grid grid-cols-1 md:grid-cols-[18rem_1fr] lg:grid-cols-[20rem_1fr] gap-3">
      <Card className={cn("flex flex-col", activeId ? "hidden md:flex" : "flex")}>
        <div className="p-3 border-b space-y-2">
          <div className="relative">
            <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
            <Input className="pl-8" placeholder="Search conversations" value={search} onChange={(e) => setSearch(e.target.value)} />
          </div>
          <label className="flex items-center gap-2 text-xs text-muted-foreground">
            <input type="checkbox" checked={includeArchived} onChange={(e) => setIncludeArchived(e.target.checked)} />
            Show archived
          </label>
        </div>
        <ScrollArea className="flex-1">
          {loadingList ? (
            <div className="p-3 space-y-3">{[...Array(4)].map((_, i) => <Skeleton key={i} className="h-12 rounded-md" />)}</div>
          ) : !convos || convos.length === 0 ? (
            <div className="p-6 text-center text-sm text-muted-foreground">No conversations yet.</div>
          ) : (
            <ul className="divide-y">
              {convos.map((c) => (
                <ConversationRow key={c.id} c={c} active={c.id === activeId}
                  presence={presence}
                  onClick={() => setSearchParams({ c: c.id }, { replace: true })} />
              ))}
            </ul>
          )}
        </ScrollArea>
      </Card>

      <Card className={cn("flex flex-col overflow-hidden", !activeId ? "hidden md:flex" : "flex")}>
        {!active ? (
          <div className="flex-1 flex items-center justify-center text-muted-foreground">
            <div className="text-center space-y-2">
              <MessageSquare className="h-10 w-10 mx-auto" />
              <p>Pick a conversation to start chatting.</p>
            </div>
          </div>
        ) : (
          <>
            <div className="md:hidden p-2 border-b">
              <Button variant="ghost" size="sm" onClick={() => setSearchParams({}, { replace: true })}>
                ← Back
              </Button>
            </div>
            <ConversationHeader
              c={active}
              presence={presence}
              onArchive={toggleArchive}
              onMute={toggleMute}
              onBlock={blockOther}
            />
            <ScrollArea className="flex-1 p-4">
              {loadingHistory ? (
                <div className="space-y-3">{[...Array(5)].map((_, i) => <Skeleton key={i} className="h-12 rounded-md" />)}</div>
              ) : (
                <div className="space-y-2">
                  {messages.map((m) => {
                    const mine = m.senderId === me?.id;
                    const state = m.readAt ? "read" : m.deliveredAt ? "delivered" : "sent";
                    return (
                      <MessageBubble
                        key={m.id} msg={m} mine={mine} state={state}
                        onDelete={mine ? () => deleteMessage(m.id) : undefined}
                        onReport={!mine ? () => setReportingId(m.id) : undefined}
                      />
                    );
                  })}
                  <div ref={endRef} />
                </div>
              )}
            </ScrollArea>

            <TypingIndicator names={typingNames} />

            <div className="border-t p-3 flex gap-2 items-end">
              <AttachmentUpload attachments={attachments} onChange={setAttachments} />
              <Input
                placeholder="Type a message..."
                value={draft}
                onChange={(e) => onDraftChange(e.target.value)}
                onKeyDown={(e) => e.key === "Enter" && !e.shiftKey && (e.preventDefault(), send())}
              />
              <Button onClick={send} disabled={!draft.trim() && attachments.length === 0}>
                <Send className="h-4 w-4" />
              </Button>
            </div>
          </>
        )}
      </Card>

      {reportingId && (
        <MessageReportDialog
          open={!!reportingId}
          onOpenChange={(v) => !v && setReportingId(null)}
          messageId={reportingId}
        />
      )}

      <ConfirmDialog
        open={!!blockTarget}
        onOpenChange={(v) => !v && setBlockTarget(null)}
        title={`Block ${blockTarget?.displayName ?? "user"}?`}
        description="You'll no longer be able to message them and they won't be able to message you."
        confirmLabel="Block"
        destructive
        onConfirm={confirmBlock}
      />

      <ConfirmDialog
        open={!!deletingMessageId}
        onOpenChange={(v) => !v && setDeletingMessageId(null)}
        title="Delete this message for everyone?"
        description="The message will be removed for all participants in this conversation."
        confirmLabel="Delete"
        destructive
        onConfirm={confirmDeleteMessage}
      />
    </div>
  );
}

function ConversationRow({ c, active, presence, onClick }: {
  c: ConversationSummary; active: boolean; presence: Record<string, boolean>; onClick: () => void;
}) {
  const other = c.participants[0];
  const online = other ? (presence[other.userId] ?? other.online) : false;
  return (
    <li>
      <button onClick={onClick}
              className={`w-full text-left flex items-center gap-3 px-3 py-2.5 hover:bg-accent transition-colors ${active ? "bg-accent" : ""}`}>
        <div className="relative">
          <Avatar>
            <AvatarImage src={other?.avatarUrl ?? undefined} />
            <AvatarFallback>{other?.displayName?.[0] ?? "?"}</AvatarFallback>
          </Avatar>
          <PresenceDot online={online} className="absolute -bottom-0.5 -right-0.5" />
        </div>
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-1">
            <p className="text-sm font-medium truncate">{other?.displayName ?? "Group"}</p>
            {c.isArchived && <Archive className="h-3 w-3 text-muted-foreground" />}
            {c.isMuted && <BellOff className="h-3 w-3 text-muted-foreground" />}
          </div>
          <p className="text-[11px] text-muted-foreground truncate">
            {c.lastMessagePreview ?? "No messages yet"}
          </p>
        </div>
        {c.unreadCount > 0 && <Badge className="ml-auto">{c.unreadCount}</Badge>}
      </button>
    </li>
  );
}

function ConversationHeader({ c, presence, onArchive, onMute, onBlock }: {
  c: ConversationSummary; presence: Record<string, boolean>;
  onArchive: () => void; onMute: () => void; onBlock: () => void;
}) {
  const other = c.participants[0];
  const online = other ? (presence[other.userId] ?? other.online) : false;
  return (
    <div className="border-b p-3 flex items-center gap-3">
      <div className="relative">
        <Avatar>
          <AvatarImage src={other?.avatarUrl ?? undefined} />
          <AvatarFallback>{other?.displayName?.[0] ?? "?"}</AvatarFallback>
        </Avatar>
        <PresenceDot online={online} className="absolute -bottom-0.5 -right-0.5" />
      </div>
      <div className="flex-1 min-w-0">
        <p className="font-medium truncate">{other?.displayName ?? "Direct message"}</p>
        <p className="text-[11px] text-muted-foreground">{online ? "Online" : "Offline"}</p>
      </div>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button variant="ghost" size="icon"><MoreHorizontal className="h-4 w-4" /></Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end">
          <DropdownMenuItem onClick={onArchive}>
            {c.isArchived ? <><ArchiveX className="h-3.5 w-3.5 mr-2" /> Unarchive</> : <><Archive className="h-3.5 w-3.5 mr-2" /> Archive</>}
          </DropdownMenuItem>
          <DropdownMenuItem onClick={onMute}>
            {c.isMuted ? <><Bell className="h-3.5 w-3.5 mr-2" /> Unmute</> : <><BellOff className="h-3.5 w-3.5 mr-2" /> Mute</>}
          </DropdownMenuItem>
          <DropdownMenuSeparator />
          <DropdownMenuItem destructive onClick={onBlock}>
            <Ban className="h-3.5 w-3.5 mr-2" /> Block user
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  );
}
