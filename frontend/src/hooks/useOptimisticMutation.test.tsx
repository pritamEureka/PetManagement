import { describe, expect, it, vi } from "vitest";
import { act, renderHook } from "@testing-library/react";
import { useOptimisticMutation } from "./useOptimisticMutation";

describe("useOptimisticMutation", () => {
  it("applies optimistic change and keeps it on success", async () => {
    const onMutate = vi.fn(() => "snap");
    const onRollback = vi.fn();
    const { result } = renderHook(() =>
      useOptimisticMutation<number, string, string>({ mutationFn: async (n) => `ok${n}`, onMutate, onRollback }),
    );
    let r: string | undefined;
    await act(async () => {
      r = await result.current.mutate(1);
    });
    expect(r).toBe("ok1");
    expect(onMutate).toHaveBeenCalledWith(1);
    expect(onRollback).not.toHaveBeenCalled();
  });

  it("rolls back with the snapshot on failure", async () => {
    const onRollback = vi.fn();
    const onError = vi.fn();
    const { result } = renderHook(() =>
      useOptimisticMutation<number, string, string>({
        mutationFn: async () => {
          throw new Error("boom");
        },
        onMutate: () => "snap",
        onRollback,
        onError,
      }),
    );
    await act(async () => {
      await result.current.mutate(1);
    });
    expect(onRollback).toHaveBeenCalledWith("snap", 1, expect.any(Error));
    expect(onError).toHaveBeenCalled();
  });

  it("drops a duplicate call with the same key while in flight", async () => {
    let release!: () => void;
    const gate = new Promise<void>((res) => (release = res));
    const mutationFn = vi.fn(async () => {
      await gate;
      return "done";
    });
    const { result } = renderHook(() =>
      useOptimisticMutation<string, string, null>({ mutationFn, onMutate: () => null, onRollback: () => {}, getKey: (v) => v }),
    );
    let p1!: Promise<string | undefined>;
    let p2!: Promise<string | undefined>;
    await act(async () => {
      p1 = result.current.mutate("a");
      p2 = result.current.mutate("a");
      release();
      await Promise.all([p1, p2]);
    });
    expect(mutationFn).toHaveBeenCalledTimes(1);
    expect(await p2).toBeUndefined();
  });
});
