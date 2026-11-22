import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Copy, Gift, Sparkles } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { toast } from "@/components/ui/sonner";
import { marketplacePlusApi } from "@/api/marketplacePlus";
import { useCommerceT } from "@/components/store/useCommerceT";

export function LoyaltyPage() {
  const c = useCommerceT();
  const qc = useQueryClient();
  const [page, setPage] = useState(1);
  const [code, setCode] = useState("");
  const { data: acct, isLoading } = useQuery({ queryKey: ["loyalty"], queryFn: () => marketplacePlusApi.loyalty() });
  const { data: history } = useQuery({ queryKey: ["loyalty-history", page], queryFn: () => marketplacePlusApi.loyaltyHistory(page, 15) });

  const redeem = useMutation({
    mutationFn: () => marketplacePlusApi.redeemReferral(code),
    onSuccess: () => { toast.success("Referral code applied. You will get a bonus after your first delivered order."); setCode(""); qc.invalidateQueries({ queryKey: ["loyalty"] }); },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Could not apply code."),
  });

  const [gcAmount, setGcAmount] = useState("500");
  const [gcEmail, setGcEmail] = useState("");
  const [gcMsg, setGcMsg] = useState("");
  const { data: myCards } = useQuery({ queryKey: ["my-gift-cards"], queryFn: () => marketplacePlusApi.myGiftCards() });
  const gcMin = 100, gcMax = 50000;
  const gcNum = Number(gcAmount);
  const gcValid = Number.isFinite(gcNum) && gcNum >= gcMin && gcNum <= gcMax;
  const requestCard = useMutation({
    mutationFn: () => marketplacePlusApi.requestGiftCard({ amount: gcNum, recipientEmail: gcEmail.trim() || undefined, message: gcMsg.trim() || undefined }),
    onSuccess: () => { toast.success(c("giftcard.requested")); setGcEmail(""); setGcMsg(""); qc.invalidateQueries({ queryKey: ["my-gift-cards"] }); },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? c("growth.error")),
  });

  const share = () => {
    if (!acct) return;
    const text = `Join me on Pawzaroo and use my referral code ${acct.referralCode}`;
    navigator.clipboard?.writeText(text).then(() => toast.success("Referral message copied"), () => toast.error("Copy failed"));
  };

  return (
    <div className="max-w-3xl mx-auto space-y-4">
      <h1 className="text-2xl font-bold flex items-center gap-2"><Sparkles className="h-6 w-6 text-primary" /> Loyalty and referrals</h1>

      {isLoading || !acct ? <Skeleton className="h-32" /> : (
        <div className="grid sm:grid-cols-2 gap-3">
          <Card>
            <CardHeader><CardTitle className="text-base">Your points</CardTitle></CardHeader>
            <CardContent className="space-y-1">
              <div className="text-3xl font-bold">{acct.balance.toLocaleString()}</div>
              <div className="text-sm text-muted-foreground">Worth ${acct.balanceValue.toFixed(2)} at checkout</div>
              <div className="flex items-center gap-2 pt-1">
                <Badge>{acct.tier}</Badge>
                {acct.nextTier && <span className="text-xs text-muted-foreground">{acct.pointsToNextTier} more points to {acct.nextTier}</span>}
              </div>
            </CardContent>
          </Card>
          <Card>
            <CardHeader><CardTitle className="text-base flex items-center gap-2"><Gift className="h-4 w-4" /> Refer a friend</CardTitle></CardHeader>
            <CardContent className="space-y-2">
              <div className="flex items-center gap-2">
                <code className="px-2 py-1 rounded bg-muted font-mono text-lg">{acct.referralCode}</code>
                <Button size="icon" variant="ghost" aria-label="Copy referral message" onClick={share}><Copy className="h-4 w-4" /></Button>
              </div>
              <p className="text-xs text-muted-foreground">
                {acct.referrals} friend(s) joined, {acct.rewardedReferrals} rewarded. You are rewarded when their first order is delivered.
              </p>
              <div className="flex gap-2">
                <Input placeholder="Have a friend's code?" value={code} onChange={(e) => setCode(e.target.value)} />
                <Button variant="outline" disabled={!code.trim() || redeem.isPending} onClick={() => redeem.mutate()}>Apply</Button>
              </div>
            </CardContent>
          </Card>
        </div>
      )}

      <Card>
        <CardHeader><CardTitle className="text-base flex items-center gap-2"><Gift className="h-4 w-4" /> {c("giftcard.title")}</CardTitle></CardHeader>
        <CardContent className="space-y-3">
          <p className="text-sm text-muted-foreground">{c("giftcard.help")}</p>
          <div className="grid sm:grid-cols-3 gap-2">
            <Input type="number" min={gcMin} max={gcMax} aria-label={c("giftcard.amount")} placeholder={c("giftcard.amount")} value={gcAmount} onChange={(e) => setGcAmount(e.target.value)} />
            <Input type="email" aria-label={c("giftcard.recipient")} placeholder={c("giftcard.recipient")} value={gcEmail} onChange={(e) => setGcEmail(e.target.value)} />
            <Input aria-label={c("giftcard.message")} placeholder={c("giftcard.message")} maxLength={200} value={gcMsg} onChange={(e) => setGcMsg(e.target.value)} />
          </div>
          {!gcValid && <p className="text-xs text-destructive">{c("giftcard.amountRange", { min: gcMin, max: gcMax })}</p>}
          <Button disabled={!gcValid || requestCard.isPending} onClick={() => requestCard.mutate()}>{c("giftcard.request")}</Button>
          {!myCards?.length ? <p className="text-sm text-muted-foreground">{c("giftcard.none")}</p> : (
            <div className="divide-y">
              {myCards.map((g) => (
                <div key={g.id} className="flex items-center justify-between py-2 text-sm gap-2 flex-wrap">
                  <div>
                    <code className="font-mono">{g.code}</code>
                    <div className="text-xs text-muted-foreground">{new Date(g.createdAt).toLocaleDateString()} - {g.initialAmount.toFixed(2)} {g.currency}</div>
                  </div>
                  <div className="text-right">
                    <Badge variant={g.status === "Active" ? "success" : g.status === "Pending" ? "warning" : "muted"}>{c(`giftcard.status.${g.status}`)}</Badge>
                    {g.status === "Active" && <div className="text-xs text-muted-foreground">{c("giftcard.balance")}: {g.balance.toFixed(2)}</div>}
                  </div>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle className="text-base">History</CardTitle></CardHeader>
        <CardContent className="space-y-1">
          {!history?.items.length ? <p className="text-sm text-muted-foreground">No activity yet.</p> : history.items.map((t) => (
            <div key={t.id} className="flex items-center justify-between text-sm border-b last:border-0 py-1.5">
              <div>
                <span className="font-medium">{t.type}</span>
                <span className="text-muted-foreground"> {t.note ? `- ${t.note}` : ""}</span>
                <div className="text-xs text-muted-foreground">
                  {new Date(t.createdAt).toLocaleDateString()}{t.expiresAt ? ` - expires ${new Date(t.expiresAt).toLocaleDateString()}` : ""}
                </div>
              </div>
              <span className={t.points >= 0 ? "text-emerald-600 font-semibold" : "text-destructive font-semibold"}>
                {t.points >= 0 ? "+" : ""}{t.points}
              </span>
            </div>
          ))}
          {history && history.total > history.pageSize && (
            <div className="flex justify-between pt-2">
              <Button size="sm" variant="outline" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Previous</Button>
              <Button size="sm" variant="outline" disabled={page * history.pageSize >= history.total} onClick={() => setPage((p) => p + 1)}>Next</Button>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
