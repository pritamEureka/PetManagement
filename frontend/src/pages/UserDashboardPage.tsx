import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { PawPrint, Calendar, ShoppingBag, HeartHandshake, MessageSquare, Stethoscope, Plus, Bell, ArrowRight, Scissors, Search } from "lucide-react";
import { UpcomingCareWidget, TodayDosesWidget } from "@/components/pets/CareWidgets";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Separator } from "@/components/ui/separator";
import { PageHeader } from "@/components/common/PageHeader";
import { StatTile } from "@/components/common/StatTile";
import { EmptyState } from "@/components/common/EmptyState";
import { useI18n } from "@/lib/i18n";
import { formatCurrency, formatDate } from "@/lib/format";
import { useAuthStore } from "@/store/authStore";
import { petsApi } from "@/api/pets";
import { ordersV2Api } from "@/api/marketplace";

export function UserDashboardPage() {
  const { t } = useI18n();
  const me = useAuthStore((s) => s.user);
  const { data: pets, isLoading: petsLoading } = useQuery({ queryKey: ["pets", "mine"], queryFn: petsApi.mine });
  const { data: orders } = useQuery({ queryKey: ["orders", "mine"], queryFn: () => ordersV2Api.mine(1, 50) });

  const firstName = me?.displayName?.split(" ")[0] ?? t("dash.friend");

  return (
    <div className="space-y-5">
      <PageHeader
        title={`${t("dash.welcome", { name: firstName })} 👋`}
        description={t("dash.subtitle")}
        actions={<Button variant="outline" size="icon" title={t("nav.notifications")} aria-label={t("nav.notifications")}><Bell aria-hidden="true" className="h-4 w-4" /></Button>}
      />

      <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
        <StatTile label={t("nav.pets")} value={pets?.length ?? 0} icon={PawPrint} />
        <StatTile label={t("dash.upcomingVisits")} value={0} icon={Calendar} hint={t("dash.soon")} />
        <StatTile label={t("dash.orders")} value={orders?.total ?? 0} icon={ShoppingBag} />
        <StatTile label={t("dash.savedAdoptions")} value={0} icon={HeartHandshake} />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
        {/* My pets — span 2 */}
        <Card className="lg:col-span-2">
          <CardHeader className="flex flex-row items-center justify-between space-y-0">
            <CardTitle className="text-base flex items-center gap-2"><PawPrint aria-hidden="true" className="h-4 w-4 text-primary" /> {t("nav.pets")}</CardTitle>
            <Button size="sm" variant="ghost" asChild>
              <Link to="/pets">{t("dash.viewAll")} <ArrowRight aria-hidden="true" className="h-3 w-3 ms-1 rtl:rotate-180" /></Link>
            </Button>
          </CardHeader>
          <CardContent>
            {petsLoading ? (
              <div className="grid grid-cols-2 md:grid-cols-3 gap-2">{[...Array(3)].map((_, i) => <Skeleton key={i} className="h-28" />)}</div>
            ) : !pets || pets.length === 0 ? (
              <EmptyState icon={PawPrint} title={t("dash.noPets")} description={t("dash.noPetsDesc")}
                action={<Button asChild size="sm"><Link to="/pets"><Plus aria-hidden="true" className="h-3.5 w-3.5 me-1" /> {t("dash.addPet")}</Link></Button>} />
            ) : (
              <div className="grid grid-cols-2 md:grid-cols-3 gap-2">
                {pets.slice(0, 6).map((p) => (
                  <Link key={p.id} to={`/pets/${p.id}`} className="rounded-lg border hover:bg-background/80 dark:hover:bg-background/60 transition-colors p-3 flex items-center gap-3">
                    <Avatar>
                      <AvatarImage src={p.primaryPhotoUrl ?? undefined} />
                      <AvatarFallback>{p.name[0]}</AvatarFallback>
                    </Avatar>
                    <div className="min-w-0">
                      <p className="font-medium truncate">{p.name}</p>
                      <p className="text-[10px] text-muted-foreground truncate">{p.animalType}{p.breed ? ` · ${p.breed}` : ""}</p>
                    </div>
                  </Link>
                ))}
              </div>
            )}
          </CardContent>
        </Card>

        {/* Quick actions */}
        <Card>
          <CardHeader><CardTitle className="text-base">{t("dash.quickActions")}</CardTitle></CardHeader>
          <CardContent className="space-y-2">
            <QuickAction to="/vets" icon={Stethoscope} title={t("dash.qa.vet")} hint={t("dash.qa.vetHint")} />
            <QuickAction to="/adoption" icon={HeartHandshake} title={t("dash.qa.adopt")} hint={t("dash.qa.adoptHint")} />
            <QuickAction to="/store" icon={ShoppingBag} title={t("dash.qa.shop")} hint={t("dash.qa.shopHint")} />
            <QuickAction to="/messages" icon={MessageSquare} title={t("dash.qa.messages")} hint={t("dash.qa.messagesHint")} />
            <QuickAction to="/services" icon={Scissors} title={t("dash.qa.grooming")} hint={t("dash.qa.groomingHint")} />
            <QuickAction to="/lost-found" icon={Search} title={t("dash.qa.lostFound")} hint={t("dash.qa.lostFoundHint")} />
          </CardContent>
        </Card>
      </div>

      {/* Pet care */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        <UpcomingCareWidget />
        <TodayDosesWidget />
      </div>

      {/* Recent orders */}
      <Card>
        <CardHeader className="flex flex-row items-center justify-between space-y-0">
          <CardTitle className="text-base">{t("dash.recentOrders")}</CardTitle>
          <Button size="sm" variant="ghost" asChild>
            <Link to="/orders">{t("dash.allOrders")} <ArrowRight aria-hidden="true" className="h-3 w-3 ms-1 rtl:rotate-180" /></Link>
          </Button>
        </CardHeader>
        <CardContent>
          {!orders || orders.items.length === 0 ? (
            <p className="text-sm text-muted-foreground text-center py-6">{t("dash.noOrders")}</p>
          ) : (
            <div className="divide-y">
              {orders.items.slice(0, 3).map((o) => (
                <div key={o.id} className="py-3 flex flex-col sm:flex-row sm:items-center justify-between gap-1">
                  <div>
                    <p className="text-sm font-medium">{o.orderNumber}</p>
                    <p className="text-xs text-muted-foreground">{formatDate(o.createdAt)}</p>
                  </div>
                  <div className="flex items-center gap-2">
                    <Badge variant="outline">{o.status}</Badge>
                    <span className="text-sm font-semibold">{formatCurrency(o.total)}</span>
                  </div>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

function QuickAction({ to, icon: Icon, title, hint }: { to: string; icon: any; title: string; hint: string }) {
  return (
    <Link to={to} className="flex items-center gap-3 rounded-md border p-3 hover:bg-background/80 dark:hover:bg-background/60 transition-colors">
      <div className="h-9 w-9 rounded-md bg-primary/10 flex items-center justify-center"><Icon aria-hidden="true" className="h-4 w-4 text-primary" /></div>
      <div className="flex-1 min-w-0">
        <p className="text-sm font-medium">{title}</p>
        <p className="text-xs text-muted-foreground">{hint}</p>
      </div>
      <ArrowRight aria-hidden="true" className="h-4 w-4 text-muted-foreground rtl:rotate-180" />
    </Link>
  );
}
