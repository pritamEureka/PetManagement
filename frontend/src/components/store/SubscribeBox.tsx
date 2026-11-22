import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Bell, BellOff, Repeat } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { toast } from "@/components/ui/sonner";
import { shippingAddressesApi } from "@/api/marketplace";
import { discoveryApi, shopPlusApi } from "@/api/shopPlus";
import { useAuthStore } from "@/store/authStore";

interface Props {
  productId: string;
  variantId?: string | null;
  quantity?: number;
  /** Effective unit price; used to preview the subscribe-and-save price. */
  unitPrice: number;
}

const INTERVALS = [14, 30, 45, 60, 90];
const SAVE_PERCENT = 10;

/**
 * Subscribe-and-save box + price-drop alert toggle + "record recently viewed" side effect.
 * Mount on ProductDetailPage below the add-to-cart row.
 */
export function SubscribeBox({ productId, variantId, quantity = 1, unitPrice }: Props) {
  const qc = useQueryClient();
  const signedIn = useAuthStore((s) => !!s.user?.id);
  const [interval, setInterval_] = useState("30");
  const [addressId, setAddressId] = useState<string>("");
  const [paymentMethod, setPaymentMethod] = useState("cod");

  useEffect(() => {
    if (signedIn) discoveryApi.recordView(productId).catch(() => { /* best effort */ });
  }, [productId, signedIn]);

  const { data: addresses } = useQuery({
    queryKey: ["shipping-addresses"], queryFn: () => shippingAddressesApi.list(), enabled: signedIn,
  });
  const { data: watches } = useQuery({
    queryKey: ["price-watches"], queryFn: () => discoveryApi.priceWatches(), enabled: signedIn,
  });
  const watch = watches?.find((w) => w.productId === productId);

  useEffect(() => {
    if (!addressId && addresses?.length) setAddressId((addresses.find((a) => a.isDefault) ?? addresses[0]).id);
  }, [addresses, addressId]);

  const subscribe = useMutation({
    mutationFn: () => shopPlusApi.subscribe({
      productId, variantId: variantId ?? null, quantity, intervalDays: Number(interval),
      shippingAddressId: addressId, paymentMethod,
    }),
    onSuccess: () => {
      toast.success("Subscribed. Manage it under My subscriptions.");
      qc.invalidateQueries({ queryKey: ["my-subscriptions"] });
    },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Could not subscribe."),
  });

  const toggleWatch = useMutation({
    mutationFn: async () => {
      if (watch) await discoveryApi.deletePriceWatch(watch.id);
      else await discoveryApi.createPriceWatch(productId);
    },
    onSuccess: () => {
      toast.success(watch ? "Price alert removed" : "We'll notify you when the price drops");
      qc.invalidateQueries({ queryKey: ["price-watches"] });
    },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Could not update price alert."),
  });

  if (!signedIn) return null;

  const saved = unitPrice * (1 - SAVE_PERCENT / 100);

  return (
    <Card>
      <CardContent className="space-y-3 pt-4">
        <div className="flex items-center gap-2 font-medium">
          <Repeat className="h-4 w-4 text-primary" /> Subscribe &amp; save {SAVE_PERCENT}%
          <span className="text-sm font-normal text-muted-foreground">${saved.toFixed(2)} each</span>
        </div>
        <div className="flex flex-wrap gap-2">
          <Select value={interval} onValueChange={setInterval_}>
            <SelectTrigger className="w-40"><SelectValue /></SelectTrigger>
            <SelectContent>
              {INTERVALS.map((d) => <SelectItem key={d} value={String(d)}>Every {d} days</SelectItem>)}
            </SelectContent>
          </Select>
          <Select value={paymentMethod} onValueChange={setPaymentMethod}>
            <SelectTrigger className="w-44"><SelectValue /></SelectTrigger>
            <SelectContent>
              <SelectItem value="cod">Cash on delivery</SelectItem>
              <SelectItem value="sslcommerz">Online payment</SelectItem>
            </SelectContent>
          </Select>
        </div>
        {addresses && addresses.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            Add a <Link className="underline" to="/account/addresses">shipping address</Link> to subscribe.
          </p>
        ) : (
          <Select value={addressId} onValueChange={setAddressId}>
            <SelectTrigger><SelectValue placeholder="Deliver to..." /></SelectTrigger>
            <SelectContent>
              {addresses?.map((a) => <SelectItem key={a.id} value={a.id}>{a.label} - {a.addressLine1}</SelectItem>)}
            </SelectContent>
          </Select>
        )}
        <div className="flex flex-wrap gap-2">
          <Button onClick={() => subscribe.mutate()} disabled={!addressId || subscribe.isPending}>
            <Repeat className="h-4 w-4 mr-2" /> Subscribe
          </Button>
          <Button variant="outline" onClick={() => toggleWatch.mutate()} disabled={toggleWatch.isPending}>
            {watch ? <BellOff className="h-4 w-4 mr-2" /> : <Bell className="h-4 w-4 mr-2" />}
            {watch ? "Stop price alerts" : "Alert me on price drop"}
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}
