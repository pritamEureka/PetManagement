import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { Minus, Plus, ShoppingCart, Trash2, AlertTriangle, Tag, X } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Separator } from "@/components/ui/separator";
import { useCartStore } from "@/store/cartStore";
import { cartApi, couponsApi } from "@/api/marketplace";
import { toast } from "@/components/ui/sonner";

export function CartPage() {
  const { lines, setQty, remove, subtotal, clear, appliedCoupon, setCoupon } = useCartStore();
  const nav = useNavigate();
  const [couponInput, setCouponInput] = useState("");
  const [applying, setApplying] = useState(false);

  // Pull authoritative totals (incl. shipping + tax) from the server so the
  // preview matches what OrderService.CheckoutAsync will stamp on the order.
  const { data: serverCart } = useQuery({
    queryKey: ["cart-totals"],
    queryFn: () => cartApi.get(),
    enabled: lines.length > 0
  });

  const sub = serverCart?.subtotal ?? subtotal();
  const discount = appliedCoupon?.discount ?? 0;
  // Server-cart shipping/tax are computed on raw subtotal; the discount applies
  // to the subtotal in OrderService.CheckoutAsync, so the *displayed* total
  // here mirrors that logic for an accurate preview.
  const subAfterDiscount = Math.max(0, sub - discount);
  const ship = serverCart?.shippingFee ?? 0;
  const tax = serverCart?.tax ?? 0;
  const total = subAfterDiscount + ship + tax;

  function reportCartError(err: any) {
    toast.error(err?.response?.data?.error?.message ?? "Cart update failed.");
  }

  async function applyCoupon() {
    const code = couponInput.trim().toUpperCase();
    if (!code) return;
    setApplying(true);
    try {
      const result = await couponsApi.apply(code, sub);
      setCoupon({ code: result.code, discount: result.discount });
      setCouponInput("");
      toast.success(`Coupon ${result.code} applied — save $${result.discount.toFixed(2)}`);
    } catch (err: any) {
      const msg = err?.response?.data?.error?.message ?? "Could not apply coupon.";
      toast.error(msg);
    } finally {
      setApplying(false);
    }
  }

  return (
    <div className="max-w-4xl mx-auto space-y-4">
      <h1 className="text-2xl font-bold flex items-center gap-2"><ShoppingCart className="h-6 w-6 text-primary" /> Cart</h1>

      {lines.length === 0 ? (
        <Card>
          <CardContent className="py-16 text-center space-y-3">
            <ShoppingCart className="h-12 w-12 mx-auto text-muted-foreground" />
            <p className="font-medium">Your cart is empty</p>
            <Button asChild><Link to="/store">Continue shopping</Link></Button>
          </CardContent>
        </Card>
      ) : (
        <div className="grid lg:grid-cols-[1fr_20rem] gap-4">
          <Card>
            <CardContent className="pt-6 divide-y">
              {lines.map((l) => {
                const overStock = l.stockAvailable !== undefined && l.quantity > l.stockAvailable;
                return (
                  <div key={l.productId} className="py-4 flex gap-2 sm:gap-3 items-start sm:items-center flex-wrap sm:flex-nowrap">
                    <div className="h-16 w-16 bg-muted rounded overflow-hidden flex-shrink-0">
                      {l.image && <img src={l.image} className="object-cover w-full h-full" />}
                    </div>
                    <div className="flex-1 min-w-0">
                      <p className="font-medium truncate">{l.name}</p>
                      <p className="text-xs text-muted-foreground">{l.storeName}</p>
                      <p className="text-sm font-semibold mt-1">${l.price.toFixed(2)}</p>
                      {overStock && (
                        <p className="text-xs text-destructive flex items-center gap-1 mt-1">
                          <AlertTriangle className="h-3 w-3" /> Only {l.stockAvailable} available
                        </p>
                      )}
                    </div>
                    <div className="flex items-center gap-1">
                      <Button size="icon" variant="outline"
                        onClick={() => setQty(l.productId, l.quantity - 1).catch(reportCartError)}>
                        <Minus className="h-3 w-3" />
                      </Button>
                      <span className="w-8 text-center">{l.quantity}</span>
                      <Button size="icon" variant="outline"
                        onClick={() => setQty(l.productId, l.quantity + 1).catch(reportCartError)}>
                        <Plus className="h-3 w-3" />
                      </Button>
                    </div>
                    <Button size="icon" variant="ghost"
                      onClick={() => remove(l.productId).catch(reportCartError)}>
                      <Trash2 className="h-4 w-4 text-destructive" />
                    </Button>
                  </div>
                );
              })}
            </CardContent>
          </Card>

          <Card className="h-fit">
            <CardContent className="pt-6 space-y-3">
              <p className="font-semibold">Order summary</p>
              <div className="flex justify-between text-sm">
                <span className="text-muted-foreground">Subtotal</span>
                <span>${sub.toFixed(2)}</span>
              </div>
              <div className="flex justify-between text-sm">
                <span className="text-muted-foreground">Shipping</span>
                <span>{ship === 0 ? "Free" : `$${ship.toFixed(2)}`}</span>
              </div>
              {tax > 0 && (
                <div className="flex justify-between text-sm">
                  <span className="text-muted-foreground">Tax</span>
                  <span>${tax.toFixed(2)}</span>
                </div>
              )}
              {appliedCoupon && (
                <div className="flex justify-between text-sm text-emerald-600">
                  <span className="flex items-center gap-1">
                    <Tag className="h-3 w-3" /> {appliedCoupon.code}
                    <button onClick={() => setCoupon(null)} className="ml-1 rounded-full text-muted-foreground hover:bg-muted hover:text-foreground">
                      <X className="h-3 w-3" />
                    </button>
                  </span>
                  <span>−${appliedCoupon.discount.toFixed(2)}</span>
                </div>
              )}
              {!appliedCoupon && (
                <div className="flex gap-2 pt-1">
                  <Input
                    placeholder="Coupon code"
                    value={couponInput}
                    onChange={(e) => setCouponInput(e.target.value)}
                    onKeyDown={(e) => { if (e.key === "Enter") { e.preventDefault(); applyCoupon(); } }}
                  />
                  <Button variant="outline" onClick={applyCoupon} disabled={applying || !couponInput.trim()}>
                    {applying ? "..." : "Apply"}
                  </Button>
                </div>
              )}
              <Separator />
              <div className="flex justify-between font-semibold">
                <span>Total</span>
                <span>${total.toFixed(2)}</span>
              </div>
              <Button className="w-full" onClick={() => nav("/checkout")}>Checkout</Button>
              <Button variant="ghost" className="w-full" onClick={() => clear().catch(reportCartError)}>Empty cart</Button>
            </CardContent>
          </Card>
        </div>
      )}
    </div>
  );
}
