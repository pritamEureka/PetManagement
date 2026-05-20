import { useEffect, useMemo, useRef, useState } from "react";
import { useInfiniteQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { Plus, HeartHandshake } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Badge, statusBadgeVariant } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { Can } from "@/components/auth/Can";
import { adoptionApi, type AdoptionListingStatus } from "@/api/adoption";
import { CreateAdoptionDialog } from "./CreateAdoptionDialog";

export function MyAdoptionListingsPage() {
  const qc = useQueryClient();
  // "all" is the sentinel for no-filter — Radix Select disallows empty-string values.
  const [status, setStatus] = useState<AdoptionListingStatus | "all">("all");
  const [open, setOpen] = useState(false);

  const queryKey = ["adoption", "mine", status];
  const {
    data, fetchNextPage, hasNextPage, isFetchingNextPage, isLoading
  } = useInfiniteQuery({
    queryKey,
    queryFn: ({ pageParam }) => adoptionApi.mine({
      cursor: pageParam, pageSize: 20,
      status: status === "all" ? undefined : status
    }),
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
      <PageHeader title="My listings" icon={HeartHandshake}
        description="Manage every pet you've put up for adoption."
        actions={
          <Can permission="adoption.create">
            <Button onClick={() => setOpen(true)}><Plus className="h-4 w-4 mr-2" /> New listing</Button>
          </Can>
        } />

      <div className="w-52">
        <Select value={status} onValueChange={(v) => setStatus(v as AdoptionListingStatus | "all")}>
          <SelectTrigger><SelectValue placeholder="All statuses" /></SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All statuses</SelectItem>
            <SelectItem value="Draft">Draft</SelectItem>
            <SelectItem value="PendingApproval">Pending approval</SelectItem>
            <SelectItem value="Approved">Approved</SelectItem>
            <SelectItem value="Rejected">Rejected</SelectItem>
            <SelectItem value="Adopted">Adopted</SelectItem>
            <SelectItem value="Closed">Closed</SelectItem>
          </SelectContent>
        </Select>
      </div>

      {isLoading ? (
        <div className="space-y-3">{[...Array(3)].map((_, i) => <Skeleton key={i} className="h-24" />)}</div>
      ) : items.length === 0 ? (
        <EmptyState icon={HeartHandshake} title="No listings yet"
          description="Create your first adoption listing — it goes through a quick approval before publishing."
          action={<Can permission="adoption.create"><Button onClick={() => setOpen(true)}><Plus className="h-4 w-4 mr-2" /> New listing</Button></Can>} />
      ) : (
        <div className="space-y-2">
          {items.map((l) => (
            <Link key={l.id} to={`/adoption/${l.id}`}>
              <Card className="hover:bg-accent/30 transition-colors">
                <CardContent className="py-3 flex items-center gap-3">
                  <div className="h-14 w-14 rounded-md bg-muted overflow-hidden flex-shrink-0">
                    {l.photoUrls?.[0] && <img src={l.photoUrls[0]} className="object-cover w-full h-full" />}
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className="font-medium truncate">{l.petName ?? l.title}</p>
                    <p className="text-xs text-muted-foreground truncate">
                      {l.animalType}{l.breed ? ` · ${l.breed}` : ""} · {new Date(l.createdAt).toLocaleDateString()}
                    </p>
                  </div>
                  <Badge variant={statusBadgeVariant(l.status)}>
                    {l.status === "PendingApproval" ? "Pending" : l.status}
                  </Badge>
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

      <CreateAdoptionDialog open={open} onOpenChange={setOpen}
        onCreated={() => qc.invalidateQueries({ queryKey: ["adoption", "mine"] })} />
    </div>
  );
}
