import { describe, expect, it } from "vitest";
import { formatDistance, haversineKm } from "./useGeolocation";

describe("formatDistance", () => {
  it("formats metres, short and long distances", () => {
    expect(formatDistance(null)).toBeNull();
    expect(formatDistance(0.04)).toBe("100 m away");
    expect(formatDistance(0.85)).toBe("900 m away");
    expect(formatDistance(3.24)).toBe("3.2 km away");
    expect(formatDistance(123.6)).toBe("124 km away");
  });
});

describe("haversineKm", () => {
  it("matches known city distances", () => {
    // Dhaka → Chattogram ≈ 215 km great-circle.
    expect(haversineKm(23.8103, 90.4125, 22.3569, 91.7832)).toBeGreaterThan(200);
    expect(haversineKm(23.8103, 90.4125, 22.3569, 91.7832)).toBeLessThan(230);
    expect(haversineKm(23.8, 90.4, 23.8, 90.4)).toBe(0);
  });
});
