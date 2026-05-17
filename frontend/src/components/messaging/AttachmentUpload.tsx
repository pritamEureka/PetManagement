import { useRef, useState } from "react";
import { Paperclip, X, Image as ImageIcon, FileText } from "lucide-react";
import axios from "axios";
import { Button } from "@/components/ui/button";
import { api } from "@/api/client";
import { toast } from "@/components/ui/sonner";
import type { Attachment } from "@/api/messages";

interface Props {
  attachments: Attachment[];
  onChange: (next: Attachment[]) => void;
  disabled?: boolean;
}

/**
 * Upload flow:
 *   1. POST /api/media/presign  -> { url, key, publicUrl, expiresAt }
 *   2. PUT the file to `url`
 *   3. Push { url: publicUrl, mimeType, sizeBytes, fileName } onto the message draft.
 */
export function AttachmentUpload({ attachments, onChange, disabled }: Props) {
  const fileInput = useRef<HTMLInputElement | null>(null);
  const [busy, setBusy] = useState(false);

  async function upload(file: File) {
    setBusy(true);
    try {
      const presign = await api.post("/media/presign", { fileName: file.name, contentType: file.type })
        .then((r) => r.data?.data ?? r.data);
      await axios.put(presign.url, file, { headers: { "Content-Type": file.type } });
      const next: Attachment = {
        url: presign.publicUrl,
        mimeType: file.type || "application/octet-stream",
        sizeBytes: file.size,
        fileName: file.name
      };
      onChange([...attachments, next]);
    } catch (err: any) {
      console.error(err);
      toast.error("Upload failed.");
    } finally { setBusy(false); }
  }

  async function onPick(e: React.ChangeEvent<HTMLInputElement>) {
    const files = Array.from(e.target.files ?? []);
    for (const f of files) await upload(f);
    e.target.value = "";
  }

  function remove(i: number) {
    onChange(attachments.filter((_, idx) => idx !== i));
  }

  return (
    <div className="space-y-2">
      {attachments.length > 0 && (
        <div className="flex flex-wrap gap-2">
          {attachments.map((a, i) => (
            <div key={i} className="relative rounded-md border bg-muted/50 p-1.5 pr-7 text-xs flex items-center gap-1.5 max-w-[14rem]">
              {a.mimeType.startsWith("image/") ? <ImageIcon className="h-3 w-3" /> : <FileText className="h-3 w-3" />}
              <span className="truncate">{a.fileName}</span>
              <button onClick={() => remove(i)} className="absolute right-1 top-1 rounded-full hover:bg-background p-0.5">
                <X className="h-3 w-3" />
              </button>
            </div>
          ))}
        </div>
      )}
      <input ref={fileInput} type="file" multiple className="hidden" onChange={onPick} />
      <Button type="button" variant="ghost" size="icon" disabled={disabled || busy}
              onClick={() => fileInput.current?.click()} title="Attach">
        <Paperclip className="h-4 w-4" />
      </Button>
    </div>
  );
}
