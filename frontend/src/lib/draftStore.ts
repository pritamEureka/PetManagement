import { safeKeysWithPrefix, safeRead, safeRemove, safeWrite, scopedKey } from "@/lib/safeStorage";

export const DRAFT_VERSION = 1;
export const DRAFT_TTL_MS = 7 * 24 * 60 * 60 * 1000;

interface Envelope<T> {
  v: number;
  savedAt: number;
  data: T;
}

export const draftStorageKey = (key: string, userId?: string) => scopedKey("draft", key, userId);

/** Returns the stored draft or null when missing, expired, or from another schema version. */
export function readDraft<T>(key: string, opts: { version?: number; ttlMs?: number; now?: number; userId?: string } = {}): { data: T; savedAt: number } | null {
  const { version = DRAFT_VERSION, ttlMs = DRAFT_TTL_MS, now = Date.now(), userId } = opts;
  const k = draftStorageKey(key, userId);
  const env = safeRead<Envelope<T> | null>(k, null);
  if (!env || typeof env !== "object" || env.v !== version || typeof env.savedAt !== "number") {
    if (env) safeRemove(k);
    return null;
  }
  if (now - env.savedAt > ttlMs) {
    safeRemove(k);
    return null;
  }
  return { data: env.data, savedAt: env.savedAt };
}

/** Writes a draft. On quota failure evicts the oldest drafts of this user once and retries. */
export function writeDraft<T>(key: string, data: T, opts: { version?: number; now?: number; userId?: string } = {}): boolean {
  const { version = DRAFT_VERSION, now = Date.now(), userId } = opts;
  const k = draftStorageKey(key, userId);
  const env: Envelope<T> = { v: version, savedAt: now, data };
  if (safeWrite(k, env)) return true;
  // Quota exceeded: drop the oldest other drafts and retry once.
  const prefix = scopedKey("draft", "", userId);
  const others = safeKeysWithPrefix(prefix)
    .filter((x) => x !== k)
    .map((x) => ({ x, at: safeRead<Envelope<unknown> | null>(x, null)?.savedAt ?? 0 }))
    .sort((a, b) => a.at - b.at);
  for (const o of others.slice(0, Math.max(1, Math.ceil(others.length / 2)))) safeRemove(o.x);
  return safeWrite(k, env);
}

export function clearDraft(key: string, userId?: string): void {
  safeRemove(draftStorageKey(key, userId));
}
