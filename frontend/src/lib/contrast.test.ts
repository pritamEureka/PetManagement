import { describe, expect, it } from "vitest";
import css from "../index.css?raw";
import { contrastRatio, readTokens } from "./contrast";

const light = readTokens(css, ":root");
const dark = { ...light, ...readTokens(css, ".dark") };

// [foreground token, background token, minimum ratio]
const TEXT_PAIRS: [string, string, number][] = [
  ["foreground", "background", 4.5],
  ["foreground", "card", 4.5],
  ["muted-foreground", "background", 4.5],
  ["muted-foreground", "card", 4.5],
  ["muted-foreground", "muted", 4.5],
  ["primary-foreground", "primary", 4.5],
  ["destructive-foreground", "destructive", 4.5],
  ["destructive-text", "background", 4.5],
  ["destructive-text", "card", 4.5],
  ["destructive-text", "muted", 4.5],
  ["destructive-text", "popover", 4.5],
  ["primary-strong", "background", 4.5],
  ["primary-strong", "card", 4.5],
];

describe("design token contrast (WCAG AA)", () => {
  for (const [name, tokens] of [["light", light], ["dark", dark]] as const) {
    for (const [fg, bg, min] of TEXT_PAIRS) {
      it(`${name}: ${fg} on ${bg} >= ${min}:1`, () => {
        expect(contrastRatio(tokens[fg], tokens[bg])).toBeGreaterThanOrEqual(min);
      });
    }
    it(`${name}: focus ring vs background >= 3:1 (non-text)`, () => {
      expect(contrastRatio(tokens["ring"], tokens["background"])).toBeGreaterThanOrEqual(3);
    });
  }
});
