import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useSearchParams } from "react-router-dom";
import { Scale } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { storesApi } from "@/api/marketplace";
import { marketplacePlusApi } from "@/api/marketplacePlus";

const dash = (v: unknown, fmt: (x: any) => string = String) => (v == null ? "-" : fmt(v));

/** Compare 2-4 stores side by side. Selection lives in the URL (?ids=a&ids=b). */
export function StoreComparePage() {
  const [params, setParams] = useSearchParams();
  const ids = params.getAll("ids");
  const [pick, setPick] = useState<string[]>(ids);

  const { data: stores } = useQuery({ queryKey: ["stores-for-compare"], queryFn: () => storesApi.search({ status: "Approved", pageSize: 50 }) });
  const list: { id: string; name: string }[] = stores?.items ?? [];

  const { data, isFetching } = useQuery({
    queryKey: ["store-compare", ids],
    queryFn: () => marketplacePlusApi.compareStores(ids),
    enabled: ids.length >= 2 && ids.length <= 4,
  });

  const toggle = (id: string) => setPick((p) => (p.includes(id) ? p.filter((x) => x !== id) : p.length >= 4 ? p : [...p, id]));
  const rows: [string, (s: any) => string][] = [
    ["Rating", (s) => dash(s.rating, (x) => `${Number(x).toFixed(1)} (${s.reviewCount})`)],
    ["Avg fulfilment", (s) => dash(s.avgFulfilmentDays, (x) => `${x} days (${s.deliveredOrders} orders)`)],
    ["Return rate", (s) => dash(s.returnRatePercent, (x) => `${x}%`)],
    ["Price index (100 = median)", (s) => dash(s.priceIndex, (x) => `${x} (${s.comparableProducts} products)`)],
    ["Shipping", (s) => `$${s.shippingFee.toFixed(2)}, free over $${s.freeShippingThreshold.toFixed(0)}`],
    ["Active products", (s) => String(s.activeProducts)],
  ];

  return (
    <div className="max-w-5xl mx-auto space-y-4">
      <h1 className="text-2xl font-bold flex items-center gap-2"><Scale className="h-6 w-6 text-primary" /> Compare stores</h1>
      <Card>
        <CardContent className="p-3 space-y-2">
          <p className="text-sm text-muted-foreground">Pick 2 to 4 stores.</p>
          <div className="flex flex-wrap gap-2">
            {list.map((s) => (
              <Button key={s.id} size="sm" variant={pick.includes(s.id) ? "default" : "outline"} onClick={() => toggle(s.id)}>{s.name}</Button>
            ))}
          </div>
          <Button disabled={pick.length < 2} onClick={() => setParams({ ids: pick })}>Compare</Button>
        </CardContent>
      </Card>

      {isFetching && <Skeleton className="h-40" />}
      {data && (
        <Card>
          <CardContent className="p-0 overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b">
                  <th className="p-3 text-left" />
                  {data.stores.map((s) => <th key={s.storeId} className="p-3 text-left">{s.name}<div className="text-xs font-normal text-muted-foreground">{s.city}</div></th>)}
                </tr>
              </thead>
              <tbody>
                {rows.map(([label, fn]) => (
                  <tr key={label} className="border-b last:border-0">
                    <td className="p-3 text-muted-foreground">{label}</td>
                    {data.stores.map((s) => <td key={s.storeId} className="p-3">{fn(s)}</td>)}
                  </tr>
                ))}
              </tbody>
            </table>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
