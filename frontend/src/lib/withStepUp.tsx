import { createRoot } from "react-dom/client";
import { toast } from "sonner";
import { StepUpDialog } from "@/components/security/StepUpDialog";

/** True when the API rejected the call with the forbidden code "step_up_required". */
export function isStepUpRequired(err: unknown): boolean {
  const e = err as { response?: { status?: number; data?: { error?: { message?: string } } } };
  return e?.response?.status === 403 && e.response.data?.error?.message === "step_up_required";
}

let pending: Promise<boolean> | null = null;

/** Opens the StepUpDialog (self-mounted, no provider needed). Resolves true once verified, false on cancel. */
export function requestStepUp(): Promise<boolean> {
  if (pending) return pending;
  pending = new Promise<boolean>((resolve) => {
    const host = document.createElement("div");
    document.body.appendChild(host);
    const root = createRoot(host);
    const done = (ok: boolean) => {
      resolve(ok);
      // Let the dialog close animation finish before unmounting.
      setTimeout(() => { root.unmount(); host.remove(); }, 300);
      pending = null;
    };
    root.render(<StepUpDialog open onResolve={done} />);
  });
  return pending;
}

/**
 * Runs `fn`; if the server demands a recent re-authentication, asks the user to verify and retries once.
 * Rejects with the original error if the user cancels.
 */
export async function withStepUp<T>(fn: () => Promise<T>): Promise<T> {
  try {
    return await fn();
  } catch (err) {
    if (!isStepUpRequired(err)) throw err;
    // The global axios interceptor toasts every failure; hide the raw "step_up_required" one.
    const cfg = (err as { config?: { method?: string; url?: string } }).config;
    toast.dismiss(`api:${cfg?.method ?? ""}:${cfg?.url ?? ""}:403`);
    if (!(await requestStepUp())) throw err;
    return await fn();
  }
}
