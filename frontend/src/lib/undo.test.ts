import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("sonner", () => {
  const toast: any = vi.fn(() => "tid");
  toast.dismiss = vi.fn();
  toast.error = vi.fn();
  return { toast };
});

import { flushUndoable, pendingUndoableCount, scheduleUndoable } from "./undo";

describe("scheduleUndoable", () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it("commits after the delay", async () => {
    const commit = vi.fn();
    scheduleUndoable({ message: "x", commit, delayMs: 1000 });
    expect(commit).not.toHaveBeenCalled();
    await vi.advanceTimersByTimeAsync(1000);
    expect(commit).toHaveBeenCalledTimes(1);
    expect(pendingUndoableCount()).toBe(0);
  });

  it("does not commit when undone and rolls back", async () => {
    const commit = vi.fn();
    const rollback = vi.fn();
    const h = scheduleUndoable({ message: "x", commit, rollback, delayMs: 1000 });
    h.undo();
    await vi.advanceTimersByTimeAsync(2000);
    expect(commit).not.toHaveBeenCalled();
    expect(rollback).toHaveBeenCalledTimes(1);
  });

  it("flush commits pending actions immediately", async () => {
    const commit = vi.fn();
    scheduleUndoable({ message: "x", commit, delayMs: 8000 });
    await flushUndoable();
    expect(commit).toHaveBeenCalledTimes(1);
  });

  it("rolls back when the commit fails", async () => {
    const rollback = vi.fn();
    scheduleUndoable({ message: "x", commit: () => Promise.reject(new Error("no")), rollback, delayMs: 10 });
    await vi.advanceTimersByTimeAsync(10);
    expect(rollback).toHaveBeenCalled();
  });
});
