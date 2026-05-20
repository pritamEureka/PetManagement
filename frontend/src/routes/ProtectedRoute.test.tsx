import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it } from "vitest";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { ProtectedRoute } from "./ProtectedRoute";
import { useAuthStore } from "@/store/authStore";

function renderProtected(options?: { permission?: string; roles?: string[]; anyOf?: string[] }) {
  return render(
    <MemoryRouter initialEntries={["/secure"]}>
      <Routes>
        <Route path="/login" element={<div>Login Page</div>} />
        <Route path="/forbidden" element={<div>Forbidden Page</div>} />
        <Route element={<ProtectedRoute {...options} />}>
          <Route path="/secure" element={<div>Secure Page</div>} />
        </Route>
      </Routes>
    </MemoryRouter>
  );
}

describe("ProtectedRoute", () => {
  beforeEach(() => {
    useAuthStore.setState({
      accessToken: null,
      refreshToken: null,
      expiresAt: null,
      user: null,
      needsTwoFactor: false,
      suspensionMessage: null,
    });
  });

  it("redirects anonymous users to login", () => {
    renderProtected();

    expect(screen.getByText("Login Page")).toBeInTheDocument();
  });

  it("renders protected content for authenticated users", () => {
    useAuthStore.setState({
      accessToken: "token",
      user: { id: "1", email: "a@b.com", displayName: "A", roles: ["User"], permissions: [] },
    });

    renderProtected();

    expect(screen.getByText("Secure Page")).toBeInTheDocument();
  });

  it("redirects authenticated users without required permissions", () => {
    useAuthStore.setState({
      accessToken: "token",
      user: { id: "1", email: "a@b.com", displayName: "A", roles: ["User"], permissions: ["pets.read"] },
    });

    renderProtected({ permission: "admin.users.manage" });

    expect(screen.getByText("Forbidden Page")).toBeInTheDocument();
  });

  it("allows SuperAdmin through permission-protected routes", () => {
    useAuthStore.setState({
      accessToken: "token",
      user: { id: "1", email: "a@b.com", displayName: "A", roles: ["SuperAdmin"], permissions: [] },
    });

    renderProtected({ permission: "admin.users.manage" });

    expect(screen.getByText("Secure Page")).toBeInTheDocument();
  });
});
