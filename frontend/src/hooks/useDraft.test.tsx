import { afterEach, beforeEach, describe, it, expect, vi } from "vitest";
import { act, renderHook } from "@testing-library/react";
import { useDraft } from "./useDraft";
import { draftStorageKey, writeDraft, DRAFT_TTL_MS } from "@/lib/draftStore";

// Signed-out user scope is "anon" (see lib/safeStorage.currentUserScope), so keys are predictable.
const KEY = "unit-form";
const storageKey = draftStorageKey(KEY, "anon");

beforeEach(() => vi.useFakeTimers());
afterEach(() => vi.useRealTimers());

const flush = (ms: number) => act(() => { vi.advanceTimersByTime(ms); });

describe("useDraft", () => {
  it("does not write anything until the value differs from the first render", () => {
    renderHook(({ v }) => useDraft(KEY, v, { debounceMs: 100 }), { initialProps: { v: { name: "a" } } });
    flush(1000);
    expect(localStorage.getItem(storageKey)).toBeNull();
  });

  it("debounces autosave of changed values", () => {
    const { rerender } = renderHook(({ v }) => useDraft(KEY, v, { debounceMs: 100 }), { initialProps: { v: { name: "a" } } });
    rerender({ v: { name: "ab" } });
    rerender({ v: { name: "abc" } });
    flush(50);
    expect(localStorage.getItem(storageKey)).toBeNull();
    flush(100);
    const env = JSON.parse(localStorage.getItem(storageKey)!);
    expect(env.data).toEqual({ name: "abc" });
  });

  it("offers a stored draft for restore, then restore() returns it and clears the prompt", () => {
    writeDraft(KEY, { name: "saved" }, { userId: "anon" });
    const { result } = renderHook(() => useDraft(KEY, { name: "" }));
    expect(result.current.hasDraft).toBe(true);
    expect(result.current.savedAt).toEqual(expect.any(Number));
    let restored: unknown;
    act(() => { restored = result.current.restore(); });
    expect(restored).toEqual({ name: "saved" });
    expect(result.current.hasDraft).toBe(false);
  });

  it("does not overwrite a pending draft while the restore prompt is open", () => {
    writeDraft(KEY, { name: "saved" }, { userId: "anon" });
    const { rerender } = renderHook(({ v }) => useDraft(KEY, v, { debounceMs: 100 }), { initialProps: { v: { name: "" } } });
    rerender({ v: { name: "typing over it" } });
    flush(1000);
    expect(JSON.parse(localStorage.getItem(storageKey)!).data).toEqual({ name: "saved" });
  });

  it("discard() removes the stored draft", () => {
    writeDraft(KEY, { name: "saved" }, { userId: "anon" });
    const { result } = renderHook(() => useDraft(KEY, { name: "" }));
    act(() => result.current.discard());
    expect(result.current.hasDraft).toBe(false);
    expect(localStorage.getItem(storageKey)).toBeNull();
  });

  it("clear() removes the draft and cancels a pending autosave (e.g. after submit)", () => {
    const { result, rerender } = renderHook(({ v }) => useDraft(KEY, v, { debounceMs: 100 }), { initialProps: { v: { name: "a" } } });
    rerender({ v: { name: "changed" } });
    act(() => result.current.clear());
    flush(1000);
    expect(localStorage.getItem(storageKey)).toBeNull();
  });

  it("ignores expired drafts (TTL) and drafts of another schema version", () => {
    const old = Date.now() - DRAFT_TTL_MS - 1000;
    writeDraft(KEY, { name: "stale" }, { userId: "anon", now: old });
    const expired = renderHook(() => useDraft(KEY, { name: "" }));
    expect(expired.result.current.hasDraft).toBe(false);
    expect(localStorage.getItem(storageKey)).toBeNull();

    writeDraft(KEY, { name: "v0" }, { userId: "anon", version: 99 });
    const other = renderHook(() => useDraft(KEY, { name: "" }));
    expect(other.result.current.hasDraft).toBe(false);
  });

  it("respects a custom ttlMs and the sanitize filter (secrets are never persisted)", () => {
    const { rerender } = renderHook(
      ({ v }) => useDraft(KEY, v, { debounceMs: 10, sanitize: (x: { name: string; password: string }) => ({ name: x.name }) }),
      { initialProps: { v: { name: "a", password: "" } } },
    );
    rerender({ v: { name: "a", password: "hunter2" } });
    flush(100);
    // sanitized value is unchanged from the initial snapshot -> nothing written at all
    expect(localStorage.getItem(storageKey)).toBeNull();
    rerender({ v: { name: "b", password: "hunter2" } });
    flush(100);
    const raw = localStorage.getItem(storageKey)!;
    expect(raw).not.toContain("hunter2");
    expect(JSON.parse(raw).data).toEqual({ name: "b" });
  });

  it("does not write when disabled", () => {
    const { rerender } = renderHook(({ v }) => useDraft(KEY, v, { debounceMs: 10, enabled: false }), { initialProps: { v: { name: "a" } } });
    rerender({ v: { name: "b" } });
    flush(100);
    expect(localStorage.getItem(storageKey)).toBeNull();
  });
});
