import { afterEach, describe, it, expect } from "vitest";
import { act, render, screen } from "@testing-library/react";
import { ErrorText, FormField } from "./a11y";
import { useLocaleStore, translateError } from "@/lib/i18n";

afterEach(() => act(() => useLocaleStore.getState().setLocale("en")));

describe("ErrorText / FormField translate known validation messages", () => {
  it("passes unknown (server) messages through unchanged in every locale", () => {
    render(<ErrorText id="e">Server said no</ErrorText>);
    expect(screen.getByRole("alert")).toHaveTextContent("Server said no");
    act(() => useLocaleStore.getState().setLocale("bn"));
    expect(screen.getByRole("alert")).toHaveTextContent("Server said no");
  });

  it("re-renders a known message when the locale changes", () => {
    render(<ErrorText id="e">Enter a valid email.</ErrorText>);
    expect(screen.getByRole("alert")).toHaveTextContent("Enter a valid email.");
    act(() => useLocaleStore.getState().setLocale("bn"));
    const bn = translateError("Enter a valid email.");
    expect(bn).not.toBe("Enter a valid email.");
    expect(screen.getByRole("alert")).toHaveTextContent(bn!);
  });

  it("FormField routes its error through the same translation and renders nothing without an error", () => {
    const { rerender } = render(
      <FormField id="mail" label="Email" error="Enter a valid email.">{(p) => <input {...p} />}</FormField>,
    );
    expect(screen.getByRole("alert")).toBeInTheDocument();
    rerender(<FormField id="mail" label="Email">{(p) => <input {...p} />}</FormField>);
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("renders non-string children as-is", () => {
    render(<ErrorText id="e"><b>bold</b></ErrorText>);
    expect(screen.getByText("bold")).toBeInTheDocument();
  });
});
