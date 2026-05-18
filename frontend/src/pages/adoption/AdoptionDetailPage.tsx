import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  ArrowLeft, MapPin, HeartHandshake, Bookmark, Pencil, Trash2, CheckCircle, ShieldCheck, ShieldX,
  Cat, Stethoscope, Sparkles, Baby, PawPrint
} from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Textarea } from "@/components/ui/textarea";
import { Skeleton } from "@/components/ui/skeleton";
import { Separator } from "@/components/ui/separator";
import { adoptionApi, type AdoptionListingStatus } from "@/api/adoption";
import { toast } from "@/components/ui/sonner";
import { EmptyState } from "@/components/common/EmptyState";
import { Can } from "@/components/auth/Can";
import { MessageOwnerButton } from "@/components/adoption/MessageOwnerButton";
import { EditAdoptionDialog } from "./EditAdoptionDialog";
import { RejectReasonModal } from "@/components/adoption/RejectReasonModal";
import { MarkAsAdoptedDialog } from "@/components/adoption/MarkAsAdoptedDialog";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";

const STATUS_VARIANT: Record<AdoptionListingStatus, "default"|"secondary"|"muted"|"destructive"> = {
  Draft: "muted", PendingApproval: "secondary", Approved: "default",
  Rejected: "destructive", Adopted: "default", Closed: "muted"
};

export function AdoptionDetailPage() {
  const { id = "" } = useParams();
  const qc = useQueryClient();
  const { data: listing, isLoading } = useQuery({
    queryKey: ["adoption", id],
    queryFn: () => adoptionApi.get(id),
    enabled: !!id
  });

  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const [editing, setEditing] = useState(false);
  const [rejecting, setRejecting] = useState(false);
  const [marking, setMarking] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [saved, setSaved] = useState<boolean | null>(null);

  function invalidate() { qc.invalidateQueries({ queryKey: ["adoption", id] }); }

  async function apply() {
    if (!message.trim()) return;
    setBusy(true);
    try {
      await adoptionApi.applyToAdopt(id, message.trim());
      toast.success("Request sent. The owner will be in touch.");
      setMessage("");
      invalidate();
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Couldn't send request.");
    } finally { setBusy(false); }
  }

  async function toggleSave() {
    try {
      const res = await adoptionApi.toggleSaved(id);
      setSaved(res.saved);
      toast.success(res.saved ? "Saved" : "Removed from saved");
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Couldn't save.");
    }
  }

  function onDelete() { setConfirmDelete(true); }
  async function confirmDeleteListing() {
    try { await adoptionApi.remove(id); toast.success("Deleted."); window.history.back(); }
    catch (err: any) { toast.error(err?.response?.data?.error?.message ?? "Delete failed."); }
  }

  async function onSubmit() {
    try { await adoptionApi.submit(id); toast.success("Submitted for review."); invalidate(); }
    catch (err: any) { toast.error(err?.response?.data?.error?.message ?? "Submit failed."); }
  }

  async function approve() {
    try { await adoptionApi.approve(id); toast.success("Approved."); invalidate(); }
    catch (err: any) { toast.error(err?.response?.data?.error?.message ?? "Approve failed."); }
  }

  if (isLoading) {
    return <div className="max-w-4xl mx-auto space-y-3"><Skeleton className="aspect-[2/1]" /><Skeleton className="h-40" /></div>;
  }
  if (!listing) {
    return (
      <div className="max-w-3xl mx-auto space-y-3">
        <Button variant="ghost" size="sm" asChild><Link to="/adoption"><ArrowLeft className="h-4 w-4 mr-1" /> Back</Link></Button>
        <EmptyState title="Listing not found" description="It may have been removed or unapproved." />
      </div>
    );
  }

  const isSavedNow = saved ?? listing.isSaved;
  const canApply = !listing.isOwn && listing.status === "Approved";

  return (
    <div className="max-w-5xl mx-auto space-y-4">
      <Button variant="ghost" size="sm" asChild><Link to="/adoption"><ArrowLeft className="h-4 w-4 mr-1" /> Back</Link></Button>

      <div className="grid lg:grid-cols-[1fr_22rem] gap-4">
        {/* Main column */}
        <div className="space-y-4">
          <Card className="overflow-hidden">
            <div className="aspect-[2/1] bg-muted">
              {listing.photoUrls[0]
                ? <img src={listing.photoUrls[0]} className="object-cover w-full h-full" />
                : <div className="flex items-center justify-center h-full text-muted-foreground"><HeartHandshake className="h-16 w-16" /></div>}
            </div>
            {listing.photoUrls.length > 1 && (
              <div className="grid grid-cols-5 gap-1 p-2 bg-muted/40">
                {listing.photoUrls.slice(0, 5).map((src, i) => (
                  <div key={i} className="aspect-square rounded overflow-hidden">
                    <img src={src} className="object-cover w-full h-full" />
                  </div>
                ))}
              </div>
            )}
            <CardContent className="pt-6 space-y-4">
              <div className="flex items-start justify-between gap-3">
                <div>
                  <h1 className="text-2xl font-bold">{listing.title}</h1>
                  {listing.petName && <p className="text-sm text-muted-foreground">Meet {listing.petName}</p>}
                </div>
                <Badge variant={STATUS_VARIANT[listing.status]}>{prettyStatus(listing.status)}</Badge>
              </div>
              <div className="flex flex-wrap gap-2 text-xs">
                <Badge variant="outline">{listing.animalType}</Badge>
                {listing.breed && <Badge variant="outline">{listing.breed}</Badge>}
                {listing.size && <Badge variant="outline">{prettySize(listing.size)}</Badge>}
                {listing.gender !== "Unknown" && <Badge variant="outline">{listing.gender}</Badge>}
                {listing.ageMonths != null && <Badge variant="outline">{prettyAge(listing.ageMonths)}</Badge>}
                {listing.color && <Badge variant="outline">{listing.color}</Badge>}
              </div>
              {listing.description && (
                <p className="text-sm whitespace-pre-wrap">{listing.description}</p>
              )}
              {listing.location && (
                <p className="text-xs text-muted-foreground flex items-center gap-1"><MapPin className="h-3 w-3" /> {listing.location}</p>
              )}
            </CardContent>
          </Card>

          {/* Details grid */}
          <div className="grid sm:grid-cols-2 gap-3">
            <DetailCard icon={Stethoscope} title="Health">
              <DetailRow label="Vaccinated" value={listing.vaccinated ? "Yes" : "No"} />
              <DetailRow label="Neutered / spayed" value={listing.neuteredSpayed ? "Yes" : "No"} />
              {listing.vaccinationDetails && <DetailNote text={listing.vaccinationDetails} />}
              {listing.healthCondition && <DetailNote text={listing.healthCondition} />}
            </DetailCard>
            <DetailCard icon={Sparkles} title="Behavior">
              <DetailRow label="Good with children" value={triLabel(listing.goodWithChildren)} icon={Baby} />
              <DetailRow label="Good with other pets" value={triLabel(listing.goodWithOtherPets)} icon={PawPrint} />
              {listing.behaviorNotes && <DetailNote text={listing.behaviorNotes} />}
            </DetailCard>
            <DetailCard icon={HeartHandshake} title="Adoption">
              <DetailRow label="Fee" value={listing.adoptionFee > 0 ? `$${listing.adoptionFee.toFixed(2)}` : "Free"} />
              <DetailRow label="Contact preference" value={listing.contactPreference} />
              {listing.reasonForListing && <DetailNote text={listing.reasonForListing} />}
            </DetailCard>
            <DetailCard icon={Cat} title="Owner">
              <div className="flex items-center gap-3">
                <Avatar><AvatarImage src={listing.ownerAvatarUrl ?? undefined} /><AvatarFallback>{listing.ownerDisplayName[0]}</AvatarFallback></Avatar>
                <div className="min-w-0">
                  <Link to={`/u/${listing.ownerId}`} className="text-sm font-medium hover:underline truncate block">
                    {listing.ownerDisplayName}
                  </Link>
                  <p className="text-xs text-muted-foreground">Listed {new Date(listing.createdAt).toLocaleDateString()}</p>
                </div>
              </div>
            </DetailCard>
          </div>
        </div>

        {/* Sidebar actions */}
        <div className="space-y-3 lg:sticky lg:top-4 h-fit">
          <Card>
            <CardContent className="pt-6 space-y-3">
              {!listing.isOwn ? (
                <>
                  <MessageOwnerButton ownerId={listing.ownerId} listingId={listing.id} />
                  <Button variant="outline" className="w-full" onClick={toggleSave}>
                    <Bookmark className={`h-4 w-4 mr-2 ${isSavedNow ? "fill-current" : ""}`} />
                    {isSavedNow ? "Saved" : "Save"}
                  </Button>
                </>
              ) : (
                <>
                  <Button variant="outline" className="w-full" onClick={() => setEditing(true)}>
                    <Pencil className="h-4 w-4 mr-2" /> Edit listing
                  </Button>
                  {listing.status === "Draft" && (
                    <Button className="w-full" onClick={onSubmit}>Submit for approval</Button>
                  )}
                  {listing.status === "Approved" && (
                    <Button className="w-full" onClick={() => setMarking(true)}>
                      <CheckCircle className="h-4 w-4 mr-2" /> Mark as adopted
                    </Button>
                  )}
                  <Button variant="destructive" className="w-full" onClick={onDelete}>
                    <Trash2 className="h-4 w-4 mr-2" /> Delete
                  </Button>
                </>
              )}

              {/* Admin moderation block */}
              {listing.status === "PendingApproval" && (
                <Can anyOf={["adoption.approve", "adoption.reject"]}>
                  <Separator />
                  <p className="text-xs font-semibold text-muted-foreground">Moderator actions</p>
                  <Can permission="adoption.approve">
                    <Button className="w-full" onClick={approve}>
                      <ShieldCheck className="h-4 w-4 mr-2" /> Approve
                    </Button>
                  </Can>
                  <Can permission="adoption.reject">
                    <Button variant="destructive" className="w-full" onClick={() => setRejecting(true)}>
                      <ShieldX className="h-4 w-4 mr-2" /> Reject
                    </Button>
                  </Can>
                </Can>
              )}
            </CardContent>
          </Card>

          {canApply && (
            <Card>
              <CardContent className="pt-6 space-y-3">
                <p className="font-semibold text-sm">Express interest</p>
                <Textarea
                  placeholder="Tell the owner about your home, experience with pets, anything that would help your application..."
                  rows={5} value={message} onChange={(e) => setMessage(e.target.value)}
                />
                <Button onClick={apply} disabled={!message.trim() || busy} className="w-full">
                  {busy ? "Sending..." : "Send request"}
                </Button>
              </CardContent>
            </Card>
          )}

          {listing.adminNotes && (
            <Card>
              <CardContent className="pt-6 space-y-1">
                <p className="text-xs font-semibold text-muted-foreground">Admin notes</p>
                <p className="text-sm">{listing.adminNotes}</p>
              </CardContent>
            </Card>
          )}
        </div>
      </div>

      {editing && <EditAdoptionDialog open={editing} onOpenChange={setEditing} listing={listing} onSaved={invalidate} />}
      {marking && <MarkAsAdoptedDialog open={marking} onOpenChange={setMarking} listingId={id} onDone={invalidate} />}
      {rejecting && (
        <RejectReasonModal
          open={rejecting} onOpenChange={setRejecting}
          onConfirm={async (reason) => { await adoptionApi.reject(id, reason); invalidate(); }}
        />
      )}

      <ConfirmDialog
        open={confirmDelete}
        onOpenChange={setConfirmDelete}
        title="Delete this listing?"
        description="The listing is removed and applicants will no longer see it."
        confirmLabel="Delete"
        destructive
        onConfirm={confirmDeleteListing}
      />
    </div>
  );
}

function DetailCard({ icon: Icon, title, children }: any) {
  return (
    <Card>
      <CardContent className="pt-5 space-y-2">
        <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground flex items-center gap-1">
          <Icon className="h-3.5 w-3.5" /> {title}
        </p>
        {children}
      </CardContent>
    </Card>
  );
}
function DetailRow({ label, value, icon: Icon }: any) {
  return (
    <div className="flex justify-between text-sm">
      <span className="text-muted-foreground flex items-center gap-1">{Icon && <Icon className="h-3 w-3" />} {label}</span>
      <span className="font-medium">{value}</span>
    </div>
  );
}
function DetailNote({ text }: { text: string }) {
  return <p className="text-xs text-muted-foreground whitespace-pre-wrap">{text}</p>;
}

function prettyStatus(s: AdoptionListingStatus) {
  return s === "PendingApproval" ? "Pending approval" : s;
}
function prettyAge(months: number) {
  if (months < 12) return `${months} mo`;
  const yrs = Math.floor(months / 12), rem = months % 12;
  return rem === 0 ? `${yrs} yr` : `${yrs} yr ${rem} mo`;
}
function prettySize(s: string) { return s.replace("ExtraLarge", "Extra large"); }
function triLabel(v?: boolean | null) { return v == null ? "Unknown" : v ? "Yes" : "No"; }
