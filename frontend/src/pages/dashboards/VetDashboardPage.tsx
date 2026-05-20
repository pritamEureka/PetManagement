import { Stethoscope, Calendar, FileText, Star } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Badge } from "@/components/ui/badge";

export function VetDashboardPage() {
  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-bold flex items-center gap-2"><Stethoscope className="h-6 w-6 text-primary" /> Vet dashboard</h1>
        <p className="text-sm text-muted-foreground">Today's appointments, pending follow-ups, and revenue snapshot.</p>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 gap-3">
        <StatTile label="Today" value="3" Icon={Calendar} />
        <StatTile label="This week" value="14" Icon={Calendar} />
        <StatTile label="Prescriptions issued" value="42" Icon={FileText} />
        <StatTile label="Rating" value="4.8" Icon={Star} />
      </div>

      <Tabs defaultValue="today">
        <TabsList>
          <TabsTrigger value="today">Today</TabsTrigger>
          <TabsTrigger value="upcoming">Upcoming</TabsTrigger>
          <TabsTrigger value="completed">Completed</TabsTrigger>
        </TabsList>
        <TabsContent value="today">
          <Card><CardContent className="py-8 text-center text-muted-foreground text-sm">
            No appointments scheduled for today.
          </CardContent></Card>
        </TabsContent>
        <TabsContent value="upcoming">
          <Card><CardContent className="py-8 text-center text-muted-foreground text-sm">
            Upcoming appointments will appear here.
          </CardContent></Card>
        </TabsContent>
        <TabsContent value="completed">
          <Card><CardContent className="py-8 text-center text-muted-foreground text-sm">
            History — last 30 days.
          </CardContent></Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}

function StatTile({ label, value, Icon }: { label: string; value: string; Icon: typeof Calendar }) {
  return (
    <Card>
      <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-muted-foreground flex items-center gap-2"><Icon className="h-4 w-4" /> {label}</CardTitle></CardHeader>
      <CardContent className="text-3xl font-bold">{value}</CardContent>
    </Card>
  );
}
