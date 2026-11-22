import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";

// vi.mock factories are hoisted above imports/consts, so shared mocks must come from vi.hoisted.
const { login, toastError, toastInfo } = vi.hoisted(() => ({ login: vi.fn(), toastError: vi.fn(), toastInfo: vi.fn() }));

vi.mock("@/store/authStore", () => {
  const state = { login, needsTwoFactor: false };
  const useAuthStore: any = (selector: (s: typeof state) => unknown) => selector(state);
  useAuthStore.setState = vi.fn();
  return { useAuthStore };
});
vi.mock("@/components/ui/sonner", () => ({
  toast: { error: (...a: unknown[]) => toastError(...a), info: (...a: unknown[]) => toastInfo(...a), success: vi.fn() },
}));
vi.mock("@/api/securityPlus", () => ({ passkeyApi: { loginOptions: vi.fn(), loginVerify: vi.fn() } }));
vi.mock("@/lib/webauthn", () => ({ passkeysSupported: () => false, getPasskey: vi.fn() }));
vi.mock("@/components/theme/ThemeToggle", () => ({ ThemeToggle: () => null }));
vi.mock("@/components/auth/AuthPawTrail", () => ({ AuthPawTrail: () => null }));

import { LoginPage } from "./LoginPage";

function renderPage() {
  return render(
    <MemoryRouter initialEntries={["/login"]}>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/home" element={<div>home page</div>} />
      </Routes>
    </MemoryRouter>,
  );
}

describe("LoginPage", () => {
  beforeEach(() => {
    login.mockReset();
  });

  it("shows validation errors and does not call the API for an invalid form", async () => {
    renderPage();
    await userEvent.type(screen.getByLabelText("Email"), "a@b")   // passes native type=email validation, fails zod;
    await userEvent.click(screen.getByRole("button", { name: /sign in$/i }));

    expect(await screen.findByText("Enter a valid email.")).toBeInTheDocument();
    expect(login).not.toHaveBeenCalled();
  });

  it("logs in with the entered credentials and navigates home on success", async () => {
    login.mockResolvedValue(undefined);
    renderPage();
    await userEvent.type(screen.getByLabelText("Email"), "user@example.com");
    await userEvent.type(screen.getByLabelText("Password"), "Sup3rSecret!");
    await userEvent.click(screen.getByRole("button", { name: /sign in$/i }));

    await waitFor(() => expect(login).toHaveBeenCalledWith("user@example.com", "Sup3rSecret!", undefined));
    expect(await screen.findByText("home page")).toBeInTheDocument();
  });

  it("shows an error toast and stays on the page when the server rejects the login", async () => {
    login.mockRejectedValue({ response: { data: { error: { code: "invalid_credentials", message: "Invalid email or password" } } } });
    renderPage();
    await userEvent.type(screen.getByLabelText("Email"), "user@example.com");
    await userEvent.type(screen.getByLabelText("Password"), "wrong");
    await userEvent.click(screen.getByRole("button", { name: /sign in$/i }));

    await waitFor(() => expect(toastError).toHaveBeenCalledWith("Invalid email or password"));
    expect(screen.queryByText("home page")).not.toBeInTheDocument();
  });

  it("renders an inline banner (not a toast) for a pending registration", async () => {
    login.mockRejectedValue({ response: { data: { error: { code: "registration_pending", message: "Awaiting admin approval" } } } });
    renderPage();
    await userEvent.type(screen.getByLabelText("Email"), "vet@example.com");
    await userEvent.type(screen.getByLabelText("Password"), "Sup3rSecret!");
    await userEvent.click(screen.getByRole("button", { name: /sign in$/i }));

    expect(await screen.findByText(/Awaiting admin approval/)).toBeInTheDocument();
    expect(toastError).not.toHaveBeenCalled();
  });
});
