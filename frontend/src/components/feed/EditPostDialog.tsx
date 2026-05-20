import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Hash, X } from "lucide-react";
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Badge } from "@/components/ui/badge";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { createPostSchema, type CreatePostInput, animalTypes } from "@/lib/schemas";
import { postsApi, type FeedItem } from "@/api/posts";
import { toast } from "@/components/ui/sonner";

interface Props {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  post: FeedItem;
  onSaved: () => void;
}

export function EditPostDialog({ open, onOpenChange, post, onSaved }: Props) {
  const { register, handleSubmit, watch, setValue, reset, formState: { isSubmitting } } =
    useForm<CreatePostInput>({
      resolver: zodResolver(createPostSchema),
      defaultValues: { content: "", hashtags: [] }
    });
  const hashtags = watch("hashtags") ?? [];
  const [tagInput, setTagInput] = useState("");

  useEffect(() => {
    if (open) {
      reset({
        content: post.content ?? "",
        animalType: (post.animalType as any) ?? undefined,
        location: post.location ?? "",
        hashtags: post.hashtags ?? [],
        mediaUrls: post.media.map((m) => m.url)
      });
    }
  }, [open, post, reset]);

  function addTag() {
    const raw = tagInput.trim().replace(/^#/, "").toLowerCase();
    if (!raw || hashtags.includes(raw)) { setTagInput(""); return; }
    setValue("hashtags", [...hashtags, raw]);
    setTagInput("");
  }

  async function onSubmit(values: CreatePostInput) {
    try {
      await postsApi.update(post.id, {
        content: values.content || "",
        animalType: values.animalType,
        location: values.location || "",
        hashtags: values.hashtags
      });
      toast.success("Post updated");
      onSaved(); onOpenChange(false);
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Couldn't update.");
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Edit post</DialogTitle>
          <DialogDescription>Media can't be edited yet — delete and repost to swap photos.</DialogDescription>
        </DialogHeader>
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-3">
          <Textarea rows={4} {...register("content")} />

          {hashtags.length > 0 && (
            <div className="flex flex-wrap gap-1.5">
              {hashtags.map((t) => (
                <Badge key={t} variant="secondary" className="gap-1">
                  #{t}
                  <button type="button" onClick={() => setValue("hashtags", hashtags.filter((x) => x !== t))} className="rounded-full hover:bg-secondary/80">
                    <X className="h-3 w-3" />
                  </button>
                </Badge>
              ))}
            </div>
          )}
          <div className="flex gap-2">
            <Input value={tagInput} onChange={(e) => setTagInput(e.target.value)}
                   onKeyDown={(e) => e.key === "Enter" && (e.preventDefault(), addTag())}
                   placeholder="Add a hashtag" />
            <Button type="button" variant="outline" onClick={addTag}><Hash className="h-4 w-4" /></Button>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <Input placeholder="Location" {...register("location")} />
            <Select value={watch("animalType") ?? ""} onValueChange={(v) => setValue("animalType", v as any)}>
              <SelectTrigger><SelectValue placeholder="Animal type" /></SelectTrigger>
              <SelectContent>
                {animalTypes.map((t) => <SelectItem key={t} value={t}>{t}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
            <Button type="submit" disabled={isSubmitting}>{isSubmitting ? "Saving..." : "Save changes"}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
