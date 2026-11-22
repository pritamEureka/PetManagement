import type { AxiosRequestConfig } from "axios";

/** Header the backend [Idempotent] filter reads. */
export const IDEMPOTENCY_HEADER = "Idempotency-Key";

/** A fresh random key (UUID v4). Generate ONE per user intent and reuse it for retries of that same request. */
export function newIdempotencyKey(): string {
  const c = typeof globalThis !== "undefined" ? (globalThis.crypto as Crypto | undefined) : undefined;
  if (c?.randomUUID) return c.randomUUID();
  const bytes = new Uint8Array(16);
  if (c?.getRandomValues) c.getRandomValues(bytes);
  else for (let i = 0; i < bytes.length; i++) bytes[i] = Math.floor(Math.random() * 256);
  bytes[6] = (bytes[6] & 0x0f) | 0x40;
  bytes[8] = (bytes[8] & 0x3f) | 0x80;
  const h = Array.from(bytes, (b) => b.toString(16).padStart(2, "0")).join("");
  return `${h.slice(0, 8)}-${h.slice(8, 12)}-${h.slice(12, 16)}-${h.slice(16, 20)}-${h.slice(20)}`;
}

/**
 * Returns an axios request config carrying an Idempotency-Key header (merged into any existing headers).
 * Pass an explicit `key` to deliberately repeat the same logical request (e.g. a manual retry button).
 */
export function withIdempotency(config: AxiosRequestConfig = {}, key: string = newIdempotencyKey()): AxiosRequestConfig {
  return {
    ...config,
    headers: { ...((config.headers as Record<string, unknown> | undefined) ?? {}), [IDEMPOTENCY_HEADER]: key },
  } as AxiosRequestConfig;
}
