import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { CalendarDays, Lock, MapPin, Plus, Search, Users } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { CreateCommunityDialog } from "@/components/communities/CommunityDialogs";
import { EventCard } from "@/components/communities/EventCard";
import { useI18n } from "@/lib/i18n";
import { formatNumber } from "@/lib/format";
import { communitiesApi, communityEventsApi } from "@/api/communities";

export function CommunitiesPage() {
  const nav = useNavigate();
  const { t } = useI18n();
  const [q, setQ] = useState("");
  const [mine, setMine] = useState(false);
  const [open, setOpen] = useState(false);

  const { data, isLoading } = useQuery({
    queryKey: ["communities", "list", q, mine],
    queryFn: () => communitiesApi.list({ q: q.trim() || undefined, mine, pageSize: 30 }),
  });
  const feed = useQuery({
    queryKey: ["community-events", "feed"],
    queryFn: () => communityEventsApi.feed(1, 5),
  });

  return (
    <div className="space-y-6">
      <PageHeader
        title={t("communities.title")}
        description={t("communities.subtitle")}
        icon={Users}
        actions={
          <>
            <Button variant="outline" asChild><Link to="/communities/events"><CalendarDays aria-hidden="true" className="h-4 w-4 me-1" />{t("communities.discoverEvents")}</Link></Button>
            <Button onClick={() => setOpen(true)}><Plus aria-hidden="true" className="h-4 w-4 me-1" />{t("communities.new")}</Button>
          </>
        }
      />

      <div className="flex gap-2 flex-wrap">
        <div className="relative flex-1 min-w-[200px]">
          <Search aria-hidden="true" className="h-4 w-4 absolute start-3 top-3 text-muted-foreground" />
          <Input className="ps-9" type="search" aria-label={t("communities.search")} placeholder={t("communities.search")} value={q} onChange={(e) => setQ(e.target.value)} />
        </div>
        <Button variant={mine ? "default" : "outline"} aria-pressed={mine} onClick={() => setMine((m) => !m)}>{t("communities.mine")}</Button>
      </div>

      {feed.data && feed.data.items.length > 0 && (
        <section className="space-y-2">
          <h2 className="font-semibold">{t("communities.upcoming")}</h2>
          <div className="grid gap-3 md:grid-cols-2">
            {feed.data.items.map((e) => <EventCard key={e.id} event={e} />)}
          </div>
        </section>
      )}

      {isLoading ? (
        <div className="grid gap-3 md:grid-cols-2 lg:grid-cols-3" aria-busy="true" role="status" aria-label={t("common.loading")}>{[0, 1, 2].map((i) => <Skeleton key={i} className="h-32" />)}</div>
      ) : data && data.items.length > 0 ? (
        <div className="grid gap-3 md:grid-cols-2 lg:grid-cols-3">
          {data.items.map((c) => (
            <Link key={c.id} to={`/communities/${c.slug}`} className="block">
              <Card className="h-full hover:shadow-md transition-shadow">
                <CardContent className="p-4 space-y-2">
                  <div className="flex items-start justify-between gap-2">
                    <h3 className="font-semibold">{c.name}</h3>
                    {!c.isPublic && <Badge variant="muted"><Lock aria-hidden="true" className="h-3 w-3 me-1" />{t("communities.private")}</Badge>}
                  </div>
                  <p className="text-xs text-muted-foreground">{[c.species, c.breed].filter(Boolean).join(" / ") || t("communities.allPets")}</p>
                  {c.description && <p className="text-sm line-clamp-2">{c.description}</p>}
                  <div className="flex items-center gap-3 text-xs text-muted-foreground">
                    <span className="flex items-center gap-1" aria-label={t("communities.members", { count: formatNumber(c.memberCount) })}><Users aria-hidden="true" className="h-3.5 w-3.5" />{formatNumber(c.memberCount)}</span>
                    {c.city && <span className="flex items-center gap-1"><MapPin aria-hidden="true" className="h-3.5 w-3.5" />{c.city}</span>}
                    {c.myStatus === "Active" && <Badge variant="success">{t("communities.joined")}</Badge>}
                    {c.myStatus === "Pending" && <Badge variant="warning">{t("communities.pending")}</Badge>}
                  </div>
                </CardContent>
              </Card>
            </Link>
          ))}
        </div>
      ) : (
        <EmptyState icon={Users} title={t("communities.empty")} description={t("communities.emptyDesc")}
          action={<Button onClick={() => setOpen(true)}>{t("communities.create")}</Button>} />
      )}

      <CreateCommunityDialog open={open} onOpenChange={setOpen} onCreated={(c) => nav(`/communities/${c.slug}`)} />
    </div>
  );
}
