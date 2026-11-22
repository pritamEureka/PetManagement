import { useQuery } from "@tanstack/react-query";
import { AlertTriangle } from "lucide-react";
import { marketplacePlusApi } from "@/api/marketplacePlus";

/** Pre-payment availability check: lists per-line problems (stock, inactive product/variant, store unavailable). Renders nothing when OK. */
export function AvailabilityBanner({ enabled = true }: { enabled?: boolean }) {
  const { data } = useQuery({
    queryKey: ["cart-availability"],
    queryFn: () => marketplacePlusApi.availability(),
    enabled,
    refetchInterval: 60_000,
  });
  if (!data || data.ok) return null;
  return (
    <div role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 p-3 text-sm space-y-1">
      <div className="font-medium flex items-center gap-2 text-destructive">
        <AlertTriangle className="h-4 w-4" /> Some items in your cart need attention
      </div>
      <ul className="list-disc pl-6">
        {data.problems.map((p) => (
          <li key={p.cartItemId}><span className="font-medium">{p.productName}</span>: {p.message}</li>
        ))}
      </ul>
    </div>
  );
}
