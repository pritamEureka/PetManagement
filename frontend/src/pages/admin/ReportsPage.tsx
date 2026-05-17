import { TrendingUp, Users, DollarSign, PawPrint } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

export function ReportsPage() {
  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-bold">Reports &amp; analytics</h1>
        <p className="text-sm text-muted-foreground">Platform health at a glance.</p>
      </div>

      <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
        <Tile label="MAU"          value="—" Icon={Users} />
        <Tile label="New signups"  value="—" Icon={Users} />
        <Tile label="GMV (30d)"    value="—" Icon={DollarSign} />
        <Tile label="Pets onboarded" value="—" Icon={PawPrint} />
      </div>

      <Card>
        <CardHeader><CardTitle className="flex items-center gap-2"><TrendingUp className="h-4 w-4" /> Activity</CardTitle></CardHeader>
        <CardContent className="py-12 text-center text-muted-foreground text-sm">
          Charts will mount here when the analytics endpoint is exposed.
        </CardContent>
      </Card>
    </div>
  );
}

function Tile({ label, value, Icon }: { label: string; value: string; Icon: typeof Users }) {
  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-medium text-muted-foreground flex items-center gap-2"><Icon className="h-4 w-4" /> {label}</CardTitle>
      </CardHeader>
      <CardContent className="text-3xl font-bold">{value}</CardContent>
    </Card>
  );
}
