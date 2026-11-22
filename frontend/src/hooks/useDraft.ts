import { useCallback, useEffect, useRef, useState } from "react";
import { clearDraft, DRAFT_TTL_MS, DRAFT_VERSION, readDraft, writeDraft } from "@/lib/draftStore";
import { useAuthStore } from "@/store/authStore";

export interface UseDraftOptions {
  /** Disable autosave (e.g. while submitting). */
  enabled?: boolean;
  debounceMs?: number;
  ttlMs?: number;
  version?: number;
  /** Optional filter to strip secrets (passwords!) before persisting. */
  sanitize?: (value: any) => any;
}

/**
 * Autosave + recovery for long forms.
 *
 *   const draft = useDraft("profile-edit", form);
 *   <DraftBanner draft={draft} onRestore={(d) => setForm(d)} />
 *   ... on successful submit: draft.clear();
 *
 * - keys are scoped to the signed-in user, versioned, and expire after 7 days
 * - nothing is written until the value differs from its first-render snapshot
 * - nothing is written while a restore prompt is pending (so it can't clobber the saved draft)
 * - storage failures (quota, private mode) are swallowed
 */
export function useDraft<T>(key: string, value: T, opts: UseDraftOptions = {}) {
  const { enabled = true, debounceMs = 800, ttlMs = DRAFT_TTL_MS, version = DRAFT_VERSION, sanitize } = opts;
  const userId = useAuthStore((s) => s.user?.id) ?? "anon";
  const [pending, setPending] = useState<{ data: T; savedAt: number } | null>(() => readDraft<T>(key, { version, ttlMs, userId }));
  const initial = useRef<string | null>(null);
  const clearedAt = useRef<string | null>(null);
  const latest = useRef<string | null>(null);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Re-read when the key/user changes (e.g. editing a different entity).
  useEffect(() => {
    clearedAt.current = null;
    // Re-snapshot the CURRENT value (not null): this effect also runs on mount, and clearing the snapshot there
    // made an untouched form look "changed" and get autosaved.
    initial.current = latest.current;
    setPending(readDraft<T>(key, { version, ttlMs, userId }));
  }, [key, userId, version, ttlMs]);

  const serialized = (() => {
    try {
      return JSON.stringify(sanitize ? sanitize(value) : value);
    } catch {
      return null;
    }
  })();
  latest.current = serialized;
  if (initial.current === null && serialized !== null) initial.current = serialized;

  useEffect(() => {
    if (!enabled || pending || serialized === null || serialized === clearedAt.current) return;
    if (serialized === initial.current) return;
    const id = (timer.current = setTimeout(() => {
      writeDraft(key, JSON.parse(serialized), { version, userId });
    }, debounceMs));
    return () => clearTimeout(id);
  }, [serialized, enabled, pending, key, debounceMs, version, userId]);

  const clear = useCallback(() => {
    if (timer.current) clearTimeout(timer.current);
    clearedAt.current = latest.current;
    clearDraft(key, userId);
    setPending(null);
  }, [key, userId]);

  const restore = useCallback((): T | null => {
    const d = pending?.data ?? null;
    setPending(null);
    return d;
  }, [pending]);

  const discard = useCallback(() => {
    clearDraft(key, userId);
    setPending(null);
  }, [key, userId]);

  return { hasDraft: pending !== null, savedAt: pending?.savedAt ?? null, restore, discard, clear };
}

export type DraftHandle = ReturnType<typeof useDraft>;
