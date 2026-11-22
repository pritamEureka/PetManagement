import { Check } from "lucide-react";
import type { AdoptionStage, StageEvent } from "@/api/adoption";
import { cn } from "@/lib/utils";

const FLOW: AdoptionStage[] = ["Submitted", "ReferenceCheck", "HomeVisit", "Decision", "Adopted", "FollowUp"];
const LABEL: Record<AdoptionStage, string> = {
  Submitted: "Submitted", ReferenceCheck: "References", HomeVisit: "Home visit", Decision: "Decision",
  Adopted: "Adopted", FollowUp: "Follow-up", Declined: "Declined", Withdrawn: "Withdrawn"
};

export function AdoptionStageTracker({ stage, history = [] }: { stage: AdoptionStage; history?: StageEvent[] }) {
  if (stage === "Declined" || stage === "Withdrawn") {
    return <p className="text-sm font-medium text-destructive">{LABEL[stage]}</p>;
  }
  const idx = FLOW.indexOf(stage);
  const entered = (s: AdoptionStage) => history.find((h) => h.stage === s)?.enteredAt;
  return (
    <ol className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs" aria-label="Application progress">
      {FLOW.map((s, i) => (
        <li key={s} className="flex items-center gap-1" title={entered(s) ? new Date(entered(s)!).toLocaleString() : undefined}>
          <span className={cn("flex h-5 w-5 items-center justify-center rounded-full border",
            i < idx ? "bg-primary text-primary-foreground" : i === idx ? "border-primary text-primary font-semibold" : "text-muted-foreground")}>
            {i < idx ? <Check className="h-3 w-3" /> : i + 1}
          </span>
          <span className={i === idx ? "font-semibold" : "text-muted-foreground"}>{LABEL[s]}</span>
          {i < FLOW.length - 1 && <span className="text-muted-foreground">›</span>}
        </li>
      ))}
    </ol>
  );
}
