import { useEffect, useMemo, useRef, useState } from "react";
import { useInfiniteQuery, useQueryClient } from "@tanstack/react-query";
import { Plus, Home, PenLine } from "lucide-react";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Can } from "@/components/auth/Can";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorState } from "@/components/common/ErrorState";
import { PostCard } from "@/components/feed/PostCard";
import { CreatePostDialog } from "@/components/feed/CreatePostDialog";
import { postsApi } from "@/api/posts";
import { useAuthStore } from "@/store/authStore";

type Tab = "public" | "following";

export function FeedPage() {
  const qc = useQueryClient();
  const me = useAuthStore((s) => s.user);
  const [open, setOpen] = useState(false);
  const [tab, setTab] = useState<Tab>("public");

  const queryKey = ["feed", tab];
  const fetchPage = ({ pageParam }: { pageParam?: string }) =>
    tab === "public"
      ? postsApi.feed({ cursor: pageParam, pageSize: 20 })
      : postsApi.following({ cursor: pageParam, pageSize: 20 });

  const {
    data, fetchNextPage, hasNextPage, isFetchingNextPage, isLoading, isError, refetch
  } = useInfiniteQuery({
    queryKey,
    queryFn: fetchPage,
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (last) => last.nextCursor ?? undefined
  });

  const items = useMemo(() => (data?.pages ?? []).flatMap((p) => p.items), [data]);

  // Sentinel-driven infinite scroll.
  const sentinelRef = useRef<HTMLDivElement | null>(null);
  useEffect(() => {
    if (!sentinelRef.current) return;
    const io = new IntersectionObserver((entries) => {
      if (entries[0]?.isIntersecting && hasNextPage && !isFetchingNextPage) fetchNextPage();
    }, { rootMargin: "200px" });
    io.observe(sentinelRef.current);
    return () => io.disconnect();
  }, [hasNextPage, isFetchingNextPage, fetchNextPage]);

  function invalidate() { qc.invalidateQueries({ queryKey: ["feed"] }); }

  return (
    <div className="max-w-2xl mx-auto space-y-4">
      <PageHeader title="Feed" icon={Home} description="Latest from your pet community." />

      <Tabs value={tab} onValueChange={(v) => setTab(v as Tab)}>
        <TabsList className="grid grid-cols-2 w-full sm:max-w-xs">
          <TabsTrigger value="public">Public</TabsTrigger>
          <TabsTrigger value="following">Following</TabsTrigger>
        </TabsList>
      </Tabs>

      <Can permission="posts.create">
        <Card className="cursor-pointer hover:bg-background/80 dark:hover:bg-background/60 transition-colors" onClick={() => setOpen(true)}>
          <CardContent className="pt-4 pb-4 flex items-center gap-3">
            <Avatar>
              <AvatarImage src={me?.avatarUrl ?? undefined} />
              <AvatarFallback>{me?.displayName?.[0] ?? "?"}</AvatarFallback>
            </Avatar>
            <div className="flex-1 text-muted-foreground text-sm">
              What's your pet up to today, {me?.displayName?.split(" ")[0] ?? "friend"}?
            </div>
            <Button size="sm"><PenLine className="h-4 w-4 mr-1" /> Post</Button>
          </CardContent>
        </Card>
      </Can>

      {isLoading && <div className="space-y-3">{[...Array(3)].map((_, i) => <Skeleton key={i} className="h-72 rounded-lg" />)}</div>}

      {isError && <ErrorState message="Couldn't load the feed." onRetry={() => refetch()} />}

      {!isLoading && !isError && items.length === 0 && (
        <EmptyState icon={Home}
          title={tab === "following" ? "Your following feed is quiet" : "No posts yet"}
          description={tab === "following"
            ? "Follow some pet parents and vets to see their posts here."
            : "Be the first to share a moment with your pet!"}
          action={<Can permission="posts.create"><Button onClick={() => setOpen(true)}><Plus className="h-4 w-4 mr-2" /> Create a post</Button></Can>}
        />
      )}

      {items.map((p) => <PostCard key={p.id} post={p} onChanged={invalidate} />)}

      {hasNextPage && (
        <div ref={sentinelRef} className="py-4 text-center text-sm text-muted-foreground">
          {isFetchingNextPage ? "Loading more..." : "Scroll for more"}
        </div>
      )}

      <CreatePostDialog open={open} onOpenChange={setOpen} onCreated={invalidate} />
    </div>
  );
}
