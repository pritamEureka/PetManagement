import { expect, test } from "@playwright/test";
import { login, requireAccount } from "./helpers";

// Grounded in src/pages/pets/PetsPage.tsx ("My pets" heading, "Add pet" / "Add your first pet" buttons) and
// PetFormDialog.tsx (dialog "Add a pet", <Label htmlFor="name">Name</Label>, submit "Add pet").
test("pet management: create a pet and see it listed", async ({ page }) => {
  const user = requireAccount("USER");
  await login(page, user);

  const name = `E2E-Pet-${Date.now()}`;
  await page.goto("/pets");
  await expect(page.getByRole("heading", { name: "My pets" })).toBeVisible();
  await page.getByRole("button", { name: /^(add pet|add your first pet)$/i }).first().click();

  const dialog = page.getByRole("dialog", { name: "Add a pet" });
  await expect(dialog).toBeVisible();
  await dialog.getByLabel("Name", { exact: true }).fill(name);
  // Animal type keeps its schema default; no selection is needed for the required fields.
  await dialog.getByRole("button", { name: "Add pet", exact: true }).click();

  await expect(dialog).toBeHidden();
  await expect(page.getByText(name).first()).toBeVisible();
});
