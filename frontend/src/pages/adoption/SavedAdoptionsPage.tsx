import { useEffect, useMemo, useRef } from "react";
import { useInfiniteQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { Bookmark, HeartHandshake, MapPin } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { adoptionApi } from "@/api/adoption";

export function SavedAdoptionsPage() {
  const {
    data, fetchNextPage, hasNextPage, isFetchingNextPage, isLoading
  } = useInfiniteQuery({
    queryKey: ["adoption", "saved"],
    queryFn: ({ pageParam }) => adoptionApi.saved({ cursor: pageParam, pageSize: 24 }),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (last) => last.nextCursor ?? undefined
  });
  const items = useMemo(() => (data?.pages ?? []).flatMap((p) => p.items), [data]);

  const sentinel = useRef<HTMLDivElement | null>(null);
  useEffect(() => {
    if (!sentinel.current) return;
    const io = new IntersectionObserver((e) => {
      if (e[0]?.isIntersecting && hasNextPage && !isFetchingNextPage) fetchNextPage();
    });
    io.observe(sentinel.current);
    return () => io.disconnect();
  }, [hasNextPage, isFetchingNextPage, fetchNextPage]);

  return (
    <div className="space-y-4">
      <PageHeader title="Saved adoptions" icon={Bookmark} description="Listings you've bookmarked." />

      {isLoading ? (
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
          {[...Array(6)].map((_, i) => <Skeleton key={i} className="h-60 rounded-lg" />)}
        </div>
      ) : items.length === 0 ? (
        <EmptyState icon={Bookmark} title="Nothing saved yet"
          description="Tap the bookmark on a listing to keep it here." />
      ) : (
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
          {items.map((l) => (
            <Link key={l.id} to={`/adoption/${l.id}`}>
              <Card className="overflow-hidden hover:shadow-md transition-shadow h-full">
                <div className="aspect-square bg-muted">
                  {l.photoUrls?.[0]
                    ? <img src={l.photoUrls[0]} className="object-cover w-full h-full" />
                    : <div className="flex items-center justify-center h-full text-muted-foreground"><HeartHandshake className="h-10 w-10" /></div>}
                </div>
                <CardContent className="pt-4 space-y-1">
                  <p className="font-semibold line-clamp-1">{l.petName ?? l.title}</p>
                  <p className="text-xs text-muted-foreground">{l.animalType}{l.breed ? ` · ${l.breed}` : ""}</p>
                  <p className="text-xs text-muted-foreground flex items-center gap-1"><MapPin className="h-3 w-3" /> {l.location ?? "—"}</p>
                </CardContent>
              </Card>
            </Link>
          ))}
        </div>
      )}

      {hasNextPage && (
        <div ref={sentinel} className="py-4 text-center text-sm text-muted-foreground">
          {isFetchingNextPage ? "Loading more..." : "Scroll for more"}
        </div>
      )}
    </div>
  );
}
