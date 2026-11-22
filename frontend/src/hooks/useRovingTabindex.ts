import { useCallback, useState } from "react";
import type { KeyboardEvent } from "react";

interface Options {
  /** Number of focusable items. */
  count: number;
  /** "horizontal" uses Left/Right (RTL-aware), "vertical" uses Up/Down. */
  orientation?: "horizontal" | "vertical";
  /** Wrap around at the ends (default true). */
  loop?: boolean;
  /** Index that is tabbable initially (e.g. the active tab). */
  initialIndex?: number;
  /** Override direction detection (tests). Defaults to document.dir. */
  rtl?: boolean;
}

/** Pure next-index calculation (exported for tests). Returns null when the key is not handled. */
export function nextRovingIndex(key: string, current: number, count: number, orientation: "horizontal" | "vertical", loop: boolean, rtl: boolean): number | null {
  if (count <= 0) return null;
  const prevKey = orientation === "horizontal" ? (rtl ? "ArrowRight" : "ArrowLeft") : "ArrowUp";
  const nextKey = orientation === "horizontal" ? (rtl ? "ArrowLeft" : "ArrowRight") : "ArrowDown";
  if (key === "Home") return 0;
  if (key === "End") return count - 1;
  if (key === nextKey) return current + 1 >= count ? (loop ? 0 : count - 1) : current + 1;
  if (key === prevKey) return current - 1 < 0 ? (loop ? count - 1 : 0) : current - 1;
  return null;
}

/**
 * Roving tabindex for custom (non-Radix) composite widgets: one tab stop, arrow keys + Home/End move
 * focus between items. Spread `getItemProps(i)` on each item and `containerProps` on the wrapper.
 * Items are located by `[data-roving-item]`, so they may be nested links/buttons.
 */
export function useRovingTabindex({ count, orientation = "horizontal", loop = true, initialIndex = 0, rtl }: Options) {
  const [active, setActive] = useState(Math.min(Math.max(initialIndex, 0), Math.max(count - 1, 0)));

  const onKeyDown = useCallback(
    (e: KeyboardEvent<HTMLElement>) => {
      const root = e.currentTarget;
      const items = Array.from(root.querySelectorAll<HTMLElement>("[data-roving-item]"));
      const current = items.findIndex((el) => el === document.activeElement || el.contains(document.activeElement));
      if (current < 0) return;
      const isRtl = rtl ?? (typeof document !== "undefined" && document.documentElement.dir === "rtl");
      const next = nextRovingIndex(e.key, current, items.length, orientation, loop, isRtl);
      if (next === null) return;
      e.preventDefault();
      setActive(next);
      items[next]?.focus();
    },
    [orientation, loop, rtl],
  );

  const getItemProps = (i: number) => ({
    "data-roving-item": "",
    tabIndex: i === Math.min(active, Math.max(count - 1, 0)) ? 0 : -1,
    onFocus: () => setActive(i),
  });

  return { active, setActive, containerProps: { onKeyDown }, getItemProps };
}
