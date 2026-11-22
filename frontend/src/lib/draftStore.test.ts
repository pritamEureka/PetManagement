import { beforeEach, describe, expect, it } from "vitest";
import { clearDraft, DRAFT_TTL_MS, readDraft, writeDraft } from "./draftStore";

describe("draftStore", () => {
  beforeEach(() => localStorage.clear());

  it("round-trips a draft scoped by user", () => {
    writeDraft("form", { a: 1 }, { userId: "u1", now: 1000 });
    expect(readDraft("form", { userId: "u1", now: 2000 })?.data).toEqual({ a: 1 });
    expect(readDraft("form", { userId: "u2", now: 2000 })).toBeNull();
  });

  it("expires after the TTL and removes the entry", () => {
    writeDraft("form", { a: 1 }, { userId: "u1", now: 0 });
    expect(readDraft("form", { userId: "u1", now: DRAFT_TTL_MS + 1 })).toBeNull();
    expect(localStorage.length).toBe(0);
  });

  it("drops drafts from another schema version", () => {
    writeDraft("form", { a: 1 }, { userId: "u1", version: 1 });
    expect(readDraft("form", { userId: "u1", version: 2 })).toBeNull();
  });

  it("clear removes the draft", () => {
    writeDraft("form", { a: 1 }, { userId: "u1" });
    clearDraft("form", "u1");
    expect(readDraft("form", { userId: "u1" })).toBeNull();
  });

  it("does not throw when storage is full", () => {
    const orig = Storage.prototype.setItem;
    Storage.prototype.setItem = () => {
      throw new DOMException("full", "QuotaExceededError");
    };
    try {
      expect(writeDraft("form", { a: 1 }, { userId: "u1" })).toBe(false);
    } finally {
      Storage.prototype.setItem = orig;
    }
  });

  it("survives corrupt JSON", () => {
    localStorage.setItem("pawzaroo:draft:u1:form", "{not json");
    expect(readDraft("form", { userId: "u1" })).toBeNull();
  });
});
