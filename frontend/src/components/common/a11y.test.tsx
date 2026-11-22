import { describe, expect, it } from "vitest";
import { act, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { FormField, LiveRegionProvider, SkipToContent, useAnnounce } from "./a11y";
import { LanguageSwitcher } from "./LanguageSwitcher";
import { useLocaleStore } from "@/lib/i18n";

describe("a11y components", () => {
  it("FormField wires label, aria-invalid and aria-describedby", () => {
    render(
      <FormField id="email" label="Email" error="Required" hint="We never share it" required>
        {(p) => <input {...p} />}
      </FormField>,
    );
    const input = screen.getByLabelText(/Email/);
    expect(input).toHaveAttribute("aria-invalid", "true");
    expect(input).toHaveAttribute("aria-required", "true");
    const ids = input.getAttribute("aria-describedby")!.split(" ");
    expect(ids).toEqual(["email-error", "email-hint"]);
    expect(screen.getByRole("alert")).toHaveTextContent("Required");
  });

  it("FormField has no invalid state without an error", () => {
    render(<FormField id="x" label="X">{(p) => <input {...p} />}</FormField>);
    const input = screen.getByLabelText("X");
    expect(input).not.toHaveAttribute("aria-invalid");
    expect(input).not.toHaveAttribute("aria-describedby");
  });

  it("skip link is the first tab stop and moves focus to main", async () => {
    const user = userEvent.setup();
    render(
      <div>
        <SkipToContent />
        <button>Other</button>
        <main id="main-content">content</main>
      </div>,
    );
    await user.tab();
    const link = screen.getByRole("link", { name: /skip to main content/i });
    expect(link).toHaveFocus();
    await user.keyboard("{Enter}");
    expect(screen.getByRole("main")).toHaveFocus();
  });

  it("live region announces messages", async () => {
    function Btn() {
      const announce = useAnnounce();
      return <button onClick={() => announce("Saved")}>go</button>;
    }
    const user = userEvent.setup();
    render(<LiveRegionProvider><Btn /></LiveRegionProvider>);
    await user.click(screen.getByText("go"));
    await act(async () => { await new Promise((r) => setTimeout(r, 80)); });
    expect(screen.getByTestId("live-polite")).toHaveTextContent("Saved");
  });

  it("language switcher is keyboard operable and updates locale", async () => {
    useLocaleStore.setState({ locale: "en" });
    const user = userEvent.setup();
    render(<LanguageSwitcher />);
    const select = screen.getByLabelText("Language");
    await user.selectOptions(select, "bn");
    expect(useLocaleStore.getState().locale).toBe("bn");
    expect(document.documentElement.lang).toBe("bn");
  });
});
