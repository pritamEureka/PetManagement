import { useQuery } from "@tanstack/react-query";
import { Activity } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { healthPlusApi } from "@/api/carePlans";
import { Section } from "./shared";

export function InsightsCard({ petId }: { petId: string }) {
  const { data } = useQuery({ queryKey: ["pets", petId, "insights"], queryFn: () => healthPlusApi.insights(petId), retry: false });
  if (!data) return null;
  return (
    <Section title="Health insights">
      {data.insights.length === 0 ? (
        <p className="text-sm text-muted-foreground">Nothing unusual spotted in the recorded doses, weights and visits.</p>
      ) : (
        <ul className="space-y-2">
          {data.insights.map((i, n) => (
            <li key={n} className="flex gap-2 text-sm">
              <Activity className="h-4 w-4 mt-0.5 shrink-0 text-amber-500" />
              <div>
                <p className="font-medium">{i.title} {i.metric && <Badge variant={i.severity === "Alert" ? "destructive" : "warning"}>{i.metric}</Badge>}</p>
                <p className="text-muted-foreground">{i.detail}</p>
              </div>
            </li>
          ))}
        </ul>
      )}
      <p className="text-[11px] text-muted-foreground mt-3">{data.disclaimer}</p>
    </Section>
  );
}
