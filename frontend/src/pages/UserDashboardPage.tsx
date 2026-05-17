import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { PawPrint, Calendar, ShoppingBag, HeartHandshake, MessageSquare, Stethoscope, Plus, Bell, ArrowRight } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Separator } from "@/components/ui/separator";
import { PageHeader } from "@/components/common/PageHeader";
import { StatTile } from "@/components/common/StatTile";
import { EmptyState } from "@/components/common/EmptyState";
import { useAuthStore } from "@/store/authStore";
import { petsApi } from "@/api/pets";
import { ordersV2Api } from "@/api/marketplace";

export function UserDashboardPage() {
  const me = useAuthStore((s) => s.user);
  const { data: pets, isLoading: petsLoading } = useQuery({ queryKey: ["pets", "mine"], queryFn: petsApi.mine });
  const { data: orders } = useQuery({ queryKey: ["orders", "mine"], queryFn: () => ordersV2Api.mine(1, 50) });

  const firstName = me?.displayName?.split(" ")[0] ?? "friend";

  return (
    <div className="space-y-5">
      <PageHeader
        title={`Welcome back, ${firstName} 👋`}
        description="Your pets, appointments, and orders at a glance."
        actions={<Button variant="outline" size="icon" title="Notifications"><Bell className="h-4 w-4" /></Button>}
      />

      <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
        <StatTile label="My pets" value={pets?.length ?? 0} icon={PawPrint} />
        <StatTile label="Upcoming visits" value={0} icon={Calendar} hint="Soon" />
        <StatTile label="Orders" value={orders?.total ?? 0} icon={ShoppingBag} />
        <StatTile label="Saved adoptions" value={0} icon={HeartHandshake} />
      </div>

      <div className="grid lg:grid-cols-3 gap-4">
        {/* My pets — span 2 */}
        <Card className="lg:col-span-2">
          <CardHeader className="flex flex-row items-center justify-between space-y-0">
            <CardTitle className="text-base flex items-center gap-2"><PawPrint className="h-4 w-4 text-primary" /> My pets</CardTitle>
            <Button size="sm" variant="ghost" asChild>
              <Link to="/pets">View all <ArrowRight className="h-3 w-3 ml-1" /></Link>
            </Button>
          </CardHeader>
          <CardContent>
            {petsLoading ? (
              <div className="grid grid-cols-2 md:grid-cols-3 gap-2">{[...Array(3)].map((_, i) => <Skeleton key={i} className="h-28" />)}</div>
            ) : !pets || pets.length === 0 ? (
              <EmptyState icon={PawPrint} title="No pets yet" description="Add your first pet to start tracking health records."
                action={<Button asChild size="sm"><Link to="/pets"><Plus className="h-3.5 w-3.5 mr-1" /> Add pet</Link></Button>} />
            ) : (
              <div className="grid grid-cols-2 md:grid-cols-3 gap-2">
                {pets.slice(0, 6).map((p) => (
                  <Link key={p.id} to={`/pets/${p.id}`} className="rounded-lg border hover:bg-accent transition-colors p-3 flex items-center gap-3">
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
          <CardHeader><CardTitle className="text-base">Quick actions</CardTitle></CardHeader>
          <CardContent className="space-y-2">
            <QuickAction to="/vets" icon={Stethoscope} title="Find a vet" hint="Book online or in-clinic" />
            <QuickAction to="/adoption" icon={HeartHandshake} title="Browse adoptions" hint="Pets near you" />
            <QuickAction to="/store" icon={ShoppingBag} title="Shop the marketplace" hint="Food, accessories, more" />
            <QuickAction to="/messages" icon={MessageSquare} title="Open messages" hint="Talk to vets & sellers" />
          </CardContent>
        </Card>
      </div>

      {/* Recent orders */}
      <Card>
        <CardHeader className="flex flex-row items-center justify-between space-y-0">
          <CardTitle className="text-base">Recent orders</CardTitle>
          <Button size="sm" variant="ghost" asChild>
            <Link to="/orders">All orders <ArrowRight className="h-3 w-3 ml-1" /></Link>
          </Button>
        </CardHeader>
        <CardContent>
          {!orders || orders.items.length === 0 ? (
            <p className="text-sm text-muted-foreground text-center py-6">No orders yet.</p>
          ) : (
            <div className="divide-y">
              {orders.items.slice(0, 3).map((o) => (
                <div key={o.id} className="py-3 flex items-center justify-between">
                  <div>
                    <p className="text-sm font-medium">{o.orderNumber}</p>
                    <p className="text-xs text-muted-foreground">{new Date(o.createdAt).toLocaleDateString()}</p>
                  </div>
                  <div className="flex items-center gap-2">
                    <Badge variant="outline">{o.status}</Badge>
                    <span className="text-sm font-semibold">${o.total.toFixed(2)}</span>
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
    <Link to={to} className="flex items-center gap-3 rounded-md border p-3 hover:bg-accent transition-colors">
      <div className="h-9 w-9 rounded-md bg-primary/10 flex items-center justify-center"><Icon className="h-4 w-4 text-primary" /></div>
      <div className="flex-1 min-w-0">
        <p className="text-sm font-medium">{title}</p>
        <p className="text-xs text-muted-foreground">{hint}</p>
      </div>
      <ArrowRight className="h-4 w-4 text-muted-foreground" />
    </Link>
  );
}
