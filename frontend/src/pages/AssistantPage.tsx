import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { MessageSquarePlus, Sparkles, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { PageHeader } from "@/components/common/PageHeader";
import { AssistantPanel } from "@/components/assistant/AssistantPanel";
import { assistantApi } from "@/api/assistant";
import { cn } from "@/lib/utils";

export function AssistantPage() {
  const qc = useQueryClient();
  const [active, setActive] = useState<string | null>(null);

  const { data: conversations = [] } = useQuery({
    queryKey: ["assistant-conversations"],
    queryFn: assistantApi.list
  });

  const remove = useMutation({
    mutationFn: (id: string) => assistantApi.remove(id),
    onSuccess: (_d, id) => {
      if (active === id) setActive(null);
      qc.invalidateQueries({ queryKey: ["assistant-conversations"] });
    }
  });

  return (
    <div className="space-y-6">
      <PageHeader title="Care Assistant" icon={Sparkles}
        description="Grounded answers from your pet records, our care guides, the store and vet directory." />

      <div className="grid lg:grid-cols-[280px_1fr] gap-4">
        <Card>
          <CardHeader className="flex-row items-center justify-between space-y-0">
            <CardTitle className="text-base">Conversations</CardTitle>
            <Button size="sm" variant="outline" onClick={() => setActive(null)} aria-label="New conversation">
              <MessageSquarePlus className="h-4 w-4" />
            </Button>
          </CardHeader>
          <CardContent className="space-y-1">
            {conversations.length === 0 && <p className="text-sm text-muted-foreground">No conversations yet.</p>}
            {conversations.map(c => (
              <div key={c.id} className={cn("flex items-center gap-1 rounded-md px-2 py-1.5 text-sm hover:bg-accent",
                active === c.id && "bg-accent")}>
                <button type="button" className="flex-1 text-left truncate" onClick={() => setActive(c.id)}>
                  {c.hasEscalation && <span className="text-destructive font-medium">! </span>}{c.title}
                </button>
                <button type="button" aria-label={`Delete ${c.title}`} className="opacity-60 hover:opacity-100"
                  onClick={() => remove.mutate(c.id)}>
                  <Trash2 className="h-4 w-4" />
                </button>
              </div>
            ))}
          </CardContent>
        </Card>

        <AssistantPanel conversationId={active} onConversationChange={setActive} />
      </div>
    </div>
  );
}
