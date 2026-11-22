import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useNavigate, Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Separator } from "@/components/ui/separator";
import { checkoutSchema, type CheckoutInput } from "@/lib/schemas";
import { cartApi, ordersV2Api, shippingAddressesApi } from "@/api/marketplace";
import { useCartStore } from "@/store/cartStore";
import { toast } from "@/components/ui/sonner";
import { CountryCityPicker } from "@/components/common/CountryCityPicker";
import { AvailabilityBanner } from "@/components/store/AvailabilityBanner";
import { GiftCardRedeemBox, type CheckoutCredits } from "@/components/store/GiftCardRedeemBox";
import { ShippingQuoteBox } from "@/components/store/ShippingQuoteBox";
import { CartDeliveryPromise } from "@/components/store/BundleCard";

export function CheckoutPage() {
  const nav = useNavigate();
  // resetLocal — not clear() — because OrderService.CheckoutAsync already
  // removed the CartItems inside its transaction. We just need to drop the
  // optimistic local mirror.
  const { lines, subtotal, resetLocal, appliedCoupon } = useCartStore();
  const [credits, setCredits] = useState<CheckoutCredits>({});

  const { data: addresses } = useQuery({
    queryKey: ["shipping-addresses"],
    queryFn: () => shippingAddressesApi.list()
  });

  const { register, handleSubmit, watch, setValue, formState: { errors, isSubmitting } } =
    useForm<CheckoutInput>({ resolver: zodResolver(checkoutSchema) });

  const selectedAddressId = watch("shippingAddressId");
  const country = watch("shippingCountry") ?? "";
  const oneOffCity = watch("shippingCity") ?? "";
  const city = oneOffCity;
  // OrderService prefers the saved address when one is selected, so its city is the destination.
  const destCity = (addresses?.find((a) => a.id === selectedAddressId)?.city ?? oneOffCity) || undefined;

  // Server totals preview using the same delivery-zone fee logic as checkout (flat fee when no zone matches).
  const { data: serverCart } = useQuery({
    queryKey: ["cart-totals", destCity],
    queryFn: () => cartApi.get(destCity),
    enabled: lines.length > 0
  });

  useEffect(() => {
    const def = addresses?.find((a) => a.isDefault) ?? addresses?.[0];
    if (def && !selectedAddressId) setValue("shippingAddressId", def.id);
  }, [addresses, selectedAddressId, setValue]);

  async function onSubmit(values: CheckoutInput) {
    if (lines.length === 0) { toast.error("Cart is empty."); return; }
    try {
      const order = await ordersV2Api.checkout({
        shippingAddressId: values.shippingAddressId || undefined,
        shippingAddress: values.shippingAddress || undefined,
        shippingCity: values.shippingCity || undefined,
        shippingCountry: values.shippingCountry || undefined,
        paymentMethod: values.paymentMethod,
        couponCode: appliedCoupon?.code,
        redeemLoyaltyPoints: credits.redeemLoyaltyPoints,
        giftCardCode: credits.giftCardCode
      });
      resetLocal();

      // Hosted-payment methods return a redirect URL the user must visit to
      // complete payment. Don't toast success yet — the order is still
      // PaymentStatus=Pending until the gateway confirms via /checkout/success.
      if (order.paymentCheckoutUrl) {
        toast.message("Redirecting to payment gateway…");
        window.location.href = order.paymentCheckoutUrl;
        return;
      }

      toast.success(`Order ${order.orderNumber} placed.`);
      nav(`/orders/${order.id}`);
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Checkout failed.");
    }
  }

  return (
    <div className="max-w-4xl mx-auto grid lg:grid-cols-[1fr_20rem] gap-4">
      <div className="lg:col-span-2"><AvailabilityBanner /></div>
      <Card>
        <CardHeader>
          <CardTitle>Shipping details</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
            {addresses && addresses.length > 0 && (
              <div className="space-y-2">
                <Label>Choose a saved address</Label>
                <div className="space-y-2">
                  {addresses.map((a) => (
                    <label key={a.id} className="flex items-start gap-2 border rounded-md p-3 cursor-pointer hover:bg-background/80 dark:hover:bg-background/60">
                      <input
                        type="radio"
                        className="mt-1"
                        name="shippingAddressId"
                        value={a.id}
                        checked={selectedAddressId === a.id}
                        onChange={() => setValue("shippingAddressId", a.id)}
                      />
                      <div className="text-sm break-words">
                        <p className="font-medium">{a.label} — {a.recipientName} {a.isDefault && <span className="text-xs text-primary">(default)</span>}</p>
                        <p className="text-muted-foreground">{a.addressLine1}{a.addressLine2 ? `, ${a.addressLine2}` : ""}</p>
                        <p className="text-muted-foreground">{[a.city, a.state, a.country, a.postalCode].filter(Boolean).join(", ")}</p>
                        <p className="text-xs text-muted-foreground">{a.phoneNumber}</p>
                      </div>
                    </label>
                  ))}
                </div>
                <Link to="/account/addresses" className="text-xs text-primary hover:underline">Manage addresses</Link>
              </div>
            )}

            <details className="text-sm">
              <summary className="cursor-pointer text-muted-foreground">Or ship to a one-off address</summary>
              <div className="space-y-3 pt-3">
                <div>
                  <Label htmlFor="addr">Address</Label>
                  <Input id="addr" placeholder="123 Main St" {...register("shippingAddress")} />
                  {errors.shippingAddress && <p className="text-xs text-destructive">{errors.shippingAddress.message}</p>}
                </div>
                <CountryCityPicker
                  showState={false}
                  value={{ country, state: "", city }}
                  onChange={(v) => {
                    setValue("shippingCountry", v.country, { shouldDirty: true });
                    setValue("shippingCity", v.city, { shouldDirty: true });
                  }}
                />
                {/* Hidden inputs so RHF still tracks the values in the form. */}
                <input type="hidden" {...register("shippingCountry")} />
                <input type="hidden" {...register("shippingCity")} />
              </div>
            </details>

            <div className="space-y-2">
              <Label>Payment method</Label>
              <div className="grid sm:grid-cols-2 gap-2">
                <label className="flex items-start gap-2 border rounded-md p-3 cursor-pointer hover:bg-background/80 dark:hover:bg-background/60">
                  <input
                    type="radio"
                    className="mt-1"
                    value="sslcommerz"
                    defaultChecked
                    {...register("paymentMethod")}
                  />
                  <div className="text-sm">
                    <p className="font-medium">Pay online (SSLCommerz)</p>
                    <p className="text-muted-foreground text-xs">
                      Card / mobile banking / net banking. Redirects to the sandbox gateway.
                    </p>
                  </div>
                </label>
                <label className="flex items-start gap-2 border rounded-md p-3 cursor-pointer hover:bg-background/80 dark:hover:bg-background/60">
                  <input
                    type="radio"
                    className="mt-1"
                    value="cod"
                    {...register("paymentMethod")}
                  />
                  <div className="text-sm">
                    <p className="font-medium">Cash on delivery</p>
                    <p className="text-muted-foreground text-xs">
                      Pay when the courier hands you the parcel.
                    </p>
                  </div>
                </label>
              </div>
            </div>

            <Button type="submit" className="w-full" disabled={isSubmitting || lines.length === 0}>
              {isSubmitting ? "Placing order..." : "Place order"}
            </Button>
          </form>
        </CardContent>
      </Card>

      <Card className="h-fit">
        <CardHeader><CardTitle>Items ({lines.length})</CardTitle></CardHeader>
        <CardContent className="space-y-3">
          <div className="space-y-2 max-h-72 overflow-y-auto pr-1">
            {lines.map((l) => (
              <div key={l.productId} className="flex justify-between text-sm">
                <span className="truncate">{l.quantity}× {l.name}</span>
                <span className="font-medium">${(l.price * l.quantity).toFixed(2)}</span>
              </div>
            ))}
          </div>
          <Separator />
          <div className="flex justify-between text-sm">
            <span className="text-muted-foreground">Subtotal</span>
            <span>${subtotal().toFixed(2)}</span>
          </div>
          <ShippingQuoteBox city={destCity} subtotal={subtotal()} />
          <CartDeliveryPromise city={destCity} />
          {appliedCoupon && (
            <div className="flex justify-between text-sm text-emerald-600">
              <span>Coupon {appliedCoupon.code}</span>
              <span>−${appliedCoupon.discount.toFixed(2)}</span>
            </div>
          )}
          <GiftCardRedeemBox
            payable={Math.max(0, subtotal() - (appliedCoupon?.discount ?? 0))}
            onChange={setCredits}
          />
          <div className="flex justify-between font-semibold pt-1">
            <span>Estimated total</span>
            <span>${(Math.max(0, subtotal() - (appliedCoupon?.discount ?? 0)) + (serverCart ? serverCart.shippingFee + serverCart.tax : 0)).toFixed(2)}</span>
          </div>
          <p className="text-xs text-muted-foreground">
            {serverCart ? "Includes estimated shipping & tax; credits and promotions are applied at order placement." : "+ shipping & tax computed at order placement."}
          </p>
        </CardContent>
      </Card>
    </div>
  );
}
