import { useEffect, useMemo, useRef } from "react";
import { useParams, Link } from "react-router-dom";
import { useInfiniteQuery } from "@tanstack/react-query";
import { ArrowLeft, User as UserIcon } from "lucide-react";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/common/EmptyState";
import { PostCard } from "@/components/feed/PostCard";
import { postsApi } from "@/api/posts";

export function UserProfilePage() {
  const { userId = "" } = useParams();
  const {
    data, fetchNextPage, hasNextPage, isFetchingNextPage, isLoading
  } = useInfiniteQuery({
    queryKey: ["feed", "user", userId],
    queryFn: ({ pageParam }) => postsApi.byUser(userId, { cursor: pageParam, pageSize: 20 }),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (last) => last.nextCursor ?? undefined,
    enabled: !!userId
  });

  const items = useMemo(() => (data?.pages ?? []).flatMap((p) => p.items), [data]);
  const profile = items[0]?.author;

  const sentinel = useRef<HTMLDivElement | null>(null);
  useEffect(() => {
    if (!sentinel.current) return;
    const io = new IntersectionObserver((e) => {
      if (e[0]?.isIntersecting && hasNextPage && !isFetchingNextPage) fetchNextPage();
    }, { rootMargin: "200px" });
    io.observe(sentinel.current);
    return () => io.disconnect();
  }, [hasNextPage, isFetchingNextPage, fetchNextPage]);

  return (
    <div className="max-w-2xl mx-auto space-y-4">
      <Button variant="ghost" size="sm" asChild>
        <Link to="/feed"><ArrowLeft className="h-4 w-4 mr-1" /> Back</Link>
      </Button>

      <Card>
        <CardContent className="pt-6 flex items-center gap-4">
          <Avatar className="h-16 w-16">
            <AvatarFallback>{profile?.displayName?.[0] ?? "?"}</AvatarFallback>
          </Avatar>
          <div className="flex-1">
            <h1 className="text-xl font-bold">{profile?.displayName ?? "—"}</h1>
            <p className="text-xs text-muted-foreground">Posts: {items.length}{hasNextPage ? "+" : ""}</p>
          </div>
        </CardContent>
      </Card>

      {isLoading && <div className="space-y-3">{[...Array(2)].map((_, i) => <Skeleton key={i} className="h-60 rounded-lg" />)}</div>}

      {!isLoading && items.length === 0 && (
        <EmptyState icon={UserIcon} title="No posts yet" description="This user hasn't shared anything yet." />
      )}

      {items.map((p) => <PostCard key={p.id} post={p} />)}

      {hasNextPage && <div ref={sentinel} className="py-4 text-center text-sm text-muted-foreground">{isFetchingNextPage ? "Loading more..." : "Scroll for more"}</div>}
    </div>
  );
}
