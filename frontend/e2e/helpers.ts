import { expect, test, type Page } from "@playwright/test";

export type Account = { email: string; password: string };

/** Reads credentials from env; the calling test is skipped when they are not provided. Nothing is hard-coded. */
export function account(prefix: "USER" | "ADMIN" | "VET" | "STORE"): Account | null {
  const email = process.env[`E2E_${prefix}_EMAIL`];
  const password = process.env[`E2E_${prefix}_PASSWORD`];
  return email && password ? { email, password } : null;
}

export function requireAccount(prefix: "USER" | "ADMIN" | "VET" | "STORE"): Account {
  const a = account(prefix);
  test.skip(!a, `E2E_${prefix}_EMAIL / E2E_${prefix}_PASSWORD not set (see e2e/README.md)`);
  return a as Account;
}

/**
 * LoginPage.tsx: <Label htmlFor="email">Email</Label>, <Label htmlFor="password">Password</Label>, submit "Sign in".
 * The password field has a show/hide toggle whose aria-label also contains "password", so the label match must be exact.
 * Successful login navigates to "/home" (or the page the user was redirected from).
 */
export async function login(page: Page, a: Account) {
  await page.goto("/login");
  await page.getByLabel("Email", { exact: true }).fill(a.email);
  await page.getByLabel("Password", { exact: true }).fill(a.password);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).not.toHaveURL(/\/login/);
}
