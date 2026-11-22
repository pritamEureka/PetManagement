import { expect, test } from "@playwright/test";
import { login, requireAccount } from "./helpers";

// Grounded in MarketplacePage.tsx (heading "Marketplace", product card button "Add"), CartPage.tsx (heading "Cart",
// button "Checkout") and CheckoutPage.tsx (card "Shipping details", submit "Place order").
// Stops before placing the order (no real payment).
test("checkout: add a product to the cart and reach the shipping step", async ({ page }) => {
  const user = requireAccount("USER");
  await login(page, user);

  await page.goto("/store");
  await expect(page.getByRole("heading", { name: "Marketplace" })).toBeVisible();
  const add = page.getByRole("button", { name: "Add", exact: true }).first();
  await expect(add, "demo data must contain an in-stock product").toBeVisible();
  await add.click();

  await page.goto("/cart");
  await expect(page.getByRole("heading", { name: "Cart" })).toBeVisible();
  await page.getByRole("button", { name: "Checkout", exact: true }).click();

  await expect(page).toHaveURL(/\/checkout/);
  await expect(page.getByText("Shipping details")).toBeVisible();
  await expect(page.getByRole("button", { name: "Place order" })).toBeVisible();
});
