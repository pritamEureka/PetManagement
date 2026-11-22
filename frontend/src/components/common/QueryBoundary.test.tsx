import { afterEach, describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryBoundary, classifyQueryError } from "./QueryBoundary";

type Q = Parameters<typeof QueryBoundary<string[]>>[0]["query"];

function q(over: Partial<Q>): Q {
  return {
    data: undefined, isLoading: false, isError: false, error: null, isFetching: false, isPlaceholderData: false,
    refetch: vi.fn() as unknown as Q["refetch"], ...over,
  } as Q;
}

afterEach(() => vi.restoreAllMocks());

describe("classifyQueryError", () => {
  it("classifies offline, unavailable (502/503/504), network errors and generic errors", () => {
    expect(classifyQueryError(new Error("x"), false)).toBe("offline");
    for (const status of [502, 503, 504]) expect(classifyQueryError({ response: { status } }, true)).toBe("unavailable");
    expect(classifyQueryError({ code: "ERR_NETWORK" }, true)).toBe("offline");
    expect(classifyQueryError({ response: { status: 500 } }, true)).toBe("error");
    expect(classifyQueryError({ response: { status: 404 } }, true)).toBe("error");
  });
});

describe("QueryBoundary", () => {
  const list = (d: string[]) => <ul>{d.map((x) => <li key={x}>{x}</li>)}</ul>;

  it("shows a busy skeleton while loading", () => {
    render(<QueryBoundary query={q({ isLoading: true })}>{list}</QueryBoundary>);
    expect(screen.getByRole("status")).toHaveAttribute("aria-busy", "true");
  });

  it("renders children when data is present", () => {
    render(<QueryBoundary query={q({ data: ["a", "b"] })}>{list}</QueryBoundary>);
    expect(screen.getAllByRole("listitem")).toHaveLength(2);
  });

  it("renders the empty node for empty arrays and custom emptiness", () => {
    const { rerender } = render(<QueryBoundary query={q({ data: [] })} empty={<p>No items</p>}>{list}</QueryBoundary>);
    expect(screen.getByText("No items")).toBeInTheDocument();
    rerender(<QueryBoundary query={q({ data: ["only"] })} isEmpty={(d) => d.length < 2} empty={<p>Too few</p>}>{list}</QueryBoundary>);
    expect(screen.getByText("Too few")).toBeInTheDocument();
  });

  it("shows a retryable error state that calls refetch and onRetry", async () => {
    const user = userEvent.setup();
    const refetch = vi.fn();
    const onRetry = vi.fn();
    render(
      <QueryBoundary query={q({ isError: true, error: { response: { status: 500, data: { error: { message: "Boom" } } } } as any, refetch: refetch as any })} onRetry={onRetry}>
        {list}
      </QueryBoundary>,
    );
    expect(screen.getByRole("alert")).toHaveTextContent("Boom");
    await user.click(screen.getByRole("button", { name: /try again/i }));
    expect(refetch).toHaveBeenCalledTimes(1);
    expect(onRetry).toHaveBeenCalledTimes(1);
  });

  it("shows the temporarily-unavailable state for 503", () => {
    render(<QueryBoundary query={q({ isError: true, error: { response: { status: 503 } } as any })}>{list}</QueryBoundary>);
    expect(screen.getByRole("alert")).toHaveTextContent("Temporarily unavailable");
  });

  it("shows the offline state when the browser is offline and there is no data", () => {
    vi.spyOn(window.navigator, "onLine", "get").mockReturnValue(false);
    render(<QueryBoundary query={q({ isError: true, error: new Error("x") })}>{list}</QueryBoundary>);
    expect(screen.getByRole("alert")).toHaveTextContent("You're offline");
  });

  it("keeps showing stale data with a refresh-failed notice when a background refetch fails", () => {
    render(<QueryBoundary query={q({ data: ["stale"], isError: true, error: new Error("x") })}>{list}</QueryBoundary>);
    expect(screen.getByText("stale")).toBeInTheDocument();
    expect(screen.getByRole("alert")).toHaveTextContent("Couldn't refresh");
  });

  it("renders the partial-results banner (default and custom text)", () => {
    const { rerender } = render(<QueryBoundary query={q({ data: ["a"] })} partial>{list}</QueryBoundary>);
    expect(screen.getByRole("status")).toHaveTextContent(/incomplete/i);
    rerender(<QueryBoundary query={q({ data: ["a"] })} partial="Custom partial">{list}</QueryBoundary>);
    expect(screen.getByRole("status")).toHaveTextContent("Custom partial");
  });
});
