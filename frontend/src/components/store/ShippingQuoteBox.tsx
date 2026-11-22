import { useQuery } from "@tanstack/react-query";
import { Truck } from "lucide-react";
import { logisticsApi } from "@/api/logisticsPlus";

/**
 * Shipping fee + ETA preview for a destination city. Mount it in the checkout page next to the totals:
 *   <ShippingQuoteBox storeId={firstStoreId} city={selectedAddress.city} subtotal={cart.subtotal} />
 * When no delivery zone matches it renders nothing (the legacy flat fee applies at checkout).
 */
export function ShippingQuoteBox({ storeId, city, subtotal }: { storeId?: string; city?: string | null; subtotal: number }) {
  const { data } = useQuery({
    queryKey: ["shipping-quote", storeId, city, subtotal],
    queryFn: () => logisticsApi.quote({ storeId, city: city ?? undefined, subtotal }),
    enabled: !!city,
    retry: false
  });
  if (!data || !data.zoneMatched) return null;
  return (
    <div className="rounded-md border p-3 text-sm space-y-1" data-testid="shipping-quote">
      <div className="flex items-center gap-2 font-medium"><Truck className="h-4 w-4" /> Delivery to {city} ({data.zoneName})</div>
      {data.freeShippingApplied
        ? <p className="text-green-600">Free shipping applied</p>
        : (
          <p>
            Shipping {data.baseFee.toFixed(2)} {data.currency}
            {data.peakSurcharge > 0 && <> + {data.peakSurcharge.toFixed(2)} {data.peakLabel ?? "peak-hour"} surcharge</>}
            {" "}= <span className="font-medium">{data.total.toFixed(2)} {data.currency}</span>
          </p>
        )}
      {data.etaMinHours != null && data.etaMaxHours != null && (
        <p className="text-muted-foreground">
          Estimated delivery in {Math.ceil(data.etaMinHours / 24)}-{Math.ceil(data.etaMaxHours / 24)} day(s)
        </p>
      )}
    </div>
  );
}
