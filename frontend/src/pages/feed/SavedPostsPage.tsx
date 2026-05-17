import { useEffect, useMemo, useRef } from "react";
import { useInfiniteQuery, useQueryClient } from "@tanstack/react-query";
import { Bookmark } from "lucide-react";
import { Skeleton } from "@/components/ui/skeleton";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorState } from "@/components/common/ErrorState";
import { PostCard } from "@/components/feed/PostCard";
import { postsApi } from "@/api/posts";

export function SavedPostsPage() {
  const qc = useQueryClient();
  const {
    data, fetchNextPage, hasNextPage, isFetchingNextPage, isLoading, isError, refetch
  } = useInfiniteQuery({
    queryKey: ["feed", "saved"],
    queryFn: ({ pageParam }) => postsApi.saved({ cursor: pageParam, pageSize: 20 }),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (last) => last.nextCursor ?? undefined
  });
  const items = useMemo(() => (data?.pages ?? []).flatMap((p) => p.items), [data]);
  const sentinel = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    if (!sentinel.current) return;
    const io = new IntersectionObserver((e) => {
      if (e[0]?.isIntersecting && hasNextPage && !isFetchingNextPage) fetchNextPage();
    }, { rootMargin: "200px" });
    io.observe(sentinel.current);
    return () => io.disconnect();
  }, [hasNextPage, isFetchingNextPage, fetchNextPage]);

  function invalidate() { qc.invalidateQueries({ queryKey: ["feed", "saved"] }); }

  return (
    <div className="max-w-2xl mx-auto space-y-4">
      <PageHeader title="Saved" icon={Bookmark} description="Posts you've bookmarked." />

      {isLoading && <div className="space-y-3">{[...Array(2)].map((_, i) => <Skeleton key={i} className="h-72 rounded-lg" />)}</div>}
      {isError && <ErrorState message="Couldn't load saved posts." onRetry={() => refetch()} />}

      {!isLoading && !isError && items.length === 0 && (
        <EmptyState icon={Bookmark} title="Nothing saved yet"
          description="Tap the bookmark on any post to keep it here for later." />
      )}

      {items.map((p) => <PostCard key={p.id} post={p} onChanged={invalidate} />)}

      {hasNextPage && <div ref={sentinel} className="py-4 text-center text-sm text-muted-foreground">{isFetchingNextPage ? "Loading more..." : "Scroll for more"}</div>}
    </div>
  );
}
