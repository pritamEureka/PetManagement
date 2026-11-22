import { describe, it, expect, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { DataTable, type Column } from "./DataTable";

interface Row { id: string; name: string; qty: number }
const rows: Row[] = [
  { id: "1", name: "Bravo", qty: 20 },
  { id: "2", name: "Alpha", qty: 3 },
  { id: "3", name: "Charlie", qty: 100 },
];
const columns: Column<Row>[] = [
  { key: "name", header: "Name", render: (r) => r.name, sortValue: (r) => r.name },
  { key: "qty", header: "Qty", render: (r) => String(r.qty), sortValue: (r) => r.qty },
  { key: "plain", header: "Plain", render: () => "x" },
];

const bodyNames = () => screen.getAllByRole("row").slice(1).map((tr) => within(tr).getAllByRole("cell")[0].textContent);
const headerFor = (name: string) => screen.getByRole("columnheader", { name: new RegExp(name) });

describe("DataTable", () => {
  it("renders a caption and only exposes aria-sort on sortable columns", () => {
    render(<DataTable data={rows} columns={columns} rowKey={(r) => r.id} caption="Stock" density="comfortable" />);
    expect(screen.getByRole("table", { name: "Stock" })).toBeInTheDocument();
    expect(headerFor("Name")).toHaveAttribute("aria-sort", "none");
    expect(headerFor("Qty")).toHaveAttribute("aria-sort", "none");
    expect(headerFor("Plain")).not.toHaveAttribute("aria-sort");
  });

  it("sorts ascending, then descending, and updates aria-sort (numbers compared numerically)", async () => {
    const user = userEvent.setup();
    render(<DataTable data={rows} columns={columns} rowKey={(r) => r.id} density="comfortable" />);
    expect(bodyNames()).toEqual(["Bravo", "Alpha", "Charlie"]); // original order

    await user.click(screen.getByRole("button", { name: /^Qty/ }));
    expect(headerFor("Qty")).toHaveAttribute("aria-sort", "ascending");
    expect(bodyNames()).toEqual(["Alpha", "Bravo", "Charlie"]); // 3, 20, 100 (not "100" < "20")

    await user.click(screen.getByRole("button", { name: /^Qty/ }));
    expect(headerFor("Qty")).toHaveAttribute("aria-sort", "descending");
    expect(bodyNames()).toEqual(["Charlie", "Bravo", "Alpha"]);

    await user.click(screen.getByRole("button", { name: /^Name/ }));
    expect(headerFor("Name")).toHaveAttribute("aria-sort", "ascending");
    expect(headerFor("Qty")).toHaveAttribute("aria-sort", "none");
    expect(bodyNames()).toEqual(["Alpha", "Bravo", "Charlie"]);
  });

  it("shows the empty node, and a busy status while loading", () => {
    const { rerender } = render(<DataTable data={[]} columns={columns} rowKey={(r) => r.id} empty={<p>Nothing here</p>} />);
    expect(screen.getByText("Nothing here")).toBeInTheDocument();
    rerender(<DataTable data={rows} columns={columns} rowKey={(r) => r.id} loading />);
    expect(screen.getByRole("status")).toHaveAttribute("aria-busy", "true");
    expect(screen.queryByRole("table")).not.toBeInTheDocument();
  });

  it("density toggle switches compact rows and persists the preference", async () => {
    const user = userEvent.setup();
    render(<DataTable data={rows} columns={columns} rowKey={(r) => r.id} showDensityToggle />);
    const toggle = screen.getByRole("button", { name: "Compact rows" });
    expect(toggle).toHaveAttribute("aria-pressed", "false");
    expect(screen.getAllByRole("cell")[0].className).toContain("py-2");

    await user.click(toggle);
    expect(screen.getByRole("button", { name: "Comfortable rows" })).toHaveAttribute("aria-pressed", "true");
    expect(screen.getAllByRole("cell")[0].className).toContain("text-xs");
    expect(localStorage.getItem("pawzaroo:table-density")).toBe(JSON.stringify("compact"));
  });

  it("an explicit density prop wins and hides the toggle", () => {
    render(<DataTable data={rows} columns={columns} rowKey={(r) => r.id} density="compact" showDensityToggle />);
    expect(screen.queryByRole("button", { name: /rows/ })).not.toBeInTheDocument();
    expect(screen.getAllByRole("cell")[0].className).toContain("text-xs");
  });

  it("clickable rows are keyboard accessible (Enter and Space)", async () => {
    const user = userEvent.setup();
    const onRowClick = vi.fn();
    render(<DataTable data={rows} columns={columns} rowKey={(r) => r.id} density="comfortable" onRowClick={onRowClick} />);
    const first = screen.getAllByRole("row")[1];
    first.focus();
    await user.keyboard("{Enter}");
    await user.keyboard(" ");
    expect(onRowClick).toHaveBeenCalledTimes(2);
    expect(onRowClick).toHaveBeenCalledWith(rows[0]);
  });
});
