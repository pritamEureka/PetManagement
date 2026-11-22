import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { Home, PawPrint, Store } from "lucide-react";
import { MobileTabBar } from "./MobileTabBar";
import { nextRovingIndex } from "@/hooks/useRovingTabindex";

const tabs = [
  { to: "/", label: "Home", icon: Home, end: true },
  { to: "/pets", label: "Pets", icon: PawPrint },
  { to: "/store", label: "Store", icon: Store },
];

function setup(path = "/pets") {
  render(
    <MemoryRouter initialEntries={[path]}>
      <MobileTabBar tabs={tabs} onMore={vi.fn()} ariaLabel="Primary" />
    </MemoryRouter>,
  );
  return {
    home: screen.getByRole("link", { name: "Home" }),
    pets: screen.getByRole("link", { name: "Pets" }),
    store: screen.getByRole("link", { name: "Store" }),
    more: screen.getByRole("button"),
  };
}

describe("MobileTabBar roving tabindex", () => {
  it("makes only the active route tabbable", () => {
    const { home, pets, store, more } = setup("/pets");
    expect(pets).toHaveAttribute("tabindex", "0");
    for (const el of [home, store, more]) expect(el).toHaveAttribute("tabindex", "-1");
  });

  it("moves focus with arrow keys, wrapping at the ends", async () => {
    const user = userEvent.setup();
    const { home, pets, store, more } = setup("/pets");
    pets.focus();
    await user.keyboard("{ArrowRight}");
    expect(store).toHaveFocus();
    await user.keyboard("{ArrowRight}");
    expect(more).toHaveFocus();
    await user.keyboard("{ArrowRight}");
    expect(home).toHaveFocus();
    await user.keyboard("{ArrowLeft}");
    expect(more).toHaveFocus();
    expect(more).toHaveAttribute("tabindex", "0");
    expect(home).toHaveAttribute("tabindex", "-1");
  });

  it("supports Home and End", async () => {
    const user = userEvent.setup();
    const { home, pets, more } = setup("/pets");
    pets.focus();
    await user.keyboard("{End}");
    expect(more).toHaveFocus();
    await user.keyboard("{Home}");
    expect(home).toHaveFocus();
  });
});

describe("nextRovingIndex", () => {
  it("reverses horizontal arrows in RTL", () => {
    expect(nextRovingIndex("ArrowLeft", 0, 3, "horizontal", true, true)).toBe(1);
    expect(nextRovingIndex("ArrowRight", 0, 3, "horizontal", true, true)).toBe(2);
  });
  it("clamps when loop is off and ignores other keys", () => {
    expect(nextRovingIndex("ArrowDown", 2, 3, "vertical", false, false)).toBe(2);
    expect(nextRovingIndex("a", 0, 3, "vertical", false, false)).toBeNull();
  });
});
