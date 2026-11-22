import { useNavigate } from "react-router-dom";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { RotateCcw } from "lucide-react";
import { Button } from "@/components/ui/button";
import { toast } from "@/components/ui/sonner";
import { shopPlusApi } from "@/api/shopPlus";
import { useCartStore } from "@/store/cartStore";

interface Props {
  orderId: string;
  size?: "default" | "sm" | "lg";
  variant?: "default" | "outline" | "secondary" | "ghost";
}

/** One-click reorder: adds the order's still-available items to the cart and opens the cart. */
export function ReorderButton({ orderId, size = "sm", variant = "outline" }: Props) {
  const navigate = useNavigate();
  const qc = useQueryClient();
  const hydrateCart = useCartStore((s) => s.hydrate);

  const reorder = useMutation({
    mutationFn: () => shopPlusApi.reorder(orderId),
    onSuccess: (r) => {
      qc.invalidateQueries({ queryKey: ["cart"] });
      void hydrateCart();
      if (r.addedCount > 0) toast.success(`${r.addedCount} item(s) added to your cart`);
      if (r.skipped.length > 0)
        toast.warning(`Skipped: ${r.skipped.map((s) => s.productName).join(", ")}`);
      if (r.addedCount > 0) navigate("/cart");
    },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Could not reorder."),
  });

  return (
    <Button size={size} variant={variant} disabled={reorder.isPending} onClick={() => reorder.mutate()}>
      <RotateCcw className="h-4 w-4 mr-2" /> Reorder
    </Button>
  );
}
