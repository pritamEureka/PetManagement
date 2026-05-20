import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it } from "vitest";
import { Can } from "./Can";
import { useAuthStore } from "@/store/authStore";

describe("Can", () => {
  beforeEach(() => {
    useAuthStore.setState({
      user: { id: "1", email: "a@b.com", displayName: "A", roles: ["User"], permissions: ["pets.read"] },
    });
  });

  it("renders children when the user has the required permission", () => {
    render(<Can permission="pets.read">Allowed</Can>);

    expect(screen.getByText("Allowed")).toBeInTheDocument();
  });

  it("renders fallback when permission checks fail", () => {
    render(<Can permission="admin.users.manage" fallback={<span>Hidden</span>}>Allowed</Can>);

    expect(screen.getByText("Hidden")).toBeInTheDocument();
    expect(screen.queryByText("Allowed")).not.toBeInTheDocument();
  });

  it("supports anyOf and role checks", () => {
    render(
      <>
        <Can anyOf={["posts.moderate", "pets.read"]}>Any Permission</Can>
        <Can role="Admin" fallback={<span>No Admin</span>}>Admin Only</Can>
      </>
    );

    expect(screen.getByText("Any Permission")).toBeInTheDocument();
    expect(screen.getByText("No Admin")).toBeInTheDocument();
  });
});
