import { describe, expect, it } from "vitest";
import { formatPhone, parseBdPhone, toBengaliDigits } from "./format";

describe("phone formatting", () => {
  it("parses local, +880 and 880 forms", () => {
    expect(parseBdPhone("01712345678")).toBe("1712345678");
    expect(parseBdPhone("+880 1712-345678")).toBe("1712345678");
    expect(parseBdPhone("8801712345678")).toBe("1712345678");
    expect(parseBdPhone("12345")).toBeNull();
  });
  it("formats local and international", () => {
    expect(formatPhone("01712345678", {}, "en")).toBe("0171-2345678".replace("0171-2345678", "01712-345678"));
    expect(formatPhone("01712345678", { international: true }, "en")).toBe("+880 1712-345678");
  });
  it("uses Bengali digits for bn and leaves junk untouched", () => {
    expect(formatPhone("01712345678", {}, "bn")).toBe(toBengaliDigits("01712-345678"));
    expect(formatPhone("abc", {}, "en")).toBe("abc");
  });
});
