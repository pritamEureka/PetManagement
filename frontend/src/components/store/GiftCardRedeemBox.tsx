import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Gift, Sparkles } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { toast } from "@/components/ui/sonner";
import { marketplacePlusApi } from "@/api/marketplacePlus";

export interface CheckoutCredits { giftCardCode?: string; redeemLoyaltyPoints?: number }

/** Gift card code entry + loyalty points toggle for the checkout summary. Reports the chosen credits via onChange. */
export function GiftCardRedeemBox({ payable, onChange }: { payable: number; onChange: (c: CheckoutCredits) => void }) {
  const [code, setCode] = useState("");
  const [appliedCode, setAppliedCode] = useState<string | undefined>();
  const [balance, setBalance] = useState<number | null>(null);
  const [usePoints, setUsePoints] = useState(false);

  const { data: preview } = useQuery({
    queryKey: ["loyalty-redeem-preview", Math.round(payable * 100)],
    queryFn: () => marketplacePlusApi.redeemPreview(payable),
    enabled: payable > 0,
  });

  useEffect(() => {
    onChange({
      giftCardCode: appliedCode,
      redeemLoyaltyPoints: usePoints && preview && preview.maxPoints > 0 ? preview.maxPoints : undefined,
    });
  }, [appliedCode, usePoints, preview?.maxPoints]); // eslint-disable-line react-hooks/exhaustive-deps

  async function apply() {
    try {
      const r = await marketplacePlusApi.checkGiftCard(code);
      if (!r.usable) { toast.error("This gift card cannot be used."); return; }
      setAppliedCode(code); setBalance(r.balance);
      toast.success(`Gift card applied (balance ${r.balance.toFixed(2)})`);
    } catch (e: any) {
      toast.error(e?.response?.data?.error?.message ?? "Gift card not found.");
    }
  }

  return (
    <div className="space-y-3 text-sm">
      <div className="space-y-1">
        <div className="font-medium flex items-center gap-1.5"><Gift className="h-4 w-4" /> Gift card</div>
        {appliedCode ? (
          <div className="flex items-center justify-between">
            <span>Applied{balance != null ? ` - balance $${balance.toFixed(2)}` : ""}</span>
            <Button size="sm" variant="ghost" onClick={() => { setAppliedCode(undefined); setBalance(null); setCode(""); }}>Remove</Button>
          </div>
        ) : (
          <div className="flex gap-2">
            <Input value={code} onChange={(e) => setCode(e.target.value)} placeholder="XXXX-XXXX-XXXX-XXXX" />
            <Button type="button" variant="outline" disabled={!code.trim()} onClick={apply}>Apply</Button>
          </div>
        )}
      </div>
      {preview && preview.balance > 0 && (
        <label className="flex items-center gap-2 cursor-pointer">
          <input type="checkbox" checked={usePoints} onChange={(e) => setUsePoints(e.target.checked)} />
          <Sparkles className="h-4 w-4 text-amber-500" />
          <span>Use {preview.maxPoints} points (-${preview.maxValue.toFixed(2)}) of {preview.balance} available</span>
        </label>
      )}
    </div>
  );
}
