import { beforeEach, describe, expect, it } from "vitest";
import { bn, en, getDirection, translate, useLocaleStore } from "./i18n";
import { formatCurrency, formatDate, formatNumber, formatRelativeTime } from "./format";

describe("i18n", () => {
  beforeEach(() => {
    useLocaleStore.setState({ locale: "en" });
  });

  it("translates and interpolates", () => {
    expect(translate("en", "auth.welcome", { name: "Ayesha" })).toBe("Welcome back, Ayesha!");
    expect(translate("bn", "auth.login")).toBe(bn["auth.login"]);
  });

  it("falls back to the key when missing and leaves unknown placeholders", () => {
    expect(translate("bn", "no.such.key")).toBe("no.such.key");
    expect(translate("en", "common.required")).toBe("{field} is required");
  });

  it("keeps bn and en catalogs in sync", () => {
    expect(Object.keys(bn).sort()).toEqual(Object.keys(en).sort());
  });

  it("has every en key in bn and vice versa, with no duplicates", () => {
    const enKeys = Object.keys(en);
    const bnKeys = Object.keys(bn);
    expect(enKeys.filter((k) => !(k in bn))).toEqual([]);
    expect(bnKeys.filter((k) => !(k in en))).toEqual([]);
    expect(new Set(enKeys).size).toBe(enKeys.length);
  });

  it("uses identical {placeholders} for every key in en and bn", () => {
    const vars = (s: string) => [...s.matchAll(/\{(\w+)\}/g)].map((m) => m[1]).sort();
    const mismatched = Object.keys(en)
      .filter((k) => k in bn && JSON.stringify(vars(en[k])) !== JSON.stringify(vars(bn[k])))
      .map((k) => `${k}: en=${vars(en[k])} bn=${vars(bn[k])}`);
    expect(mismatched).toEqual([]);
  });

  it("has no empty translations", () => {
    for (const [k, v] of [...Object.entries(en), ...Object.entries(bn)]) {
      expect(v.trim(), k).not.toBe("");
    }
  });

  it("reports direction and updates document lang/dir on locale change", () => {
    expect(getDirection("en")).toBe("ltr");
    expect(getDirection("ar-EG")).toBe("rtl");
    useLocaleStore.getState().setLocale("bn");
    expect(document.documentElement.lang).toBe("bn");
    expect(document.documentElement.dir).toBe("ltr");
    expect(useLocaleStore.getState().locale).toBe("bn");
  });

  it("persists locale in localStorage", () => {
    useLocaleStore.getState().setLocale("bn");
    expect(localStorage.getItem("pawzaroo-locale")).toContain("bn");
  });
});

describe("format", () => {
  it("formats currency in BDT by default and honors locale", () => {
    expect(formatCurrency(1500, "BDT", "en")).toMatch(/1,500/);
    expect(formatCurrency(1500, "BDT", "bn")).toMatch(/[০-৯]/);
  });

  it("formats numbers per locale", () => {
    expect(formatNumber(12345.5, undefined, "en")).toBe("12,345.5");
    expect(formatNumber(12, undefined, "bn")).toBe("১২");
  });

  it("formats dates and tolerates invalid input", () => {
    expect(formatDate("2025-03-04T12:00:00Z", { year: "numeric" }, "en")).toBe("2025");
    expect(formatDate("not a date")).toBe("");
  });

  it("formats relative time", () => {
    const now = new Date("2025-01-10T00:00:00Z");
    expect(formatRelativeTime("2025-01-09T00:00:00Z", now, "en")).toBe("yesterday");
  });
});

describe("translateError", () => {
  it("passes unknown messages through and translates known ones per locale", async () => {
    const { translateError, useLocaleStore } = await import("./i18n");
    useLocaleStore.getState().setLocale("en");
    expect(translateError("Enter a valid email.")).toBe("Enter a valid email.");
    expect(translateError("Server said no")).toBe("Server said no");
    expect(translateError(undefined)).toBeUndefined();
    useLocaleStore.getState().setLocale("bn");
    expect(translateError("Enter a valid email.")).not.toBe("Enter a valid email.");
    expect(translateError("Server said no")).toBe("Server said no");
    useLocaleStore.getState().setLocale("en");
  });
});
