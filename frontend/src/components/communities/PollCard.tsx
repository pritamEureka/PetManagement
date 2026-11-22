import { useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { toast } from "@/components/ui/sonner";
import { useI18n } from "@/lib/i18n";
import { communityPlusApi, type Poll } from "@/api/socialPlus";

export function PollCard({ poll, canVote, onChanged }: { poll: Poll; canVote: boolean; onChanged: () => void }) {
  const { t } = useI18n();
  const [sel, setSel] = useState<string[]>(poll.options.filter((o) => o.voted).map((o) => o.id));
  const voting = !poll.isClosed && canVote && !poll.hasVoted;

  const vote = useMutation({
    mutationFn: () => communityPlusApi.vote(poll.id, sel),
    onSuccess: () => { toast.success(t("poll.voted")); onChanged(); },
  });
  const close = useMutation({ mutationFn: () => communityPlusApi.close(poll.id), onSuccess: onChanged });
  const del = useMutation({ mutationFn: () => communityPlusApi.remove(poll.id), onSuccess: onChanged });

  function toggle(id: string) {
    setSel((s) => (poll.multiChoice ? (s.includes(id) ? s.filter((x) => x !== id) : [...s, id]) : [id]));
  }

  return (
    <Card>
      <CardContent className="p-4 space-y-3">
        <div className="flex items-start justify-between gap-2">
          <div>
            <p className="font-medium">{poll.question}</p>
            <p className="text-xs text-muted-foreground">
              {t("poll.by", { name: poll.createdByName })} · {t("poll.voters", { count: poll.totalVoters })}
              {poll.multiChoice ? ` · ${t("poll.multi")}` : ""}
            </p>
          </div>
          {poll.isClosed && <Badge variant="secondary">{t("poll.closed")}</Badge>}
        </div>

        <div className="space-y-2">
          {poll.options.map((o) => (
            <div key={o.id}>
              {voting ? (
                <label className="flex items-center gap-2 text-sm cursor-pointer">
                  <input type={poll.multiChoice ? "checkbox" : "radio"} name={"poll-" + poll.id} checked={sel.includes(o.id)}
                    onChange={() => toggle(o.id)} />
                  {o.text}
                </label>
              ) : (
                <div className="text-sm">
                  <div className="flex justify-between">
                    <span>{o.text}{o.voted ? " ✓" : ""}</span>
                    <span className="text-muted-foreground">{o.votes} · {o.percent}%</span>
                  </div>
                  <div className="h-2 rounded bg-muted overflow-hidden" role="progressbar" aria-valuenow={o.percent} aria-valuemin={0} aria-valuemax={100}>
                    <div className="h-full bg-primary" style={{ width: o.percent + "%" }} />
                  </div>
                </div>
              )}
            </div>
          ))}
        </div>

        <div className="flex flex-wrap gap-2">
          {voting && (
            <Button size="sm" disabled={sel.length === 0 || vote.isPending} onClick={() => vote.mutate()}>{t("poll.vote")}</Button>
          )}
          {poll.canManage && !poll.isClosed && <Button size="sm" variant="outline" onClick={() => close.mutate()}>{t("poll.close")}</Button>}
          {poll.canManage && <Button size="sm" variant="ghost" onClick={() => del.mutate()}>{t("poll.delete")}</Button>}
        </div>
      </CardContent>
    </Card>
  );
}
