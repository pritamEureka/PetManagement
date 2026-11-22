import { api } from "@/api/client";

/**
 * Post-upload verification for direct-to-S3 uploads. Call right AFTER the presigned PUT completes:
 *   POST /media/confirm { key, declaredContentType }
 * The server re-reads the stored bytes (magic bytes, polyglot, size, pixels, scanner), strips JPEG/PNG metadata when
 * small, and deletes the object + answers 422 when it is not acceptable (the thrown axios error carries that 422).
 * Returns the confirmed final URL (use it instead of presign.publicUrl).
 */
export async function confirmUpload(key: string, contentType: string, signal?: AbortSignal): Promise<string> {
  const res = await api
    .post("/media/confirm", { key, declaredContentType: contentType || "application/octet-stream" }, { signal })
    .then((r) => r.data?.data ?? r.data);
  return (res?.url as string) ?? "";
}
