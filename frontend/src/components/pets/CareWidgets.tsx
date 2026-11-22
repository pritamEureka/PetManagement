import { Link } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { CalendarClock, Cake, Check, Pill, Syringe, Baby } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { toast } from "@/components/ui/sonner";
import { QueryBoundary } from "@/components/common/QueryBoundary";
import { petsApi, type UpcomingCareItem } from "@/api/pets";
import { useI18n } from "@/lib/i18n";
import { formatDate, formatNumber } from "@/lib/format";

const kindIcon: Record<UpcomingCareItem["kind"], typeof Syringe> = {
  vaccination: Syringe, medication: Pill, birthday: Cake, breeding: Baby,
};

/** Vaccines due / overdue, birthdays, course end dates and expected births for the next 30 days. */
export function UpcomingCareWidget() {
  const { t } = useI18n();
  const query = useQuery({ queryKey: ["pets", "upcoming-care"], queryFn: () => petsApi.upcomingCare(30) });

  return (
    <Card>
      <CardHeader className="pb-3">
        <CardTitle className="text-base flex items-center gap-2"><CalendarClock aria-hidden="true" className="h-4 w-4 text-primary" /> {t("care.upcoming")}</CardTitle>
      </CardHeader>
      <CardContent>
        <QueryBoundary
          query={query}
          skeletonRows={3}
          empty={<p className="text-sm text-muted-foreground">{t("care.nothingDue")} 🎉</p>}
        >
          {(items) => (
            <ul className="space-y-2">
              {items.slice(0, 8).map((i, idx) => {
                const Icon = kindIcon[i.kind];
                return (
                  <li key={idx}>
                    <Link to={i.url} className="flex items-center justify-between gap-2 rounded-md p-1.5 -mx-1.5 hover:bg-muted/60">
                      <span className="flex items-center gap-2 text-sm min-w-0">
                        <Icon aria-hidden="true" className="h-4 w-4 text-muted-foreground shrink-0" />
                        <span className="truncate"><span className="font-medium">{i.petName}</span> · {i.title}</span>
                      </span>
                      {i.overdue
                        ? <Badge variant="destructive">{t("care.overdue")}</Badge>
                        : <span className="text-xs text-muted-foreground shrink-0">{formatDate(i.dueOn)}</span>}
                    </Link>
                  </li>
                );
              })}
            </ul>
          )}
        </QueryBoundary>
      </CardContent>
    </Card>
  );
}

/** Today's medication doses across every pet the user cares for, with one-tap logging. */
export function TodayDosesWidget() {
  const { t } = useI18n();
  const qc = useQueryClient();
  const { data: doses = [] } = useQuery({ queryKey: ["pets", "doses-today"], queryFn: petsApi.medications.today });
  if (doses.length === 0) return null;

  async function give(d: (typeof doses)[number]) {
    await petsApi.medications.logDose(d.petId, d.medicationId, { scheduledFor: d.scheduledFor, skipped: false });
    toast.success(t("care.doseLogged", { pet: d.petName, drug: d.drugName }));
    qc.invalidateQueries({ queryKey: ["pets", "doses-today"] });
  }

  const pending = doses.filter((d) => d.status === "due" || d.status === "upcoming").length;
  return (
    <Card>
      <CardHeader className="pb-3 flex flex-row items-center justify-between space-y-0">
        <CardTitle className="text-base flex items-center gap-2"><Pill aria-hidden="true" className="h-4 w-4 text-primary" /> {t("care.todayDoses")}</CardTitle>
        <Badge variant={pending ? "warning" : "success"}>{pending ? t("care.toGo", { count: formatNumber(pending) }) : t("care.allDone")}</Badge>
      </CardHeader>
      <CardContent>
        <ul className="space-y-2">
          {doses.map((d) => (
            <li key={`${d.medicationId}-${d.scheduledFor}`} className="flex items-center justify-between gap-2 text-sm">
              <span className="min-w-0 truncate">
                <span className="text-muted-foreground">{new Date(d.scheduledFor).toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit" })}</span>{" "}
                <span className="font-medium">{d.petName}</span> · {d.drugName}
              </span>
              {d.status === "taken" ? <Check aria-label={t("care.taken")} className="h-4 w-4 text-emerald-600" />
                : d.status === "skipped" ? <span className="text-xs text-muted-foreground">{t("care.skipped")}</span>
                : <Button size="sm" variant={d.status === "due" ? "default" : "outline"} className="h-7" onClick={() => give(d)}>{t("care.given")}</Button>}
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}
