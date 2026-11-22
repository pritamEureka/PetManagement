import { useQuery } from "@tanstack/react-query";
import { CheckCircle2, Circle, MapPin, PackageCheck, Truck, AlertTriangle, Undo2, Tag } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { trackingApi, type TrackingStatus } from "@/api/commercePlus";

const LABELS: Record<TrackingStatus, string> = {
  LabelCreated: "Shipment created",
  PickedUp: "Picked up",
  InTransit: "In transit",
  OutForDelivery: "Out for delivery",
  Delivered: "Delivered",
  DeliveryFailed: "Delivery attempt failed",
  Returned: "Returned to sender",
  Exception: "Delay / exception"
};

function StatusIcon({ status }: { status: TrackingStatus }) {
  const cls = "h-4 w-4";
  switch (status) {
    case "Delivered": return <PackageCheck className={`${cls} text-emerald-600`} />;
    case "DeliveryFailed":
    case "Exception": return <AlertTriangle className={`${cls} text-amber-500`} />;
    case "Returned": return <Undo2 className={`${cls} text-amber-500`} />;
    case "LabelCreated": return <Tag className={`${cls} text-muted-foreground`} />;
    default: return <Truck className={`${cls} text-primary`} />;
  }
}

/** Live courier / delivery timeline for an order. Refreshes every 30s until delivered. */
export function TrackingTimeline({ orderId }: { orderId: string }) {
  const { data, isLoading, isError } = useQuery({
    queryKey: ["order-tracking", orderId],
    queryFn: () => trackingApi.timeline(orderId),
    enabled: !!orderId,
    retry: false,
    refetchInterval: (q) => (q.state.data?.shipmentStatus === "Delivered" ? false : 30_000)
  });

  if (isError) return null; // not authorised or tracking unavailable: stay out of the way
  if (isLoading || !data) return <Skeleton className="h-24" />;

  // Newest first reads best for buyers.
  const events = [...data.checkpoints].reverse();

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between space-y-0">
        <CardTitle className="text-base flex items-center gap-2"><Truck className="h-4 w-4" /> Tracking</CardTitle>
        <div className="flex items-center gap-2 text-xs text-muted-foreground">
          {data.courier && <Badge variant="outline">{data.courier}</Badge>}
          {data.trackingNumber && <span className="font-mono">{data.trackingNumber}</span>}
        </div>
      </CardHeader>
      <CardContent>
        {events.length === 0 ? (
          <p className="text-sm text-muted-foreground py-2">No tracking updates yet. Check back once your order ships.</p>
        ) : (
          <ol className="relative space-y-4 border-l pl-5 ml-2">
            {events.map((e, i) => (
              <li key={e.id} className="relative">
                <span className="absolute -left-[1.85rem] top-0.5 bg-background rounded-full">
                  {i === 0 ? <CheckCircle2 className="h-4 w-4 text-primary" /> : <Circle className="h-4 w-4 text-muted-foreground" />}
                </span>
                <div className="flex items-center gap-2 text-sm font-medium">
                  <StatusIcon status={e.status} /> {LABELS[e.status] ?? e.status}
                  {e.source === "Webhook" && e.provider && <Badge variant="secondary" className="text-[10px]">{e.provider}</Badge>}
                </div>
                {e.description && <p className="text-xs text-muted-foreground">{e.description}</p>}
                {e.locationText && (
                  <p className="text-xs text-muted-foreground flex items-center gap-1"><MapPin className="h-3 w-3" /> {e.locationText}</p>
                )}
                <p className="text-[11px] text-muted-foreground">{new Date(e.occurredAt).toLocaleString()}</p>
              </li>
            ))}
          </ol>
        )}

        {data.proof && (
          <div className="mt-4 rounded-md border p-3 text-sm space-y-1">
            <p className="font-semibold flex items-center gap-1"><PackageCheck className="h-4 w-4 text-emerald-600" /> Proof of delivery</p>
            {data.proof.recipientName && <p className="text-muted-foreground">Received by {data.proof.recipientName}</p>}
            {data.proof.signatureName && <p className="text-muted-foreground">Signed: {data.proof.signatureName}</p>}
            {data.proof.otpProvided && <p className="text-muted-foreground">Verified with delivery code</p>}
            {data.proof.photoUrl && (
              <a href={data.proof.photoUrl} target="_blank" rel="noopener noreferrer">
                <img src={data.proof.photoUrl} alt="Delivery proof" className="mt-1 h-24 rounded object-cover border" />
              </a>
            )}
          </div>
        )}
      </CardContent>
    </Card>
  );
}
