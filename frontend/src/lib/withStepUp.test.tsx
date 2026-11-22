import { afterEach, describe, it, expect, vi } from "vitest";
import { act } from "@testing-library/react";

// vi.mock factories are hoisted: shared handles must come from vi.hoisted.
const { dismiss, dialog } = vi.hoisted(() => ({
  dismiss: vi.fn(),
  dialog: { resolve: null as null | ((ok: boolean) => void), opened: 0 },
}));

vi.mock("sonner", () => ({ toast: { dismiss: (...a: unknown[]) => dismiss(...a) } }));
vi.mock("@/components/security/StepUpDialog", () => ({
  StepUpDialog: ({ onResolve }: { open: boolean; onResolve: (ok: boolean) => void }) => {
    dialog.resolve = onResolve;
    dialog.opened++;
    return null;
  },
}));

import { isStepUpRequired, withStepUp } from "./withStepUp";

const stepUpError = (extra: object = {}) =>
  Object.assign(new Error("forbidden"), { response: { status: 403, data: { error: { message: "step_up_required" } } }, ...extra });

const tick = () => act(async () => { await new Promise((r) => setTimeout(r, 0)); });

afterEach(() => { dialog.resolve = null; dialog.opened = 0; });

describe("isStepUpRequired", () => {
  it("matches only 403 + step_up_required", () => {
    expect(isStepUpRequired(stepUpError())).toBe(true);
    expect(isStepUpRequired({ response: { status: 403, data: { error: { message: "Forbidden" } } } })).toBe(false);
    expect(isStepUpRequired({ response: { status: 401, data: { error: { message: "step_up_required" } } } })).toBe(false);
    expect(isStepUpRequired(new Error("x"))).toBe(false);
    expect(isStepUpRequired(undefined)).toBe(false);
  });
});

describe("withStepUp", () => {
  it("returns the result without opening a dialog when the call succeeds", async () => {
    const fn = vi.fn().mockResolvedValue("ok");
    await expect(withStepUp(fn)).resolves.toBe("ok");
    expect(fn).toHaveBeenCalledTimes(1);
    expect(dialog.opened).toBe(0);
  });

  it("rethrows other errors untouched without a dialog", async () => {
    const err = new Error("boom");
    await expect(withStepUp(vi.fn().mockRejectedValue(err))).rejects.toBe(err);
    expect(dialog.opened).toBe(0);
  });

  it("opens the dialog on step_up_required and retries once after verification", async () => {
    const fn = vi.fn().mockRejectedValueOnce(stepUpError({ config: { method: "put", url: "/roles/1" } })).mockResolvedValueOnce("done");
    const p = withStepUp(fn);
    await tick();
    expect(dialog.opened).toBeGreaterThan(0);
    expect(fn).toHaveBeenCalledTimes(1);
    expect(dismiss).toHaveBeenCalledWith("api:put:/roles/1:403");

    await act(async () => { dialog.resolve!(true); });
    await expect(p).resolves.toBe("done");
    expect(fn).toHaveBeenCalledTimes(2);
  });

  it("rejects with the original error and does not retry when the user cancels", async () => {
    const err = stepUpError();
    const fn = vi.fn().mockRejectedValue(err);
    const p = withStepUp(fn);
    const caught = p.catch((e) => e);
    await tick();
    await act(async () => { dialog.resolve!(false); });
    expect(await caught).toBe(err);
    expect(fn).toHaveBeenCalledTimes(1);
  });

  it("does not loop: a second step_up_required after verification is thrown", async () => {
    const fn = vi.fn().mockRejectedValue(stepUpError());
    const p = withStepUp(fn);
    const caught = p.catch((e) => e);
    await tick();
    await act(async () => { dialog.resolve!(true); });
    expect(isStepUpRequired(await caught)).toBe(true);
    expect(fn).toHaveBeenCalledTimes(2);
  });
});
