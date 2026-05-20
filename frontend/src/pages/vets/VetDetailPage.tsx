import { useQuery } from "@tanstack/react-query";
import { Link, useParams } from "react-router-dom";
import { ArrowLeft, Calendar, Stethoscope, MapPin, Star, Video, Building2 } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Avatar, AvatarImage, AvatarFallback } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Separator } from "@/components/ui/separator";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { vetsApi } from "@/api/vets";
import { EmptyState } from "@/components/common/EmptyState";

export function VetDetailPage() {
  const { id = "" } = useParams();
  const { data: doctor, isLoading } = useQuery({
    queryKey: ["vet", id], queryFn: () => vetsApi.get(id), enabled: !!id
  });
  const { data: reviews } = useQuery({
    queryKey: ["vet-reviews", id], queryFn: () => vetsApi.reviews(id), enabled: !!id
  });

  if (isLoading) return <div className="max-w-3xl mx-auto space-y-3"><Skeleton className="h-40" /><Skeleton className="h-24" /></div>;
  if (!doctor) {
    return (
      <div className="max-w-3xl mx-auto space-y-3">
        <Button variant="ghost" size="sm" asChild><Link to="/vets"><ArrowLeft className="h-4 w-4 mr-1" /> Back</Link></Button>
        <EmptyState title="Doctor not found" />
      </div>
    );
  }

  return (
    <div className="max-w-4xl mx-auto space-y-4">
      <Button variant="ghost" size="sm" asChild><Link to="/vets"><ArrowLeft className="h-4 w-4 mr-1" /> All vets</Link></Button>

      <Card>
        <CardHeader className="flex flex-col sm:flex-row gap-3 sm:gap-4 space-y-0">
          <Avatar className="h-16 w-16 sm:h-20 sm:w-20">
            <AvatarImage src={doctor.avatarUrl ?? undefined} />
            <AvatarFallback>{doctor.name[0]}</AvatarFallback>
          </Avatar>
          <div className="flex-1 space-y-1">
            <CardTitle>Dr. {doctor.name}</CardTitle>
            <p className="text-sm text-muted-foreground flex items-center gap-1">
              <Stethoscope className="h-3.5 w-3.5" /> {doctor.specialty ?? "General practice"}
              {doctor.experienceYears ? ` · ${doctor.experienceYears}y experience` : ""}
            </p>
            <p className="text-sm text-muted-foreground flex items-center gap-1">
              <MapPin className="h-3.5 w-3.5" /> {doctor.clinicName ?? ""}{doctor.city ? ` · ${doctor.city}` : ""}{doctor.country ? `, ${doctor.country}` : ""}
            </p>
            <div className="flex items-center flex-wrap gap-2 text-xs">
              {doctor.onlineAvailable && <Badge variant="secondary"><Video className="h-3 w-3 mr-1" /> Online</Badge>}
              {doctor.offlineAvailable && <Badge variant="muted"><Building2 className="h-3 w-3 mr-1" /> In-clinic</Badge>}
              <span className="flex items-center gap-1 text-amber-500"><Star className="h-3 w-3 fill-current" /> {doctor.ratingAverage.toFixed(1)} ({doctor.ratingCount})</span>
              <span className="font-semibold text-foreground">${doctor.consultationFee.toFixed(2)}</span>
            </div>
          </div>
          <Button asChild><Link to={`/vets/${id}/book`}><Calendar className="h-4 w-4 mr-2" /> Book</Link></Button>
        </CardHeader>
        <CardContent className="space-y-3">
          {doctor.about && <p className="text-sm whitespace-pre-wrap">{doctor.about}</p>}
          <Separator />
          <div className="text-xs text-muted-foreground space-y-1">
            <p>Slot length: {doctor.defaultSlotMinutes} min · Cancellation cutoff: {doctor.cancellationCutoffHours} h</p>
            <p>Treats: {doctor.supportedAnimalTypes.join(", ")}</p>
            {doctor.specialties.length > 0 && <p>Specialties: {doctor.specialties.join(", ")}</p>}
          </div>
        </CardContent>
      </Card>

      <Tabs defaultValue="reviews">
        <TabsList><TabsTrigger value="reviews">Reviews</TabsTrigger></TabsList>
        <TabsContent value="reviews">
          {!reviews || reviews.length === 0 ? (
            <Card><CardContent className="py-10 text-center text-sm text-muted-foreground">No reviews yet.</CardContent></Card>
          ) : (
            <div className="space-y-2">
              {reviews.map((r: any) => (
                <Card key={r.id}>
                  <CardContent className="py-3 space-y-1">
                    <div className="flex items-center justify-between">
                      <p className="text-sm font-medium">{r.reviewerName}</p>
                      <div className="flex items-center gap-1 text-amber-500 text-xs">
                        {Array.from({ length: 5 }, (_, i) => (
                          <Star key={i} className={`h-3 w-3 ${i < r.rating ? "fill-current" : "opacity-30"}`} />
                        ))}
                      </div>
                    </div>
                    {r.comment && <p className="text-sm text-muted-foreground">{r.comment}</p>}
                    <p className="text-[10px] text-muted-foreground">{new Date(r.createdAt).toLocaleDateString()}</p>
                  </CardContent>
                </Card>
              ))}
            </div>
          )}
        </TabsContent>
      </Tabs>
    </div>
  );
}
