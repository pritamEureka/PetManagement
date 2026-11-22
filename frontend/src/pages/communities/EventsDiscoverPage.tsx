import { useState } from "react";
import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { ArrowLeft, CalendarDays, LocateFixed, Search } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { toast } from "@/components/ui/sonner";
import { EventCard } from "@/components/communities/EventCard";
import { nativeSelectCls } from "@/components/communities/CommunityDialogs";
import { useI18n } from "@/lib/i18n";
import { formatNumber } from "@/lib/format";
import { communityEventsApi, EVENT_TYPES, type CommunityEventType } from "@/api/communities";

export function EventsDiscoverPage() {
  const { t } = useI18n();
  const [city, setCity] = useState("");
  const [type, setType] = useState<CommunityEventType | "">("");
  const [radius, setRadius] = useState(25);
  const [coords, setCoords] = useState<{ lat: number; lng: number } | null>(null);

  const { data, isLoading } = useQuery({
    queryKey: ["community-events", "discover", city, type, radius, coords],
    queryFn: () => communityEventsApi.discover({
      city: coords ? undefined : city.trim() || undefined,
      type: type || undefined,
      lat: coords?.lat, lng: coords?.lng, radiusKm: coords ? radius : undefined,
      pageSize: 30,
    }),
  });

  const locate = () => {
    if (!navigator.geolocation) { toast.error(t("events.noGeo")); return; }
    navigator.geolocation.getCurrentPosition(
      (p) => setCoords({ lat: p.coords.latitude, lng: p.coords.longitude }),
      () => toast.error(t("events.locateFail")),
    );
  };

  return (
    <div className="space-y-6">
      <Link to="/communities" className="text-sm text-muted-foreground inline-flex items-center gap-1 hover:text-foreground">
        <ArrowLeft aria-hidden="true" className="h-4 w-4 rtl:rotate-180" /> {t("communities.title")}
      </Link>
      <PageHeader title={t("communities.discoverEvents")} description={t("events.subtitle")} icon={CalendarDays} />

      <div className="flex gap-2 flex-wrap items-center">
        <div className="relative flex-1 min-w-[180px]">
          <Search aria-hidden="true" className="h-4 w-4 absolute start-3 top-3 text-muted-foreground" />
          <Input className="ps-9" aria-label={t("events.city")} placeholder={t("events.city")} value={city} disabled={!!coords} onChange={(e) => setCity(e.target.value)} />
        </div>
        <select aria-label={t("events.type")} className={`${nativeSelectCls} w-auto`} value={type} onChange={(e) => setType(e.target.value as CommunityEventType | "")}>
          <option value="">{t("events.allTypes")}</option>
          {EVENT_TYPES.map((et) => <option key={et.value} value={et.value}>{t(`events.typeLabel.${et.value}`)}</option>)}
        </select>
        {coords ? (
          <>
            <select aria-label={t("events.radius")} className={`${nativeSelectCls} w-auto`} value={radius} onChange={(e) => setRadius(Number(e.target.value))}>
              {[5, 10, 25, 50, 100].map((r) => <option key={r} value={r}>{t("events.withinKm", { km: formatNumber(r) })}</option>)}
            </select>
            <Button variant="outline" onClick={() => setCoords(null)}>{t("events.clearLocation")}</Button>
          </>
        ) : (
          <Button variant="outline" onClick={locate}><LocateFixed aria-hidden="true" className="h-4 w-4 me-1" />{t("events.nearMe")}</Button>
        )}
      </div>

      {isLoading ? (
        <div className="grid gap-3 md:grid-cols-2" aria-busy="true" role="status" aria-label={t("common.loading")}>{[0, 1].map((i) => <Skeleton key={i} className="h-36" />)}</div>
      ) : data && data.items.length > 0 ? (
        <div className="grid gap-3 md:grid-cols-2">
          {data.items.map((e) => <EventCard key={e.id} event={e} />)}
        </div>
      ) : (
        <EmptyState icon={CalendarDays} title={t("events.empty")} description={t("events.emptyDesc")} />
      )}
    </div>
  );
}
