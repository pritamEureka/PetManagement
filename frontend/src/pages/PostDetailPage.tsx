import { Link, useParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { ArrowLeft } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Separator } from "@/components/ui/separator";
import { PostCard } from "@/components/feed/PostCard";
import { CommentThread, type CommentItem } from "@/components/feed/CommentThread";
import { EmptyState } from "@/components/common/EmptyState";
import { postsApi } from "@/api/posts";

export function PostDetailPage() {
  const { id = "" } = useParams();
  const { data: post, isLoading } = useQuery({
    queryKey: ["post", id],
    queryFn: () => postsApi.getById(id),
    enabled: !!id
  });

  // GET /posts/{id}/comments isn't exposed yet; UI shows the composer + an
  // empty thread until the endpoint is added. Real wire-up will populate this.
  const comments: CommentItem[] = [];

  if (isLoading) return <div className="max-w-2xl mx-auto space-y-3"><Skeleton className="h-72" /><Skeleton className="h-32" /></div>;
  if (!post) {
    return (
      <div className="max-w-2xl mx-auto space-y-3">
        <Button variant="ghost" size="sm" asChild><Link to="/feed"><ArrowLeft className="h-4 w-4 mr-1" /> Back</Link></Button>
        <EmptyState title="Post not found" description="It may have been removed by the author or a moderator." />
      </div>
    );
  }

  return (
    <div className="max-w-2xl mx-auto space-y-4">
      <Button variant="ghost" size="sm" asChild>
        <Link to="/feed"><ArrowLeft className="h-4 w-4 mr-1" /> Back to feed</Link>
      </Button>

      <PostCard post={post} />

      <Card>
        <CardContent className="pt-6 space-y-3">
          <h2 className="text-sm font-semibold">Comments</h2>
          <Separator />
          <CommentThread postId={post.id} comments={comments} />
        </CardContent>
      </Card>
    </div>
  );
}
