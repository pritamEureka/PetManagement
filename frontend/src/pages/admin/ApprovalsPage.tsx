import { useState } from "react";
import { Check, X, Stethoscope, ShoppingBag, HeartHandshake, Sparkles } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Can } from "@/components/auth/Can";
import { adoptionApi } from "@/api/adoption";
import { toast } from "@/components/ui/sonner";

export function ApprovalsPage() {
  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-bold">Approvals</h1>
        <p className="text-sm text-muted-foreground">
          Review pending items across modules. Each tab is a separate queue with its own permissions.
        </p>
      </div>

      <Tabs defaultValue="adoption">
        <TabsList>
          <TabsTrigger value="adoption"><HeartHandshake className="h-3.5 w-3.5 mr-1" /> Adoption</TabsTrigger>
          <TabsTrigger value="vets"><Stethoscope className="h-3.5 w-3.5 mr-1" /> Vets</TabsTrigger>
          <TabsTrigger value="stores"><ShoppingBag className="h-3.5 w-3.5 mr-1" /> Stores</TabsTrigger>
          <TabsTrigger value="services"><Sparkles className="h-3.5 w-3.5 mr-1" /> Services</TabsTrigger>
        </TabsList>

        <TabsContent value="adoption">
          <AdoptionQueue />
        </TabsContent>
        <TabsContent value="vets">
          <Card><CardContent className="py-8 text-center text-muted-foreground text-sm">
            Vet approval queue. Wires into <code>POST /vets/{"{id}"}/approve|reject</code>.
          </CardContent></Card>
        </TabsContent>
        <TabsContent value="stores">
          <Card><CardContent className="py-8 text-center text-muted-foreground text-sm">
            Store owner approvals.
          </CardContent></Card>
        </TabsContent>
        <TabsContent value="services">
          <Card><CardContent className="py-8 text-center text-muted-foreground text-sm">
            Service provider approvals.
          </CardContent></Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}

function AdoptionQueue() {
  // The current API only lists approved listings publicly. A real implementation
  // would expose GET /v1/adoption/listings?status=pending — wire that here when
  // it lands. For now this surface demonstrates the per-row workflow.
  const [items] = useState<{ id: string; title: string; ownerName: string; createdAt: string }[]>([]);

  async function approve(id: string) {
    try { await adoptionApi.approve(id); toast.success("Approved."); }
    catch (e: any) { toast.error(e?.response?.data?.error?.message ?? "Failed."); }
  }
  async function reject(id: string) {
    const notes = prompt("Reason (visible to the submitter)?") ?? "";
    try { await adoptionApi.reject(id, notes); toast.success("Rejected."); }
    catch (e: any) { toast.error(e?.response?.data?.error?.message ?? "Failed."); }
  }

  if (items.length === 0) {
    return <Card><CardContent className="py-8 text-center text-muted-foreground text-sm">No pending adoption listings.</CardContent></Card>;
  }

  return (
    <div className="space-y-2">
      {items.map((it) => (
        <Card key={it.id}>
          <CardContent className="py-3 flex items-center justify-between gap-3">
            <div>
              <p className="font-medium">{it.title}</p>
              <p className="text-xs text-muted-foreground">by {it.ownerName} · {new Date(it.createdAt).toLocaleString()}</p>
            </div>
            <div className="flex gap-2">
              <Badge variant="muted">Pending</Badge>
              <Can permission="adoption.approve">
                <Button size="sm" onClick={() => approve(it.id)}><Check className="h-3.5 w-3.5 mr-1" /> Approve</Button>
              </Can>
              <Can permission="adoption.reject">
                <Button size="sm" variant="destructive" onClick={() => reject(it.id)}><X className="h-3.5 w-3.5 mr-1" /> Reject</Button>
              </Can>
            </div>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
