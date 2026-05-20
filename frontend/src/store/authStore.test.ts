import { describe, expect, it, beforeEach, vi } from "vitest";
import { useAuthStore } from "./authStore";
import { authApi } from "@/api/auth";

vi.mock("@/api/auth", () => ({
  authApi: {
    login: vi.fn(),
    register: vi.fn(),
    refresh: vi.fn(),
    logout: vi.fn(),
  },
}));

const user = {
  id: "user-1",
  email: "user@example.com",
  displayName: "User One",
  roles: ["User"],
  permissions: ["pets.read"],
};

function resetAuthStore() {
  useAuthStore.setState({
    accessToken: null,
    refreshToken: null,
    expiresAt: null,
    user: null,
    needsTwoFactor: false,
    suspensionMessage: null,
  });
}

describe("authStore", () => {
  beforeEach(resetAuthStore);

  it("stores tokens and user details after login", async () => {
    vi.mocked(authApi.login).mockResolvedValue({
      accessToken: "access",
      refreshToken: "refresh",
      expiresAt: "2026-05-20T12:00:00Z",
      user,
    });

    await useAuthStore.getState().login("user@example.com", "Password1");

    expect(authApi.login).toHaveBeenCalledWith("user@example.com", "Password1", undefined);
    expect(useAuthStore.getState().accessToken).toBe("access");
    expect(useAuthStore.getState().user?.email).toBe("user@example.com");
    expect(useAuthStore.getState().needsTwoFactor).toBe(false);
  });

  it("sets the two factor flag when the API requires a second factor", async () => {
    vi.mocked(authApi.login).mockRejectedValue({
      response: { data: { error: { message: "two_factor_required" } } },
    });

    await expect(useAuthStore.getState().login("admin@example.com", "Password1"))
      .rejects.toBeTruthy();

    expect(useAuthStore.getState().needsTwoFactor).toBe(true);
  });

  it("refreshes tokens and clears auth state when refresh fails", async () => {
    useAuthStore.setState({ refreshToken: "old-refresh", accessToken: "old-access", user });
    vi.mocked(authApi.refresh).mockResolvedValueOnce({
      accessToken: "new-access",
      refreshToken: "new-refresh",
      expiresAt: "2026-05-20T13:00:00Z",
      user,
    });

    await expect(useAuthStore.getState().refresh()).resolves.toBe("new-access");
    expect(useAuthStore.getState().refreshToken).toBe("new-refresh");

    vi.mocked(authApi.refresh).mockRejectedValueOnce(new Error("expired"));
    await expect(useAuthStore.getState().refresh()).resolves.toBeNull();
    expect(useAuthStore.getState().accessToken).toBeNull();
    expect(useAuthStore.getState().user).toBeNull();
  });

  it("short-circuits permissions for SuperAdmin users", () => {
    useAuthStore.setState({
      user: { ...user, roles: ["SuperAdmin"], permissions: [] },
    });

    expect(useAuthStore.getState().hasPermission("anything.manage")).toBe(true);
    expect(useAuthStore.getState().hasAnyRole("Admin", "SuperAdmin")).toBe(true);
  });
});
