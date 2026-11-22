import { useCallback, useRef, useState } from "react";
import axios from "axios";
import { AlertCircle, Crop, ImagePlus, RotateCw, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { uploadFile } from "@/components/common/FileUploadButton";
import { ImageCropper, type CropAspect } from "@/components/common/ImageCropper";
import { compressImage } from "@/lib/imageProcess";
import { useI18n } from "@/lib/i18n";
import { useAnnounce } from "@/components/common/a11y";

type Status = "queued" | "processing" | "uploading" | "done" | "error";
interface Item {
  id: string;
  file: File;
  preview: string;
  status: Status;
  progress: number;
  url?: string;
  error?: string;
}

interface Props {
  /** Called with all successfully uploaded URLs (in order) whenever the set changes. */
  onChange: (urls: string[]) => void;
  /** Already-uploaded URLs to show (read-only previews, kept in the output). */
  initialUrls?: string[];
  multiple?: boolean;
  maxFiles?: number;
  /** When set (and single-file), opens the crop dialog before uploading. */
  cropAspect?: CropAspect;
  label?: string;
  accept?: string;
  disabled?: boolean;
}

let seq = 0;

/**
 * Photo uploader: previews, optional crop (single file), client-side
 * compression (1600px / q0.82, EXIF honored), per-file progress, cancel and retry.
 */
export function ImageUploader({ onChange, initialUrls = [], multiple = false, maxFiles = 8, cropAspect, label, accept = "image/*", disabled }: Props) {
  const { t } = useI18n();
  const announce = useAnnounce();
  const input = useRef<HTMLInputElement>(null);
  const aborts = useRef(new Map<string, AbortController>());
  const [items, setItems] = useState<Item[]>([]);
  const [existing, setExisting] = useState<string[]>(initialUrls);
  const [cropping, setCropping] = useState<File | null>(null);

  const emit = useCallback(
    (nextItems: Item[], nextExisting: string[]) => {
      onChange([...nextExisting, ...nextItems.filter((i) => i.status === "done" && i.url).map((i) => i.url!)]);
    },
    [onChange],
  );

  const patch = useCallback(
    (id: string, p: Partial<Item>) => {
      setItems((cur) => {
        const next = cur.map((i) => (i.id === id ? { ...i, ...p } : i));
        if (p.status === "done") queueMicrotask(() => emit(next, existing));
        return next;
      });
    },
    [emit, existing],
  );

  const run = useCallback(
    async (item: Item) => {
      const ac = new AbortController();
      aborts.current.set(item.id, ac);
      try {
        patch(item.id, { status: "processing", error: undefined, progress: 0 });
        const processed = await compressImage(item.file).catch(() => item.file);
        patch(item.id, { status: "uploading" });
        const url = await uploadFile(processed, { signal: ac.signal, onProgress: (p) => patch(item.id, { progress: p }) });
        patch(item.id, { status: "done", url, progress: 100 });
        announce(t("upload.done"));
      } catch (err: any) {
        if (axios.isCancel(err) || err?.name === "CanceledError") {
          setItems((cur) => cur.filter((i) => i.id !== item.id));
        } else {
          patch(item.id, { status: "error", error: err?.message ?? t("upload.failed") });
        }
      } finally {
        aborts.current.delete(item.id);
      }
    },
    [patch, announce, t],
  );

  const enqueue = useCallback(
    (files: File[]) => {
      const room = Math.max(0, maxFiles - existing.length - items.length);
      const fresh: Item[] = files
        .filter((f) => f.type.startsWith("image/"))
        .slice(0, room)
        .map((file) => ({ id: `u${++seq}`, file, preview: URL.createObjectURL(file), status: "queued" as Status, progress: 0 }));
      if (fresh.length === 0) return;
      setItems((cur) => [...cur, ...fresh]);
      fresh.forEach((f) => void run(f));
    },
    [existing.length, items.length, maxFiles, run],
  );

  function onPick(e: React.ChangeEvent<HTMLInputElement>) {
    const files = Array.from(e.target.files ?? []);
    e.target.value = "";
    if (files.length === 0) return;
    if (cropAspect && !multiple) setCropping(files[0]);
    else enqueue(files);
  }

  function remove(item: Item) {
    aborts.current.get(item.id)?.abort();
    URL.revokeObjectURL(item.preview);
    const next = items.filter((i) => i.id !== item.id);
    setItems(next);
    emit(next, existing);
  }

  return (
    <div className="space-y-2">
      <input ref={input} type="file" accept={accept} multiple={multiple} className="sr-only" tabIndex={-1} aria-hidden="true" onChange={onPick} />
      <Button
        type="button"
        variant="outline"
        disabled={disabled || existing.length + items.length >= maxFiles}
        onClick={() => input.current?.click()}
        className="min-h-11"
      >
        <ImagePlus aria-hidden="true" className="h-4 w-4 me-2" /> {label ?? (multiple ? t("upload.addPhotos") : t("upload.addPhoto"))}
      </Button>

      {(existing.length > 0 || items.length > 0) && (
        <ul className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-3" aria-label={t("upload.photos")}>
          {existing.map((u) => (
            <li key={u} className="relative aspect-square overflow-hidden rounded-md border">
              <img src={u} alt="" className="h-full w-full object-cover" />
              <button
                type="button"
                aria-label={t("upload.remove")}
                className="absolute end-1 top-1 grid h-8 w-8 place-items-center rounded-full bg-background/90 shadow focus-visible:ring-2 focus-visible:ring-ring"
                onClick={() => {
                  const next = existing.filter((x) => x !== u);
                  setExisting(next);
                  emit(items, next);
                }}
              >
                <X aria-hidden="true" className="h-4 w-4" />
              </button>
            </li>
          ))}
          {items.map((i) => (
            <li key={i.id} className="relative aspect-square overflow-hidden rounded-md border">
              <img src={i.preview} alt={i.file.name} className="h-full w-full object-cover" />
              {i.status !== "done" && (
                <div className="absolute inset-x-0 bottom-0 bg-background/90 p-2 text-xs space-y-1">
                  {i.status === "error" ? (
                    <div className="flex items-center gap-1 text-destructive" role="alert">
                      <AlertCircle aria-hidden="true" className="h-3.5 w-3.5 shrink-0" />
                      <span className="truncate">{i.error}</span>
                      <button
                        type="button"
                        aria-label={t("upload.retry")}
                        className="ms-auto grid h-8 w-8 shrink-0 place-items-center rounded focus-visible:ring-2 focus-visible:ring-ring"
                        onClick={() => void run(i)}
                      >
                        <RotateCw aria-hidden="true" className="h-4 w-4" />
                      </button>
                    </div>
                  ) : (
                    <>
                      <div
                        role="progressbar"
                        aria-label={`${t("upload.uploading")}: ${i.file.name}`}
                        aria-valuemin={0}
                        aria-valuemax={100}
                        aria-valuenow={i.progress}
                        className="h-1.5 w-full overflow-hidden rounded bg-muted"
                      >
                        <div className="h-full bg-primary transition-[width]" style={{ width: `${i.progress}%` }} />
                      </div>
                      <span>{i.status === "processing" ? t("upload.processing") : `${i.progress}%`}</span>
                    </>
                  )}
                </div>
              )}
              <button
                type="button"
                aria-label={i.status === "uploading" || i.status === "processing" ? t("common.cancel") : t("upload.remove")}
                className="absolute end-1 top-1 grid h-8 w-8 place-items-center rounded-full bg-background/90 shadow focus-visible:ring-2 focus-visible:ring-ring"
                onClick={() => remove(i)}
              >
                <X aria-hidden="true" className="h-4 w-4" />
              </button>
            </li>
          ))}
        </ul>
      )}

      <Dialog open={!!cropping} onOpenChange={(o) => !o && setCropping(null)}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <Crop aria-hidden="true" className="h-4 w-4" /> {t("upload.cropTitle")}
            </DialogTitle>
            <DialogDescription>{t("upload.cropHint")}</DialogDescription>
          </DialogHeader>
          {cropping && (
            <ImageCropper
              file={cropping}
              aspect={cropAspect ?? "square"}
              onCancel={() => setCropping(null)}
              onCropped={(f) => {
                setCropping(null);
                enqueue([f]);
              }}
            />
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
