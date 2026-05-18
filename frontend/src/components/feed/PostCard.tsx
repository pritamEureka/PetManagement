import { useState } from "react";
import { Link } from "react-router-dom";
import { Heart, MessageCircle, Share2, Bookmark, Flag, MoreHorizontal, Pencil, Trash2, EyeOff } from "lucide-react";
import { formatDistanceToNow } from "date-fns";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardFooter, CardHeader } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger
} from "@/components/ui/dropdown-menu";
import { postsApi, type FeedItem } from "@/api/posts";
import { toast } from "@/components/ui/sonner";
import { usePermissions } from "@/hooks/usePermissions";
import { Can } from "@/components/auth/Can";
import { EditPostDialog } from "./EditPostDialog";
import { ReportPostDialog } from "./ReportPostDialog";
import { CommentDrawer } from "./CommentDrawer";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { PromptDialog } from "@/components/common/PromptDialog";

interface Props {
  post: FeedItem;
  onChanged?: () => void;
}

export function PostCard({ post, onChanged }: Props) {
  const { can } = usePermissions();
  const canModerate = can("posts.moderate") || can("posts.delete");
  const [editing, setEditing] = useState(false);
  const [reporting, setReporting] = useState(false);
  const [commenting, setCommenting] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [hideOpen, setHideOpen] = useState(false);

  // Optimistic mirror — UI stays snappy while invalidations roll through.
  const [reactedType, setReactedType] = useState<string | null>(post.myReaction ?? null);
  const [reactionCount, setReactionCount] = useState(post.reactionCount);
  const [saved, setSaved] = useState(post.isSaved);

  async function toggleReact() {
    try {
      if (reactedType) {
        await postsApi.unreact(post.id);
        setReactedType(null);
        setReactionCount((n) => Math.max(0, n - 1));
      } else {
        await postsApi.react(post.id, "Like");
        setReactedType("Like");
        setReactionCount((n) => n + 1);
      }
      onChanged?.();
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Couldn't react.");
    }
  }

  async function onShare() {
    try { await postsApi.share(post.id); toast.success("Shared"); onChanged?.(); }
    catch (err: any) { toast.error(err?.response?.data?.error?.message ?? "Share failed."); }
  }

  async function onSave() {
    try {
      const res = await postsApi.toggleSave(post.id);
      setSaved(res.saved);
      toast.success(res.saved ? "Saved" : "Removed from saved");
      onChanged?.();
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Couldn't save.");
    }
  }

  function onDelete() { setConfirmDelete(true); }
  async function confirmDeletePost() {
    try { await postsApi.remove(post.id); toast.success("Deleted"); onChanged?.(); }
    catch (err: any) { toast.error(err?.response?.data?.error?.message ?? "Delete failed."); }
  }

  function onHide() { setHideOpen(true); }
  async function submitHide(reason: string) {
    try { await postsApi.hide(post.id, true, reason); toast.success("Post hidden"); onChanged?.(); }
    catch (err: any) { toast.error(err?.response?.data?.error?.message ?? "Couldn't hide."); }
  }

  return (
    <Card className={post.isHidden ? "opacity-60" : ""}>
      <CardHeader className="flex flex-row items-center gap-3 space-y-0">
        <Link to={`/u/${post.author.id}`}>
          <Avatar>
            <AvatarImage src={post.author.avatarUrl ?? undefined} />
            <AvatarFallback>{post.author.displayName[0]}</AvatarFallback>
          </Avatar>
        </Link>
        <div className="flex-1 min-w-0">
          <Link to={`/u/${post.author.id}`} className="font-medium leading-tight truncate hover:underline">
            {post.author.displayName}
          </Link>
          <p className="text-xs text-muted-foreground">
            {formatDistanceToNow(new Date(post.createdAt), { addSuffix: true })}
            {post.location ? ` · ${post.location}` : ""}
            {post.animalType ? ` · ${post.animalType}` : ""}
            {post.updatedAt ? " · edited" : ""}
            {post.isHidden && <Badge variant="destructive" className="ml-2 text-[9px]">hidden</Badge>}
          </p>
        </div>
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="icon"><MoreHorizontal className="h-4 w-4" /></Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuItem onClick={onSave}>{saved ? "Remove from saved" : "Save post"}</DropdownMenuItem>
            <DropdownMenuItem onClick={onShare}>Share</DropdownMenuItem>
            {post.isOwn && (
              <>
                <DropdownMenuSeparator />
                <DropdownMenuItem onClick={() => setEditing(true)}><Pencil className="h-3.5 w-3.5 mr-2" /> Edit</DropdownMenuItem>
                <DropdownMenuItem destructive onClick={onDelete}><Trash2 className="h-3.5 w-3.5 mr-2" /> Delete</DropdownMenuItem>
              </>
            )}
            {!post.isOwn && canModerate && (
              <>
                <DropdownMenuSeparator />
                <Can permission="posts.moderate">
                  <DropdownMenuItem onClick={onHide}><EyeOff className="h-3.5 w-3.5 mr-2" /> Hide (moderate)</DropdownMenuItem>
                </Can>
                <Can permission="posts.delete">
                  <DropdownMenuItem destructive onClick={onDelete}><Trash2 className="h-3.5 w-3.5 mr-2" /> Delete (moderate)</DropdownMenuItem>
                </Can>
              </>
            )}
            {!post.isOwn && (
              <>
                <DropdownMenuSeparator />
                <DropdownMenuItem destructive onClick={() => setReporting(true)}>
                  <Flag className="h-3.5 w-3.5 mr-2" /> Report
                </DropdownMenuItem>
              </>
            )}
          </DropdownMenuContent>
        </DropdownMenu>
      </CardHeader>

      <CardContent className="space-y-3">
        {post.content && (
          <Link to={`/feed/${post.id}`} className="block">
            <p className="whitespace-pre-wrap">{post.content}</p>
          </Link>
        )}
        {post.media.length > 0 && (
          <Link to={`/feed/${post.id}`} className={`grid gap-1.5 ${post.media.length === 1 ? "grid-cols-1" : "grid-cols-2"}`}>
            {post.media.slice(0, 4).map((m, i) =>
              m.mediaType === "video"
                ? <video key={i} src={m.url} controls className="rounded-md w-full h-56 object-cover" />
                : <img key={i} src={m.url} className="rounded-md object-cover w-full h-56" />
            )}
          </Link>
        )}
        {post.hashtags.length > 0 && (
          <div className="flex flex-wrap gap-1.5">
            {post.hashtags.map((t) => (
              <Badge key={t} variant="secondary" className="text-[10px]">#{t}</Badge>
            ))}
          </div>
        )}
      </CardContent>

      <CardFooter className="justify-between text-muted-foreground">
        <Button variant="ghost" size="sm" onClick={toggleReact}>
          <Heart className={`h-4 w-4 mr-1 ${reactedType ? "fill-rose-500 text-rose-500" : ""}`} />
          {reactionCount}
        </Button>
        <Button variant="ghost" size="sm" onClick={() => setCommenting(true)}>
          <MessageCircle className="h-4 w-4 mr-1" /> {post.commentCount}
        </Button>
        <Button variant="ghost" size="sm" onClick={onShare}>
          <Share2 className="h-4 w-4 mr-1" /> {post.shareCount}
        </Button>
        <Button variant="ghost" size="sm" onClick={onSave}>
          <Bookmark className={`h-4 w-4 ${saved ? "fill-current" : ""}`} />
        </Button>
      </CardFooter>

      {editing && <EditPostDialog open={editing} onOpenChange={setEditing} post={post} onSaved={() => onChanged?.()} />}
      {reporting && (
        <ReportPostDialog
          open={reporting}
          onOpenChange={setReporting}
          target={{ id: post.id, kind: "post" }}
          onReport={(reason, details) => postsApi.report(post.id, reason, details)}
        />
      )}
      {commenting && <CommentDrawer open={commenting} onOpenChange={setCommenting} postId={post.id} />}

      <ConfirmDialog
        open={confirmDelete}
        onOpenChange={setConfirmDelete}
        title="Delete this post?"
        description="The post is removed for everyone. This can't be undone."
        confirmLabel="Delete"
        destructive
        onConfirm={confirmDeletePost}
      />

      <PromptDialog
        open={hideOpen}
        onOpenChange={setHideOpen}
        title="Hide this post"
        description="The post will be hidden from the feed. The reason is recorded in the audit log."
        label="Reason (visible in audit log)"
        confirmLabel="Hide"
        destructive
        onSubmit={submitHide}
      />
    </Card>
  );
}
