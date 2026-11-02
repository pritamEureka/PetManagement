import { useRef, useState } from "react";
import axios from "axios";
import { Loader2, Upload } from "lucide-react";
import { Button } from "@/components/ui/button";
import { toast } from "@/components/ui/sonner";
import { api } from "@/api/client";

const MAX_BYTES = 10 * 1024 * 1024;

/**
 * Presigned upload to object storage (same flow as message attachments):
 *   POST /media/presign -> PUT the file to the signed URL -> use publicUrl.
 */
export async function uploadFile(file: File): Promise<string> {
  if (file.size > MAX_BYTES) throw new Error("Files must be 10 MB or smaller.");
  const presign = await api.post("/media/presign", { fileName: file.name, contentType: file.type || "application/octet-stream" })
    .then((r) => r.data?.data ?? r.data);
  await axios.put(presign.url, file, { headers: { "Content-Type": file.type || "application/octet-stream" } });
  return presign.publicUrl as string;
}

interface Props {
  onUploaded: (url: string, file: File) => void;
  accept?: string;
  label?: string;
  size?: "sm" | "default";
  variant?: "outline" | "secondary" | "ghost";
  disabled?: boolean;
}

export function FileUploadButton({ onUploaded, accept = "image/*", label = "Upload", size = "sm", variant = "outline", disabled }: Props) {
  const input = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);

  async function onPick(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    e.target.value = "";
    if (!file) return;
    setBusy(true);
    try {
      onUploaded(await uploadFile(file), file);
    } catch (err: any) {
      toast.error(err?.message ?? "Upload failed.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      <input ref={input} type="file" accept={accept} className="hidden" onChange={onPick} />
      <Button type="button" size={size} variant={variant} disabled={busy || disabled} onClick={() => input.current?.click()}>
        {busy ? <Loader2 className="h-3.5 w-3.5 mr-1 animate-spin" /> : <Upload className="h-3.5 w-3.5 mr-1" />}
        {busy ? "Uploading..." : label}
      </Button>
    </>
  );
}
