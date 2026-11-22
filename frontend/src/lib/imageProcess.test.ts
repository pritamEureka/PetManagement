import { describe, expect, it } from "vitest";
import { clampOffset, computeCropRect, fitWithin } from "./imageProcess";

describe("imageProcess math", () => {
  it("fits within the max edge without upscaling", () => {
    expect(fitWithin(3200, 1600)).toEqual({ width: 1600, height: 800 });
    expect(fitWithin(800, 600)).toEqual({ width: 800, height: 600 });
  });

  it("crops the centered square from a landscape image at zoom 1", () => {
    const r = computeCropRect({ width: 400, height: 200 }, { width: 100, height: 100 }, 1, { x: 0, y: 0 });
    expect(r.sw).toBeCloseTo(200);
    expect(r.sh).toBeCloseTo(200);
    expect(r.sx).toBeCloseTo(100);
    expect(r.sy).toBeCloseTo(0);
  });

  it("zooming in shrinks the source rectangle", () => {
    const r = computeCropRect({ width: 400, height: 200 }, { width: 100, height: 100 }, 2, { x: 0, y: 0 });
    expect(r.sw).toBeCloseTo(100);
  });

  it("clamps panning so the image keeps covering the box", () => {
    const o = clampOffset({ width: 400, height: 200 }, { width: 100, height: 100 }, 1, { x: 999, y: 999 });
    expect(o.x).toBeCloseTo(50);
    expect(o.y).toBeCloseTo(0);
  });
});
