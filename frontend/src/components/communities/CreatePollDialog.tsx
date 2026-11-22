import { useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Checkbox } from "@/components/ui/checkbox";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { toast } from "@/components/ui/sonner";
import { Field } from "@/pages/pets/detail/shared";
import { useI18n } from "@/lib/i18n";
import { communityPlusApi } from "@/api/socialPlus";

export function CreatePollDialog({ communityId, open, onOpenChange, onCreated }:
  { communityId: string; open: boolean; onOpenChange: (o: boolean) => void; onCreated: () => void }) {
  const { t } = useI18n();
  const [question, setQuestion] = useState("");
  const [options, setOptions] = useState(["", ""]);
  const [multi, setMulti] = useState(false);
  const [closes, setCloses] = useState("");

  const create = useMutation({
    mutationFn: () => communityPlusApi.createPoll(communityId, {
      question: question.trim(), options: options.map((o) => o.trim()).filter(Boolean), multiChoice: multi,
      closesAt: closes ? new Date(closes).toISOString() : null,
    }),
    onSuccess: () => {
      toast.success(t("poll.created"));
      setQuestion(""); setOptions(["", ""]); setMulti(false); setCloses("");
      onOpenChange(false); onCreated();
    },
  });
  const valid = question.trim().length > 0 && options.filter((o) => o.trim()).length >= 2;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader><DialogTitle>{t("poll.create")}</DialogTitle></DialogHeader>
        <div className="space-y-3">
          <Field label={t("poll.question")} htmlFor="pq">
            <Input id="pq" maxLength={300} value={question} onChange={(e) => setQuestion(e.target.value)} />
          </Field>
          {options.map((o, i) => (
            <Field key={i} label={t("poll.option", { n: i + 1 })} htmlFor={"po" + i}>
              <Input id={"po" + i} maxLength={160} value={o} onChange={(e) => setOptions(options.map((x, j) => (j === i ? e.target.value : x)))} />
            </Field>
          ))}
          {options.length < 10 && (
            <Button type="button" variant="outline" size="sm" onClick={() => setOptions([...options, ""])}>{t("poll.addOption")}</Button>
          )}
          <label className="flex items-center gap-2 text-sm">
            <Checkbox checked={multi} onChange={(e) => setMulti(e.target.checked)} /> {t("poll.allowMulti")}
          </label>
          <Field label={t("poll.closesAt")} htmlFor="pc">
            <Input id="pc" type="datetime-local" value={closes} onChange={(e) => setCloses(e.target.value)} />
          </Field>
        </div>
        <DialogFooter>
          <Button disabled={!valid || create.isPending} onClick={() => create.mutate()}>{t("poll.create")}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
