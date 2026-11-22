import { Link } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Hourglass } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { toast } from "@/components/ui/sonner";
import { waitlistApi } from "@/api/vetPlus";

export function MyWaitlistsPage() {
  const qc = useQueryClient();
  const { data, isLoading } = useQuery({ queryKey: ["waitlists", "mine"], queryFn: waitlistApi.mine });
  const leave = useMutation({
    mutationFn: (id: string) => waitlistApi.leave(id),
    onSuccess: () => {
      toast.success("Removed from waitlist");
      qc.invalidateQueries({ queryKey: ["waitlists", "mine"] });
    }
  });

  return (
    <div className="mx-auto max-w-3xl space-y-4 p-4">
      <h1 className="flex items-center gap-2 text-2xl font-semibold">
        <Hourglass className="h-6 w-6" /> My waitlists
      </h1>

      {isLoading && <Skeleton className="h-24 w-full" />}
      {!isLoading && (data?.length ?? 0) === 0 && (
        <Card>
          <CardContent className="p-6 text-sm text-muted-foreground">
            You are not on any waitlists. When a vet is fully booked, use "Join waitlist" on their page.
          </CardContent>
        </Card>
      )}

      {data?.map((w) => (
        <Card key={w.id}>
          <CardHeader className="flex-row items-center justify-between space-y-0">
            <CardTitle className="text-base">{w.doctorName}</CardTitle>
            {w.status === "Notified"
              ? <Badge>A slot opened</Badge>
              : <Badge variant="secondary">Position #{w.position}</Badge>}
          </CardHeader>
          <CardContent className="space-y-3 text-sm">
            <div className="text-muted-foreground">
              {w.preferredFrom || w.preferredTo
                ? `Preferred: ${w.preferredFrom ?? "any date"} to ${w.preferredTo ?? "any date"}`
                : "Preferred: any date"}
              {w.consultationType ? ` · ${w.consultationType}` : ""}
            </div>
            {w.note && <div>{w.note}</div>}
            <div className="flex gap-2">
              {w.status === "Notified" && (
                <Button asChild size="sm">
                  <Link to={`/vets/${w.doctorId}/book`}>Book now</Link>
                </Button>
              )}
              <Button size="sm" variant="outline" disabled={leave.isPending} onClick={() => leave.mutate(w.id)}>
                Leave waitlist
              </Button>
            </div>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
