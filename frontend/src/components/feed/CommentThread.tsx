import { useState } from "react";
import { formatDistanceToNow } from "date-fns";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { postsApi } from "@/api/posts";
import { toast } from "@/components/ui/sonner";

export interface CommentItem {
  id: string;
  authorName: string;
  authorAvatarUrl?: string | null;
  content: string;
  createdAt: string;
  replies?: CommentItem[];
}

interface Props {
  postId: string;
  comments: CommentItem[];
  onAdded?: () => void;
}

export function CommentThread({ postId, comments, onAdded }: Props) {
  const [draft, setDraft] = useState("");
  const [busy, setBusy] = useState(false);

  async function submit() {
    if (!draft.trim()) return;
    setBusy(true);
    try {
      await postsApi.comment(postId, draft.trim());
      setDraft("");
      onAdded?.();
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Couldn't post.");
    } finally { setBusy(false); }
  }

  return (
    <div className="space-y-4">
      <div className="flex gap-2 items-start">
        <Textarea
          rows={2}
          placeholder="Add a comment..."
          value={draft}
          onChange={(e) => setDraft(e.target.value)}
          className="flex-1"
        />
        <Button onClick={submit} disabled={!draft.trim() || busy}>{busy ? "..." : "Post"}</Button>
      </div>
      {comments.length === 0 ? (
        <p className="text-sm text-muted-foreground text-center py-6">Be the first to comment.</p>
      ) : (
        <ul className="space-y-3">
          {comments.map((c) => <CommentItem key={c.id} c={c} />)}
        </ul>
      )}
    </div>
  );
}

function CommentItem({ c }: { c: CommentItem }) {
  return (
    <li className="flex gap-2">
      <Avatar className="h-8 w-8">
        <AvatarFallback>{c.authorName[0]}</AvatarFallback>
      </Avatar>
      <div className="flex-1 min-w-0 space-y-1">
        <div className="rounded-lg bg-muted px-3 py-2">
          <p className="text-xs font-semibold">{c.authorName}</p>
          <p className="text-sm whitespace-pre-wrap">{c.content}</p>
        </div>
        <p className="text-[10px] text-muted-foreground pl-3">
          {formatDistanceToNow(new Date(c.createdAt), { addSuffix: true })}
        </p>
        {c.replies && c.replies.length > 0 && (
          <ul className="pl-4 mt-2 space-y-2 border-l">
            {c.replies.map((r) => <CommentItem key={r.id} c={r} />)}
          </ul>
        )}
      </div>
    </li>
  );
}
