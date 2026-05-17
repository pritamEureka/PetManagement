import { Link, useNavigate } from "react-router-dom";
import { Minus, Plus, ShoppingCart, Trash2, AlertTriangle } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Separator } from "@/components/ui/separator";
import { useCartStore } from "@/store/cartStore";

export function CartPage() {
  const { lines, setQty, remove, subtotal, clear } = useCartStore();
  const nav = useNavigate();

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
                  <div key={l.productId} className="py-4 flex gap-3 items-center">
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
                      <Button size="icon" variant="outline" onClick={() => setQty(l.productId, l.quantity - 1)}>
                        <Minus className="h-3 w-3" />
                      </Button>
                      <span className="w-8 text-center">{l.quantity}</span>
                      <Button size="icon" variant="outline" onClick={() => setQty(l.productId, l.quantity + 1)}>
                        <Plus className="h-3 w-3" />
                      </Button>
                    </div>
                    <Button size="icon" variant="ghost" onClick={() => remove(l.productId)}>
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
                <span>${subtotal().toFixed(2)}</span>
              </div>
              <div className="flex justify-between text-sm">
                <span className="text-muted-foreground">Shipping</span>
                <span>Calculated at checkout</span>
              </div>
              <Separator />
              <div className="flex justify-between font-semibold">
                <span>Total</span>
                <span>${subtotal().toFixed(2)}</span>
              </div>
              <Button className="w-full" onClick={() => nav("/checkout")}>Checkout</Button>
              <Button variant="ghost" className="w-full" onClick={clear}>Empty cart</Button>
            </CardContent>
          </Card>
        </div>
      )}
    </div>
  );
}
