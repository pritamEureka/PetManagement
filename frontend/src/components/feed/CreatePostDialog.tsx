import { useState } from "react";
import { useForm, useFieldArray } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ImagePlus, Hash, MapPin, X } from "lucide-react";
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Badge } from "@/components/ui/badge";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { createPostSchema, type CreatePostInput, animalTypes } from "@/lib/schemas";
import { postsApi } from "@/api/posts";
import { toast } from "@/components/ui/sonner";

interface Props {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  onCreated: () => void;
}

export function CreatePostDialog({ open, onOpenChange, onCreated }: Props) {
  const { register, handleSubmit, watch, setValue, reset, formState: { errors, isSubmitting } } =
    useForm<CreatePostInput>({
      resolver: zodResolver(createPostSchema),
      defaultValues: { content: "", mediaUrls: [], hashtags: [] }
    });
  const media = watch("mediaUrls") ?? [];
  const hashtags = watch("hashtags") ?? [];

  const [mediaInput, setMediaInput] = useState("");
  const [tagInput, setTagInput] = useState("");

  function addMedia() {
    const url = mediaInput.trim();
    if (!url) return;
    let parsed: URL;
    try { parsed = new URL(url); }
    catch { toast.error("Enter a valid image URL"); return; }
    // Reject javascript:, data:, file:, blob:, etc. — only http(s) URLs may be
    // embedded as post media. https is strongly preferred but http is accepted
    // for now to keep dev/staging assets working.
    if (parsed.protocol !== "https:" && parsed.protocol !== "http:") {
      toast.error("Only https:// (or http://) URLs are allowed.");
      return;
    }
    setValue("mediaUrls", [...media, parsed.toString()]);
    setMediaInput("");
  }
  function removeMedia(i: number) {
    setValue("mediaUrls", media.filter((_, idx) => idx !== i));
  }
  function addTag() {
    const raw = tagInput.trim().replace(/^#/, "").toLowerCase();
    if (!raw || hashtags.includes(raw)) { setTagInput(""); return; }
    setValue("hashtags", [...hashtags, raw]);
    setTagInput("");
  }
  function removeTag(t: string) {
    setValue("hashtags", hashtags.filter((x) => x !== t));
  }

  async function onSubmit(values: CreatePostInput) {
    try {
      await postsApi.create({
        ...values,
        animalType: values.animalType,
        mediaUrls: values.mediaUrls && values.mediaUrls.length > 0 ? values.mediaUrls : undefined,
        hashtags: values.hashtags && values.hashtags.length > 0 ? values.hashtags : undefined
      });
      toast.success("Post shared");
      reset(); onCreated(); onOpenChange(false);
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Couldn't publish.");
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Share with the pack</DialogTitle>
          <DialogDescription>Add text, photos, location, and tags. Posts are public to the community.</DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit(onSubmit)} className="space-y-3">
          <Textarea rows={4} placeholder="What's your pet up to today?" {...register("content")} />

          {/* Media */}
          {media.length > 0 && (
            <div className="grid grid-cols-3 gap-2">
              {media.map((url, i) => (
                <div key={i} className="relative aspect-square rounded-md overflow-hidden bg-muted">
                  <img src={url} className="object-cover w-full h-full" />
                  <button type="button"
                    onClick={() => removeMedia(i)}
                    className="absolute top-1 right-1 rounded-full bg-black/60 text-white p-1 hover:bg-black/80">
                    <X className="h-3 w-3" />
                  </button>
                </div>
              ))}
            </div>
          )}
          <div className="flex gap-2">
            <Input value={mediaInput} onChange={(e) => setMediaInput(e.target.value)} placeholder="Paste image URL" />
            <Button type="button" variant="outline" onClick={addMedia}><ImagePlus className="h-4 w-4" /></Button>
          </div>

          {/* Hashtags */}
          {hashtags.length > 0 && (
            <div className="flex flex-wrap gap-1.5">
              {hashtags.map((t) => (
                <Badge key={t} variant="secondary" className="gap-1">
                  #{t}
                  <button type="button" onClick={() => removeTag(t)} className="rounded-full hover:bg-secondary/80"><X className="h-3 w-3" /></button>
                </Badge>
              ))}
            </div>
          )}
          <div className="flex gap-2">
            <Input value={tagInput} onChange={(e) => setTagInput(e.target.value)}
                   onKeyDown={(e) => e.key === "Enter" && (e.preventDefault(), addTag())}
                   placeholder="Add a hashtag and press Enter" />
            <Button type="button" variant="outline" onClick={addTag}><Hash className="h-4 w-4" /></Button>
          </div>

          {/* Location + animal type */}
          <div className="grid grid-cols-2 gap-3">
            <div className="relative">
              <MapPin className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input className="pl-8" placeholder="Location (optional)" {...register("location")} />
            </div>
            <Select value={watch("animalType") ?? ""} onValueChange={(v) => setValue("animalType", v as any)}>
              <SelectTrigger><SelectValue placeholder="Animal type" /></SelectTrigger>
              <SelectContent>
                {animalTypes.map((t) => <SelectItem key={t} value={t}>{t}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>

          {errors.root?.message && <p className="text-xs text-destructive">{errors.root.message}</p>}

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
            <Button type="submit" disabled={isSubmitting}>{isSubmitting ? "Publishing..." : "Publish"}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
