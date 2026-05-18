import { useEffect, useMemo, useState } from "react";
import { useInfiniteQuery, useQueryClient } from "@tanstack/react-query";
import { Send, Flag, Pencil, Trash2 } from "lucide-react";
import { formatDistanceToNow } from "date-fns";
import {
  Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle
} from "@/components/ui/sheet";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { Input } from "@/components/ui/input";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Skeleton } from "@/components/ui/skeleton";
import { Badge } from "@/components/ui/badge";
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger
} from "@/components/ui/dropdown-menu";
import { commentsApi, type Comment } from "@/api/posts";
import { toast } from "@/components/ui/sonner";
import { MoreHorizontal } from "lucide-react";
import { ReportPostDialog } from "./ReportPostDialog";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";

interface Props {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  postId: string;
}

export function CommentDrawer({ open, onOpenChange, postId }: Props) {
  const qc = useQueryClient();
  const [draft, setDraft] = useState("");
  const [reportTarget, setReportTarget] = useState<string | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<Comment | null>(null);

  const {
    data, fetchNextPage, hasNextPage, isFetchingNextPage, isLoading
  } = useInfiniteQuery({
    queryKey: ["comments", postId],
    queryFn: ({ pageParam }) => commentsApi.list(postId, { cursor: pageParam ?? undefined, pageSize: 30 }),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (p) => p.nextCursor ?? undefined,
    enabled: open
  });

  const all = useMemo(() => (data?.pages ?? []).flatMap((p) => p.items), [data]);

  async function send() {
    if (!draft.trim()) return;
    const text = draft.trim();
    setDraft("");
    try {
      await commentsApi.add(postId, text);
      qc.invalidateQueries({ queryKey: ["comments", postId] });
      qc.invalidateQueries({ queryKey: ["feed"] });
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Couldn't post.");
    }
  }

  function onDelete(c: Comment) { setDeleteTarget(c); }

  async function confirmDeleteComment() {
    if (!deleteTarget) return;
    try {
      await commentsApi.remove(deleteTarget.id);
      qc.invalidateQueries({ queryKey: ["comments", postId] });
      qc.invalidateQueries({ queryKey: ["feed"] });
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Delete failed.");
    }
  }

  return (
    <>
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent side="right" className="w-full sm:max-w-md p-0">
        <SheetHeader>
          <SheetTitle>Comments</SheetTitle>
          <SheetDescription>Be kind. Reports are reviewed by our team.</SheetDescription>
        </SheetHeader>

        <ScrollArea className="flex-1 px-6 py-3">
          {isLoading ? (
            <div className="space-y-3">{[...Array(4)].map((_, i) => <Skeleton key={i} className="h-16 rounded-md" />)}</div>
          ) : all.length === 0 ? (
            <p className="text-sm text-muted-foreground text-center py-10">Be the first to comment.</p>
          ) : (
            <ul className="space-y-3">
              {all.map((c) => <CommentRow key={c.id} c={c} onDelete={() => onDelete(c)} onReport={() => setReportTarget(c.id)} onEdited={() => qc.invalidateQueries({ queryKey: ["comments", postId] })} />)}
              {hasNextPage && (
                <div className="flex justify-center pt-2">
                  <Button size="sm" variant="ghost" onClick={() => fetchNextPage()} disabled={isFetchingNextPage}>
                    {isFetchingNextPage ? "Loading..." : "Load more"}
                  </Button>
                </div>
              )}
            </ul>
          )}
        </ScrollArea>

        <div className="px-6 py-3 border-t flex gap-2">
          <Textarea rows={2} placeholder="Add a comment..." value={draft}
                    onChange={(e) => setDraft(e.target.value)}
                    onKeyDown={(e) => e.key === "Enter" && !e.shiftKey && (e.preventDefault(), send())} />
          <Button onClick={send} disabled={!draft.trim()}><Send className="h-4 w-4" /></Button>
        </div>
      </SheetContent>
    </Sheet>

    {reportTarget && (
      <ReportPostDialog
        open={!!reportTarget}
        onOpenChange={(v) => !v && setReportTarget(null)}
        target={{ id: reportTarget, kind: "comment" }}
        onReport={(reason, details) => commentsApi.report(reportTarget, reason, details)}
      />
    )}

    <ConfirmDialog
      open={!!deleteTarget}
      onOpenChange={(v) => !v && setDeleteTarget(null)}
      title="Delete this comment?"
      description="This is permanent."
      confirmLabel="Delete"
      destructive
      onConfirm={confirmDeleteComment}
    />
    </>
  );
}

interface RowProps { c: Comment; onDelete: () => void; onReport: () => void; onEdited: () => void; }

function CommentRow({ c, onDelete, onReport, onEdited }: RowProps) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(c.content);

  async function save() {
    if (!draft.trim() || draft === c.content) { setEditing(false); return; }
    try {
      await commentsApi.edit(c.id, draft.trim());
      onEdited();
      setEditing(false);
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Edit failed.");
    }
  }

  return (
    <li className="flex gap-2">
      <Avatar className="h-8 w-8">
        <AvatarImage src={c.authorAvatarUrl ?? undefined} />
        <AvatarFallback>{c.authorName[0]}</AvatarFallback>
      </Avatar>
      <div className="flex-1 min-w-0 space-y-1">
        <div className="rounded-lg bg-muted px-3 py-2 relative">
          <div className="flex items-start justify-between gap-2">
            <p className="text-xs font-semibold">{c.authorName} {c.updatedAt && <Badge variant="muted" className="text-[8px] ml-1">edited</Badge>}</p>
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="ghost" size="icon" className="h-6 w-6"><MoreHorizontal className="h-3 w-3" /></Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                {c.isOwn && <DropdownMenuItem onClick={() => setEditing(true)}><Pencil className="h-3 w-3 mr-2" /> Edit</DropdownMenuItem>}
                {c.isOwn && <DropdownMenuItem destructive onClick={onDelete}><Trash2 className="h-3 w-3 mr-2" /> Delete</DropdownMenuItem>}
                {!c.isOwn && <DropdownMenuItem onClick={onReport}><Flag className="h-3 w-3 mr-2" /> Report</DropdownMenuItem>}
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
          {editing ? (
            <div className="space-y-2 mt-1">
              <Input value={draft} onChange={(e) => setDraft(e.target.value)}
                     onKeyDown={(e) => e.key === "Enter" && (e.preventDefault(), save())} />
              <div className="flex gap-2 justify-end">
                <Button size="sm" variant="ghost" onClick={() => { setEditing(false); setDraft(c.content); }}>Cancel</Button>
                <Button size="sm" onClick={save}>Save</Button>
              </div>
            </div>
          ) : (
            <p className="text-sm whitespace-pre-wrap">{c.content}</p>
          )}
        </div>
        <p className="text-[10px] text-muted-foreground pl-3">
          {formatDistanceToNow(new Date(c.createdAt), { addSuffix: true })}
        </p>
      </div>
    </li>
  );
}
