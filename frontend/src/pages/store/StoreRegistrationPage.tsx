import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "react-router-dom";
import { Store as StoreIcon, ShieldCheck, FileText, AlertTriangle } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Badge } from "@/components/ui/badge";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Separator } from "@/components/ui/separator";
import { kycSubmitSchema, storeRegisterSchema, type KycSubmitInput, type StoreRegisterInput } from "@/lib/schemas";
import { storesApi } from "@/api/marketplace";
import { toast } from "@/components/ui/sonner";

export function StoreRegistrationPage() {
  const qc = useQueryClient();
  const nav = useNavigate();

  const { data: kyc } = useQuery({ queryKey: ["store-kyc"], queryFn: () => storesApi.myKyc() });
  const { data: store } = useQuery({ queryKey: ["my-store"], queryFn: () => storesApi.mine() });

  const kycForm = useForm<KycSubmitInput>({
    resolver: zodResolver(kycSubmitSchema),
    defaultValues: kyc as any
  });
  const storeForm = useForm<StoreRegisterInput>({ resolver: zodResolver(storeRegisterSchema) });

  const submitKyc = useMutation({
    mutationFn: (input: KycSubmitInput) => storesApi.submitKyc(input as any),
    onSuccess: () => {
      toast.success("KYC submitted for review");
      qc.invalidateQueries({ queryKey: ["store-kyc"] });
    },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Submit failed.")
  });

  const registerStore = useMutation({
    mutationFn: (input: StoreRegisterInput) => storesApi.register(input as any),
    onSuccess: () => {
      toast.success("Store created. Awaiting admin approval.");
      qc.invalidateQueries({ queryKey: ["my-store"] });
      nav("/dashboard/store");
    },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Registration failed.")
  });

  const kycApproved = kyc?.kycStatus === "Approved";

  return (
    <div className="max-w-3xl mx-auto space-y-4">
      <div>
        <h1 className="text-2xl font-bold flex items-center gap-2"><StoreIcon className="h-6 w-6 text-primary" /> Become a seller</h1>
        <p className="text-sm text-muted-foreground">
          Two-step process: 1) verify your identity, 2) register your store. Admin reviews each step.
        </p>
      </div>

      {store && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <StoreIcon className="h-5 w-5" /> {store.name}
              <Badge variant={store.approvalStatus === "Approved" ? "default" : "outline"}>{store.approvalStatus}</Badge>
            </CardTitle>
          </CardHeader>
          <CardContent className="text-sm text-muted-foreground">
            You already have a store.{" "}
            <Link to="/dashboard/store" className="text-primary hover:underline">Open dashboard →</Link>
          </CardContent>
        </Card>
      )}

      <Tabs defaultValue="kyc">
        <TabsList>
          <TabsTrigger value="kyc"><ShieldCheck className="h-4 w-4 mr-1" /> 1. KYC</TabsTrigger>
          <TabsTrigger value="store" disabled={!kycApproved || !!store}>
            <StoreIcon className="h-4 w-4 mr-1" /> 2. Store details
          </TabsTrigger>
        </TabsList>

        <TabsContent value="kyc">
          <Card>
            <CardHeader>
              <CardTitle className="text-base flex items-center justify-between">
                Identity verification
                {kyc && <Badge variant={kycApproved ? "default" : "outline"}>{kyc.kycStatus}</Badge>}
              </CardTitle>
            </CardHeader>
            <CardContent>
              {kyc?.kycStatus === "Rejected" && (
                <p className="text-sm text-destructive flex items-center gap-1 mb-3">
                  <AlertTriangle className="h-4 w-4" /> Rejected: {kyc.adminNotes ?? "see admin notes"}
                </p>
              )}
              <form onSubmit={kycForm.handleSubmit((v) => submitKyc.mutate(v))} className="space-y-3">
                <div><Label>Legal name</Label><Input {...kycForm.register("legalName")} /></div>
                <div><Label>Business name (optional)</Label><Input {...kycForm.register("businessName")} /></div>
                <Separator />
                <p className="text-xs text-muted-foreground">Provide at least one of the following.</p>
                <div className="grid grid-cols-2 gap-3">
                  <div><Label>Trade license #</Label><Input {...kycForm.register("tradeLicenseNumber")} /></div>
                  <div><Label>National ID #</Label><Input {...kycForm.register("nationalIdNumber")} /></div>
                </div>
                <div><Label>Tax ID</Label><Input {...kycForm.register("taxId")} /></div>
                <Separator />
                <p className="text-xs text-muted-foreground flex items-center gap-1">
                  <FileText className="h-3 w-3" /> Document URLs (after uploading via /media)
                </p>
                <div className="grid sm:grid-cols-2 gap-3">
                  <div><Label>Trade license doc URL</Label><Input {...kycForm.register("tradeLicenseDocUrl")} /></div>
                  <div><Label>National ID doc URL</Label><Input {...kycForm.register("nationalIdDocUrl")} /></div>
                  <div className="sm:col-span-2"><Label>Address proof doc URL</Label><Input {...kycForm.register("addressProofDocUrl")} /></div>
                </div>
                <Button type="submit" disabled={submitKyc.isPending || kycApproved}>
                  {kycApproved ? "Approved" : submitKyc.isPending ? "Submitting..." : kyc ? "Re-submit" : "Submit for review"}
                </Button>
              </form>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="store">
          <Card>
            <CardHeader><CardTitle className="text-base">Store details</CardTitle></CardHeader>
            <CardContent>
              <form onSubmit={storeForm.handleSubmit((v) => registerStore.mutate(v))} className="space-y-3">
                <div><Label>Store name</Label><Input {...storeForm.register("name")} /></div>
                <div><Label>Description</Label><Textarea rows={3} {...storeForm.register("description")} /></div>
                <div className="grid grid-cols-2 gap-3">
                  <div><Label>Logo URL</Label><Input {...storeForm.register("logoUrl")} /></div>
                  <div><Label>Banner URL</Label><Input {...storeForm.register("bannerUrl")} /></div>
                </div>
                <Separator />
                <div><Label>Address</Label><Input {...storeForm.register("address")} /></div>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  <div><Label>City</Label><Input {...storeForm.register("city")} /></div>
                  <div><Label>Country</Label><Input {...storeForm.register("country")} /></div>
                  <div><Label>Phone</Label><Input {...storeForm.register("phoneNumber")} /></div>
                  <div><Label>Email</Label><Input {...storeForm.register("email")} /></div>
                </div>
                <Button type="submit" disabled={registerStore.isPending || !kycApproved}>
                  {registerStore.isPending ? "Creating..." : "Create store"}
                </Button>
                {!kycApproved && <p className="text-xs text-destructive">Complete KYC approval first.</p>}
              </form>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
