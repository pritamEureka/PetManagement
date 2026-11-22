import { useRef, useState } from "react";
import axios from "axios";
import { Loader2, Upload } from "lucide-react";
import { Button } from "@/components/ui/button";
import { toast } from "@/components/ui/sonner";
import { api } from "@/api/client";
import { confirmUpload } from "@/lib/uploadConfirm";
import { compressImage } from "@/lib/imageProcess";
import { useI18n } from "@/lib/i18n";

const MAX_BYTES = 10 * 1024 * 1024;

export interface UploadOptions {
  /** 0..100 */
  onProgress?: (percent: number) => void;
  signal?: AbortSignal;
}

/**
 * Presigned upload to object storage (same flow as message attachments):
 *   POST /media/presign -> PUT the file to the signed URL -> use publicUrl.
 * Backward compatible: `opts` is optional.
 */
export async function uploadFile(file: File, opts: UploadOptions = {}): Promise<string> {
  if (file.size > MAX_BYTES) throw new Error("Files must be 10 MB or smaller.");
  const presign = await api
    .post("/media/presign", { fileName: file.name, contentType: file.type || "application/octet-stream" }, { signal: opts.signal })
    .then((r) => r.data?.data ?? r.data);
  await axios.put(presign.url, file, {
    headers: { "Content-Type": file.type || "application/octet-stream" },
    signal: opts.signal,
    onUploadProgress: (e) => {
      if (opts.onProgress && e.total) opts.onProgress(Math.min(100, Math.round((e.loaded / e.total) * 100)));
    },
  });
  opts.onProgress?.(100);
  // Post-upload verification (magic bytes, scanner, metadata strip); use the confirmed URL.
  return await confirmUpload(presign.key as string, file.type || "application/octet-stream", opts.signal);
}

interface Props {
  onUploaded: (url: string, file: File) => void;
  accept?: string;
  label?: string;
  size?: "sm" | "default";
  variant?: "outline" | "secondary" | "ghost";
  disabled?: boolean;
  /** Downscale/re-encode images before upload (max 1600px, q0.82). Default true. */
  compress?: boolean;
  /** Accessible label override (defaults to `label`). */
  "aria-label"?: string;
}

export function FileUploadButton({ onUploaded, accept = "image/*", label, size = "sm", variant = "outline", disabled, compress = true, ...rest }: Props) {
  const { t } = useI18n();
  const input = useRef<HTMLInputElement>(null);
  const abort = useRef<AbortController | null>(null);
  const [busy, setBusy] = useState(false);
  const [progress, setProgress] = useState<number | null>(null);
  const text = label ?? t("upload.label");

  async function onPick(e: React.ChangeEvent<HTMLInputElement>) {
    const picked = e.target.files?.[0];
    e.target.value = "";
    if (!picked) return;
    setBusy(true);
    setProgress(0);
    abort.current = new AbortController();
    try {
      const file = compress && picked.type.startsWith("image/") ? await compressImage(picked).catch(() => picked) : picked;
      onUploaded(await uploadFile(file, { onProgress: setProgress, signal: abort.current.signal }), file);
    } catch (err: any) {
      if (!axios.isCancel(err)) toast.error(err?.message ?? t("upload.failed"));
    } finally {
      setBusy(false);
      setProgress(null);
      abort.current = null;
    }
  }

  return (
    <>
      <input ref={input} type="file" accept={accept} className="sr-only" tabIndex={-1} aria-hidden="true" onChange={onPick} />
      <Button
        type="button"
        size={size}
        variant={variant}
        disabled={disabled}
        aria-busy={busy || undefined}
        aria-label={rest["aria-label"]}
        className="min-h-9"
        onClick={() => (busy ? abort.current?.abort() : input.current?.click())}
      >
        {busy ? <Loader2 aria-hidden="true" className="h-3.5 w-3.5 me-1 animate-spin" /> : <Upload aria-hidden="true" className="h-3.5 w-3.5 me-1" />}
        {busy ? `${t("upload.uploading")}${progress ? ` ${progress}%` : ""} · ${t("common.cancel")}` : text}
      </Button>
      {busy && progress !== null && (
        <span role="progressbar" aria-label={t("upload.uploading")} aria-valuemin={0} aria-valuemax={100} aria-valuenow={progress} className="sr-only" />
      )}
    </>
  );
}
