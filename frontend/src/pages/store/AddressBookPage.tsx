import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Plus, Star, Trash2 } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Checkbox } from "@/components/ui/checkbox";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog";
import { shippingAddressesApi } from "@/api/marketplace";
import { shippingAddressSchema, type ShippingAddressInput } from "@/lib/schemas";
import { toast } from "@/components/ui/sonner";

export function AddressBookPage() {
  const qc = useQueryClient();
  const [open, setOpen] = useState(false);

  const { data: addresses } = useQuery({
    queryKey: ["shipping-addresses"],
    queryFn: () => shippingAddressesApi.list()
  });

  const { register, handleSubmit, reset, formState: { errors, isSubmitting } } =
    useForm<ShippingAddressInput>({ resolver: zodResolver(shippingAddressSchema) });

  const create = useMutation({
    mutationFn: (input: ShippingAddressInput) => shippingAddressesApi.create({
      ...input,
      addressLine2: input.addressLine2 || undefined,
      city: input.city || undefined, state: input.state || undefined,
      country: input.country || undefined, postalCode: input.postalCode || undefined
    } as any),
    onSuccess: () => {
      toast.success("Address saved.");
      qc.invalidateQueries({ queryKey: ["shipping-addresses"] });
      reset(); setOpen(false);
    },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Could not save address.")
  });

  const remove = useMutation({
    mutationFn: (id: string) => shippingAddressesApi.remove(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["shipping-addresses"] })
  });
  const setDefault = useMutation({
    mutationFn: (id: string) => shippingAddressesApi.setDefault(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["shipping-addresses"] })
  });

  return (
    <div className="max-w-3xl mx-auto space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Shipping addresses</h1>
        <Dialog open={open} onOpenChange={setOpen}>
          <DialogTrigger asChild><Button><Plus className="h-4 w-4 mr-1" /> Add address</Button></DialogTrigger>
          <DialogContent>
            <DialogHeader><DialogTitle>New address</DialogTitle></DialogHeader>
            <form onSubmit={handleSubmit((v) => create.mutate(v))} className="space-y-3">
              <div><Label>Label</Label><Input {...register("label")} placeholder="Home" />
                {errors.label && <p className="text-xs text-destructive">{errors.label.message}</p>}</div>
              <div><Label>Recipient</Label><Input {...register("recipientName")} />
                {errors.recipientName && <p className="text-xs text-destructive">{errors.recipientName.message}</p>}</div>
              <div><Label>Phone</Label><Input {...register("phoneNumber")} /></div>
              <div><Label>Address line 1</Label><Input {...register("addressLine1")} /></div>
              <div><Label>Address line 2</Label><Input {...register("addressLine2")} /></div>
              <div className="grid grid-cols-2 gap-3">
                <div><Label>City</Label><Input {...register("city")} /></div>
                <div><Label>State</Label><Input {...register("state")} /></div>
                <div><Label>Country</Label><Input {...register("country")} /></div>
                <div><Label>Postal code</Label><Input {...register("postalCode")} /></div>
              </div>
              <label className="flex items-center gap-2 text-sm">
                <Checkbox {...register("isDefault")} /> Set as default
              </label>
              <Button type="submit" className="w-full" disabled={isSubmitting}>
                {isSubmitting ? "Saving..." : "Save"}
              </Button>
            </form>
          </DialogContent>
        </Dialog>
      </div>

      <div className="grid sm:grid-cols-2 gap-3">
        {addresses?.length
          ? addresses.map((a) => (
              <Card key={a.id}>
                <CardHeader className="flex flex-row items-start justify-between space-y-0 pb-2">
                  <CardTitle className="text-base flex items-center gap-2">
                    {a.label} {a.isDefault && <Star className="h-4 w-4 text-amber-400 fill-current" />}
                  </CardTitle>
                  <div className="flex gap-1">
                    {!a.isDefault && (
                      <Button variant="ghost" size="sm" onClick={() => setDefault.mutate(a.id)}>Set default</Button>
                    )}
                    <Button variant="ghost" size="icon" onClick={() => remove.mutate(a.id)}>
                      <Trash2 className="h-4 w-4 text-destructive" />
                    </Button>
                  </div>
                </CardHeader>
                <CardContent className="text-sm space-y-0.5">
                  <p className="font-medium">{a.recipientName}</p>
                  <p className="text-muted-foreground">{a.addressLine1}{a.addressLine2 ? `, ${a.addressLine2}` : ""}</p>
                  <p className="text-muted-foreground">{[a.city, a.state, a.country, a.postalCode].filter(Boolean).join(", ")}</p>
                  <p className="text-xs text-muted-foreground pt-1">{a.phoneNumber}</p>
                </CardContent>
              </Card>
            ))
          : <Card className="sm:col-span-2"><CardContent className="py-8 text-center text-sm text-muted-foreground">No saved addresses yet.</CardContent></Card>}
      </div>
    </div>
  );
}
