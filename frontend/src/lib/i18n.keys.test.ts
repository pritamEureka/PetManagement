import { describe, expect, it } from "vitest";
import { bn, en } from "./i18n";
import { API_ERROR_CODES } from "./apiErrors";

const sources = import.meta.glob("/src/**/*.{ts,tsx}", { query: "?raw", import: "default", eager: true }) as Record<string, string>;

describe("i18n key coverage", () => {
  it("every t('literal.key') used in src/ exists in the English catalog", () => {
    const used = new Map<string, string>();
    const re = /(?<![\w.])t\(\s*["'`]([a-zA-Z][\w]*(?:\.[\w]+)+)["'`]/g;
    for (const [file, text] of Object.entries(sources)) {
      if (file.includes(".test.") || file.endsWith("i18n.ts") || file.endsWith("i18n.ux.ts")) continue;
      for (const m of text.matchAll(re)) used.set(m[1], file);
    }
    const missing = [...used].filter(([k]) => !(k in en)).map(([k, f]) => `${k} (${f})`);
    expect(missing).toEqual([]);
  });

  it("has an err.<code> message in en and bn for every API error code", () => {
    for (const code of API_ERROR_CODES) {
      expect(en[`err.${code}`], `en err.${code}`).toBeTruthy();
      expect(bn[`err.${code}`], `bn err.${code}`).toBeTruthy();
    }
  });
});
