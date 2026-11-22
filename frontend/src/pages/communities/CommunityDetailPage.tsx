import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, Lock, MapPin, Plus, Users } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { toast } from "@/components/ui/sonner";
import { CreateEventDialog } from "@/components/communities/CommunityDialogs";
import { EventCard } from "@/components/communities/EventCard";
import { PollCard } from "@/components/communities/PollCard";
import { CreatePollDialog } from "@/components/communities/CreatePollDialog";
import { ReputationLeaderboard } from "@/components/communities/ReputationLeaderboard";
import { communityPlusApi } from "@/api/socialPlus";
import { useI18n } from "@/lib/i18n";
import { formatDate, formatNumber } from "@/lib/format";
import {
  communitiesApi, type CommunityMember, type CommunityMemberStatus, type MemberAction,
} from "@/api/communities";

function MemberRow({
  m, communityId, isOwner, isMod, onChanged,
}: { m: CommunityMember; communityId: string; isOwner: boolean; isMod: boolean; onChanged: () => void }) {
  const { t } = useI18n();
  const act = useMutation({
    mutationFn: (a: MemberAction) => communitiesApi.moderate(communityId, m.userId, a),
    onSuccess: () => { toast.success(t("communities.updated")); onChanged(); },
  });
  const targetIsOwner = m.role === "Owner";
  const canActOnTarget = isMod && !targetIsOwner && (m.role !== "Moderator" || isOwner);
  const btn = (a: MemberAction, variant: "default" | "outline" | "destructive" = "outline") => (
    <Button key={a} size="sm" variant={variant} disabled={act.isPending} onClick={() => act.mutate(a)}>{t(`communities.action.${a}`)}</Button>
  );

  return (
    <div className="flex items-center justify-between gap-2 py-2 border-b last:border-0">
      <div className="min-w-0">
        <p className="text-sm font-medium truncate">{m.displayName}</p>
        <p className="text-xs text-muted-foreground">{t("communities.joinedOn", { date: formatDate(m.joinedAt) })}</p>
      </div>
      <div className="flex items-center gap-1.5 flex-wrap justify-end">
        {m.role !== "Member" && <Badge variant={m.role === "Owner" ? "default" : "info"}>{t(`communities.role.${m.role}`)}</Badge>}
        {canActOnTarget && m.status === "Pending" && btn("Approve", "default")}
        {canActOnTarget && m.status === "Banned" && btn("Unban")}
        {canActOnTarget && m.status === "Active" && (
          <>
            {isOwner && m.role === "Member" && btn("Promote")}
            {isOwner && m.role === "Moderator" && btn("Demote")}
            {btn("Ban", "destructive")}
          </>
        )}
        {canActOnTarget && m.status !== "Banned" && btn("Remove")}
      </div>
    </div>
  );
}

export function CommunityDetailPage() {
  const { idOrSlug = "" } = useParams();
  const { t } = useI18n();
  const nav = useNavigate();
  const qc = useQueryClient();
  const [eventOpen, setEventOpen] = useState(false);
  const [pollOpen, setPollOpen] = useState(false);
  const [memberTab, setMemberTab] = useState<CommunityMemberStatus>("Active");

  const community = useQuery({ queryKey: ["communities", "detail", idOrSlug], queryFn: () => communitiesApi.get(idOrSlug) });
  const c = community.data;
  const isActive = c?.myStatus === "Active";
  const isMod = isActive && (c?.myRole === "Moderator" || c?.myRole === "Owner");
  const isOwner = isActive && c?.myRole === "Owner";
  const canView = !!c && (c.isPublic || isActive);

  const members = useQuery({
    queryKey: ["communities", "members", c?.id, memberTab],
    queryFn: () => communitiesApi.members(c!.id, memberTab),
    enabled: canView && (memberTab === "Active" || isMod),
  });
  const events = useQuery({
    queryKey: ["community-events", "community", c?.id],
    queryFn: () => communitiesApi.events(c!.id),
    enabled: canView,
  });

  const polls = useQuery({
    queryKey: ["community-plus", "polls", c?.id],
    queryFn: () => communityPlusApi.polls(c!.id),
    enabled: canView,
  });

  const refreshAll = () => {
    qc.invalidateQueries({ queryKey: ["communities"] });
    qc.invalidateQueries({ queryKey: ["community-events"] });
  };
  const join = useMutation({
    mutationFn: () => communitiesApi.join(c!.id),
    onSuccess: (r) => { toast.success(r.myStatus === "Pending" ? t("communities.requestSent") : t("communities.joinedToast")); refreshAll(); },
  });
  const leave = useMutation({
    mutationFn: () => communitiesApi.leave(c!.id),
    onSuccess: () => { toast.success(t("communities.left")); refreshAll(); nav("/communities"); },
  });

  if (community.isLoading) return <Skeleton className="h-48" aria-busy="true" aria-label={t("common.loading")} />;
  if (!c) return <EmptyState title={t("communities.notFound")} action={<Button asChild><Link to="/communities">{t("common.back")}</Link></Button>} />;

  return (
    <div className="space-y-6">
      <Link to="/communities" className="text-sm text-muted-foreground inline-flex items-center gap-1 hover:text-foreground">
        <ArrowLeft aria-hidden="true" className="h-4 w-4 rtl:rotate-180" /> {t("communities.title")}
      </Link>
      <PageHeader
        title={c.name}
        description={[c.species, c.breed, c.city].filter(Boolean).join(" / ") || undefined}
        icon={c.isPublic ? Users : Lock}
        actions={
          <>
            {!c.myStatus && <Button disabled={join.isPending} onClick={() => join.mutate()}>{c.isPublic ? t("communities.join") : t("communities.requestJoin")}</Button>}
            {c.myStatus === "Pending" && <Badge variant="warning">{t("communities.requestPending")}</Badge>}
            {c.myStatus === "Banned" && <Badge variant="destructive">{t("communities.banned")}</Badge>}
            {isActive && c.myRole !== "Owner" && <Button variant="outline" disabled={leave.isPending} onClick={() => leave.mutate()}>{t("communities.leave")}</Button>}
            {isActive && <Button onClick={() => setEventOpen(true)}><Plus aria-hidden="true" className="h-4 w-4 me-1" />{t("communities.hostEvent")}</Button>}
          </>
        }
      />
      {c.description && <p className="text-sm">{c.description}</p>}
      <p className="text-sm text-muted-foreground flex items-center gap-3">
        <span className="flex items-center gap-1"><Users aria-hidden="true" className="h-4 w-4" />{t("communities.members", { count: formatNumber(c.memberCount) })}</span>
        {c.city && <span className="flex items-center gap-1"><MapPin aria-hidden="true" className="h-4 w-4" />{c.city}</span>}
      </p>

      {!canView ? (
        <EmptyState icon={Lock} title={t("communities.privateTitle")} description={t("communities.privateDesc")} />
      ) : (
        <Tabs defaultValue="events">
          <TabsList>
            <TabsTrigger value="events">{t("communities.events")}</TabsTrigger>
            <TabsTrigger value="polls">{t("poll.tab")}</TabsTrigger>
            <TabsTrigger value="members">{t("communities.membersTab")}</TabsTrigger>
          </TabsList>

          <TabsContent value="events" className="space-y-3">
            {events.isLoading ? <Skeleton className="h-24" aria-busy="true" aria-label={t("common.loading")} /> : events.data && events.data.length > 0
              ? events.data.map((e) => <EventCard key={e.id} event={e} showCommunity={false} />)
              : <EmptyState title={t("communities.noEvents")} description={isActive ? t("communities.hostFirst") : t("communities.joinToHost")} />}
          </TabsContent>

          <TabsContent value="polls" className="space-y-3">
            {isActive && <Button size="sm" onClick={() => setPollOpen(true)}><Plus aria-hidden="true" className="h-4 w-4 me-1" />{t("poll.create")}</Button>}
            {polls.isLoading ? <Skeleton className="h-24" /> : polls.data && polls.data.length > 0
              ? polls.data.map((p) => <PollCard key={p.id} poll={p} canVote={!!isActive} onChanged={() => qc.invalidateQueries({ queryKey: ["community-plus"] })} />)
              : <EmptyState title={t("poll.none")} />}
            <ReputationLeaderboard communityId={c.id} />
          </TabsContent>

          <TabsContent value="members" className="space-y-3">
            {isMod && (
              <div className="flex gap-2" role="group" aria-label={t("communities.membersTab")}>
                {(["Active", "Pending", "Banned"] as CommunityMemberStatus[]).map((s) => (
                  <Button key={s} size="sm" aria-pressed={memberTab === s} variant={memberTab === s ? "default" : "outline"} onClick={() => setMemberTab(s)}>
                    {t(`communities.status.${s}`)}
                  </Button>
                ))}
              </div>
            )}
            <Card>
              <CardContent className="p-4">
                {members.isLoading ? <Skeleton className="h-16" aria-busy="true" aria-label={t("common.loading")} /> : members.data && members.data.length > 0
                  ? members.data.map((m) => (
                      <MemberRow key={m.userId} m={m} communityId={c.id} isOwner={!!isOwner} isMod={!!isMod}
                        onChanged={() => { qc.invalidateQueries({ queryKey: ["communities"] }); }} />
                    ))
                  : <p className="text-sm text-muted-foreground">{t("communities.nobody")}</p>}
              </CardContent>
            </Card>
          </TabsContent>
        </Tabs>
      )}

      <CreateEventDialog communityId={c.id} open={eventOpen} onOpenChange={setEventOpen} />
      <CreatePollDialog communityId={c.id} open={pollOpen} onOpenChange={setPollOpen} onCreated={() => qc.invalidateQueries({ queryKey: ["community-plus"] })} />
    </div>
  );
}
