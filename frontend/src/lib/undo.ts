import { toast } from "sonner";
import { t } from "@/lib/i18n";

/**
 * Soft-commit undo: the UI updates immediately, a toast offers "Undo" for
 * `delayMs` (default 8s), and the server call only runs once that window closes.
 * Pending actions are flushed (committed) on route change and page hide.
 */
export const UNDO_DELAY_MS = 8000;

export interface UndoableOptions {
  /** Toast text, e.g. "Notification dismissed". */
  message: string;
  /** Server call, executed after the delay unless undone. */
  commit: () => Promise<unknown> | unknown;
  /** Restores UI state if the user hits Undo (or the commit fails). */
  rollback?: () => void;
  onCommitError?: (err: unknown) => void;
  delayMs?: number;
  undoLabel?: string;
  /** De-dupes: scheduling the same key again commits the earlier action first. */
  key?: string;
}

interface Pending {
  key: string;
  timer: ReturnType<typeof setTimeout>;
  toastId: string | number;
  run: () => Promise<void>;
  cancel: () => void;
}

const pending = new Map<string, Pending>();
let seq = 0;

export function scheduleUndoable(opts: UndoableOptions): { undo: () => void; flush: () => Promise<void> } {
  const key = opts.key ?? `undo-${++seq}`;
  const existing = pending.get(key);
  if (existing) void existing.run();

  const delay = opts.delayMs ?? UNDO_DELAY_MS;
  let settled = false;

  const finish = () => {
    settled = true;
    pending.delete(key);
  };

  const run = async () => {
    if (settled) return;
    clearTimeout(timer);
    toast.dismiss(toastId);
    finish();
    try {
      await opts.commit();
    } catch (err) {
      opts.rollback?.();
      if (opts.onCommitError) opts.onCommitError(err);
      else toast.error(t("undo.failed"));
    }
  };

  const cancel = () => {
    if (settled) return;
    clearTimeout(timer);
    toast.dismiss(toastId);
    finish();
    opts.rollback?.();
  };

  const timer = setTimeout(() => void run(), delay);
  const toastId = toast(opts.message, {
    duration: delay,
    action: { label: opts.undoLabel ?? t("undo.action"), onClick: cancel },
  });

  pending.set(key, { key, timer, toastId, run, cancel });
  return { undo: cancel, flush: run };
}

/** Commit every pending undoable action right now (route change / unload). */
export async function flushUndoable(): Promise<void> {
  await Promise.all([...pending.values()].map((p) => p.run()));
}

export function pendingUndoableCount(): number {
  return pending.size;
}

if (typeof window !== "undefined") {
  // Best effort: start the commits when the page is being hidden/unloaded.
  window.addEventListener("pagehide", () => {
    void flushUndoable();
  });
}
