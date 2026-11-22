import { expect, test } from "@playwright/test";
import { login, requireAccount } from "./helpers";

// Grounded in AdoptionListPage.tsx (cards link to /adoption/:id), AdoptionDetailPage.tsx ("Express interest" card with a
// textarea and "Send request") and admin/AdoptionApprovalPage.tsx (route /admin/adoption-approvals, title
// "Adoption approvals", row buttons "Approve"/"Reject", empty state "No pending listings"). The request is NOT submitted.
test.describe("adoption", () => {
  test("a signed-in user can open a listing and reach the request form", async ({ page }) => {
    const user = requireAccount("USER");
    await login(page, user);

    await page.goto("/adoption");
    // Listing cards link to /adoption/<uuid>; other /adoption/* links (mine, saved, ...) are not uuids.
    const named = ["mine", "saved", "pipeline", "follow-ups", "my-applications", "profile", "wanted"];
    const listing = page.locator(
      'a[href^="/adoption/"]' + named.map((n) => `:not([href^="/adoption/${n}"])`).join(""),
    ).first();
    await expect(listing, "demo data must contain an approved listing not owned by the test user").toBeVisible();
    await listing.click();
    await expect(page).toHaveURL(/\/adoption\/[0-9a-f-]{36}/i);

    // Shown to non-owners of an open listing; the button stays disabled until a message is typed.
    await expect(page.getByText("Express interest")).toBeVisible();
    await expect(page.getByRole("button", { name: "Send request" })).toBeDisabled();
  });

  test("admin approval queue lists pending listings with Approve/Reject, or the empty state", async ({ page }) => {
    const admin = requireAccount("ADMIN");
    await login(page, admin);

    await page.goto("/admin/adoption-approvals");
    await expect(page.getByRole("heading", { name: "Adoption approvals" })).toBeVisible();

    const approve = page.getByRole("button", { name: "Approve", exact: true }).first();
    if (await approve.count()) {
      await expect(page.getByRole("button", { name: "Reject", exact: true }).first()).toBeVisible();
      await approve.click();
      await expect(page.getByText(/^Approved:/)).toBeVisible();
    } else {
      await expect(page.getByText("No pending listings")).toBeVisible();
    }
  });
});
