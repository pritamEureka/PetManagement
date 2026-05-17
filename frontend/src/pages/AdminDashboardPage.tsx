import { useQuery } from "@tanstack/react-query";
import {
  Users, UserPlus, Stethoscope, ShoppingBag, HeartHandshake, MessageSquare,
  Flag, Calendar, Package, DollarSign, Percent, Store
} from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { StatCard } from "@/components/admin/StatCard";
import { AreaSeriesChart, DonutChart, HBarChart, LineSeriesChart } from "@/components/admin/Charts";
import { adminApi } from "@/api/adminV2";

export function AdminDashboardPage() {
  const { data: overview, isLoading } = useQuery({
    queryKey: ["admin-overview"],
    queryFn: () => adminApi.overview(),
    refetchInterval: 60_000
  });
  const { data: series } = useQuery({
    queryKey: ["admin-series", 30],
    queryFn: () => adminApi.series(30)
  });
  const { data: top } = useQuery({
    queryKey: ["admin-top", 30],
    queryFn: () => adminApi.top(30)
  });

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Overview</h1>
        <p className="text-sm text-muted-foreground">Operational snapshot of the platform.</p>
      </div>

      {/* Headline KPIs */}
      {isLoading || !overview ? (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
          {Array.from({ length: 8 }).map((_, i) => <Skeleton key={i} className="h-24" />)}
        </div>
      ) : (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
          <StatCard label="Total users"   value={overview.totalUsers}    icon={Users}      hint={`${overview.activeUsers} active`} />
          <StatCard label="New today"     value={overview.newUsersToday} icon={UserPlus}   tone="success" />
          <StatCard label="Active stores" value={overview.activeStores}  icon={Store} />
          <StatCard label="Active vets"   value={overview.activeDoctors} icon={Stethoscope} />

          <StatCard label="Pending vets"      value={overview.pendingDoctorApprovals}  icon={Stethoscope}    tone="warning" />
          <StatCard label="Pending stores"    value={overview.pendingStoreApprovals}    icon={ShoppingBag}    tone="warning" />
          <StatCard label="Pending adoptions" value={overview.pendingAdoptionListings}  icon={HeartHandshake} tone="warning" />
          <StatCard label="Open reports"      value={overview.openReports}              icon={Flag}           tone="destructive" />

          <StatCard label="New posts today"    value={overview.newFeedPostsToday} icon={MessageSquare} />
          <StatCard label="Appointments today" value={overview.appointmentsToday} icon={Calendar} />
          <StatCard label="Total orders"       value={overview.totalOrders}       icon={Package} />
          <StatCard label="Total appointments" value={overview.totalAppointments} icon={Calendar} />

          <StatCard label="Total revenue"     value={`$${overview.totalRevenue.toLocaleString(undefined, { maximumFractionDigits: 0 })}`}    icon={DollarSign} tone="success" />
          <StatCard label="Commission earned" value={`$${overview.commissionEarned.toLocaleString(undefined, { maximumFractionDigits: 0 })}`} icon={Percent}    tone="success" />
        </div>
      )}

      {/* Time-series charts */}
      <div className="grid lg:grid-cols-3 gap-4">
        <Card className="lg:col-span-2">
          <CardHeader><CardTitle className="text-base">Last 30 days</CardTitle></CardHeader>
          <CardContent>
            {series && series.length > 0 ? (
              <LineSeriesChart
                data={series}
                xKey="date"
                series={[
                  { key: "users",  label: "New users" },
                  { key: "orders", label: "Orders" }
                ]}
              />
            ) : <p className="text-sm text-muted-foreground py-12 text-center">No activity yet.</p>}
          </CardContent>
        </Card>

        <Card>
          <CardHeader><CardTitle className="text-base">Daily revenue</CardTitle></CardHeader>
          <CardContent>
            {series && series.length > 0 ? (
              <AreaSeriesChart data={series} xKey="date" dataKey="revenue" label="Revenue ($)" />
            ) : <p className="text-sm text-muted-foreground py-12 text-center">No revenue yet.</p>}
          </CardContent>
        </Card>
      </div>

      {/* Top breakdowns */}
      <div className="grid lg:grid-cols-2 gap-4">
        <Card>
          <CardHeader><CardTitle className="text-base">Top animal types (30d)</CardTitle></CardHeader>
          <CardContent>
            {top?.animals && top.animals.length > 0 ? (
              <DonutChart data={top.animals} dataKey="listings" nameKey="type" />
            ) : <p className="text-sm text-muted-foreground py-12 text-center">No adoption activity.</p>}
          </CardContent>
        </Card>

        <Card>
          <CardHeader><CardTitle className="text-base">Top products (30d)</CardTitle></CardHeader>
          <CardContent>
            {top?.products && top.products.length > 0 ? (
              <HBarChart data={top.products} labelKey="name" dataKey="revenue" />
            ) : <p className="text-sm text-muted-foreground py-12 text-center">No orders yet.</p>}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
