import { useMutation, useQueryClient } from "@tanstack/react-query";
import { CalendarDays, MapPin, Users } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { toast } from "@/components/ui/sonner";
import { communityEventsApi, EVENT_TYPES, type CommunityEvent } from "@/api/communities";
import { Link } from "react-router-dom";
import { useI18n } from "@/lib/i18n";
import { formatDate, formatDateTime, formatNumber } from "@/lib/format";

export function EventCard({ event, showCommunity = true }: { event: CommunityEvent; showCommunity?: boolean }) {
  const { t, locale } = useI18n();
  const qc = useQueryClient();
  const refresh = () => qc.invalidateQueries({ queryKey: ["community-events"] });

  const rsvp = useMutation({
    mutationFn: () => communityEventsApi.rsvp(event.id),
    onSuccess: (e) => { toast.success(e.myRsvp === "Waitlisted" ? t("ecard.waitlistedToast") : t("ecard.goingToast")); refresh(); },
  });
  const cancel = useMutation({
    mutationFn: () => communityEventsApi.cancelRsvp(event.id),
    onSuccess: () => { toast.success(t("ecard.rsvpCancelled")); refresh(); },
  });

  const active = event.myRsvp === "Going" || event.myRsvp === "Waitlisted";
  const full = event.capacity != null && event.goingCount >= event.capacity;
  const known = EVENT_TYPES.some((et) => et.value === event.type);
  const typeLabel = known ? t(`events.typeLabel.${event.type}`) : event.type;

  return (
    <Card>
      <CardContent className="p-4 space-y-2">
        <div className="flex items-start justify-between gap-2 flex-wrap">
          <div>
            <h3 className="font-semibold">{event.title}</h3>
            {showCommunity && (
              <Link to={`/communities/${event.communityId}`} className="text-xs text-primary hover:underline">{event.communityName}</Link>
            )}
          </div>
          <div className="flex gap-1.5">
            <Badge variant="secondary">{typeLabel}</Badge>
            {event.isCancelled && <Badge variant="destructive">{t("ecard.eventCancelled")}</Badge>}
            {event.myRsvp && event.myRsvp !== "Cancelled" && <Badge variant={event.myRsvp === "Going" ? "success" : "warning"}>{t(`ecard.status.${event.myRsvp}`)}</Badge>}
          </div>
        </div>
        <p className="text-sm text-muted-foreground flex items-center gap-1.5">
          <CalendarDays className="h-4 w-4" />
          {formatDateTime(event.startsAt, locale)} - {formatDate(event.endsAt, { hour: "2-digit", minute: "2-digit" }, locale)}
        </p>
        {(event.venue || event.city) && (
          <p className="text-sm text-muted-foreground flex items-center gap-1.5">
            <MapPin className="h-4 w-4" />
            {[event.venue, event.city].filter(Boolean).join(", ")}
            {event.distanceKm != null && <span> {t("ecard.awayKm", { km: formatNumber(event.distanceKm, undefined, locale) })}</span>}
          </p>
        )}
        {event.description && <p className="text-sm">{event.description}</p>}
        <div className="flex items-center justify-between gap-2 pt-1">
          <p className="text-sm text-muted-foreground flex items-center gap-1.5">
            <Users className="h-4 w-4" />
            {event.capacity != null
              ? t("ecard.goingOfCap", { count: formatNumber(event.goingCount, undefined, locale), cap: formatNumber(event.capacity, undefined, locale) })
              : t("ecard.goingCount", { count: formatNumber(event.goingCount, undefined, locale) })}
            {event.waitlistCount > 0 && " · " + t("ecard.waitlistedCount", { count: formatNumber(event.waitlistCount, undefined, locale) })}
          </p>
          {!event.isCancelled && (active ? (
            <Button size="sm" variant="outline" disabled={cancel.isPending} onClick={() => cancel.mutate()}>{t("ecard.cancelRsvp")}</Button>
          ) : (
            <Button size="sm" disabled={rsvp.isPending} onClick={() => rsvp.mutate()}>{full ? t("ecard.joinWaitlist") : t("ecard.rsvp")}</Button>
          ))}
        </div>
      </CardContent>
    </Card>
  );
}
