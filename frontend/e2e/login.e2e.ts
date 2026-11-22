import { expect, test } from "@playwright/test";
import { login, requireAccount } from "./helpers";

// Grounded in src/pages/LoginPage.tsx + src/lib/schemas.ts (loginSchema).
test.describe("login", () => {
  test("rejects bad credentials and stays on /login", async ({ page }) => {
    await page.goto("/login");
    await page.getByLabel("Email", { exact: true }).fill("nobody@example.invalid");
    await page.getByLabel("Password", { exact: true }).fill("definitely-wrong-password");
    await page.getByRole("button", { name: "Sign in", exact: true }).click();
    await expect(page).toHaveURL(/\/login/);
  });

  test("requires a password (client-side zod message)", async ({ page }) => {
    // An invalid-email test was removed: <input type="email"> is blocked by native browser validation before
    // react-hook-form can show "Enter a valid email.", so that message cannot be asserted reliably.
    await page.goto("/login");
    await page.getByLabel("Email", { exact: true }).fill("someone@example.com");
    await page.getByRole("button", { name: "Sign in", exact: true }).click();
    await expect(page.getByText("Password required.")).toBeVisible();
  });

  test("signs in a seeded user and lands on /home", async ({ page }) => {
    const user = requireAccount("USER");
    await login(page, user);
    await expect(page).toHaveURL(/\/home/);
  });

  test("protected routes redirect anonymous visitors to login", async ({ page }) => {
    await page.goto("/pets");
    await expect(page).toHaveURL(/\/login/);
  });
});
