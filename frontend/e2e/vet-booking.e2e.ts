import { expect, test } from "@playwright/test";
import { login, requireAccount } from "./helpers";

// Grounded in VetsPage.tsx (cards are links to /vets/:id showing "Dr. {name}"), VetDetailPage.tsx (link "Book" ->
// /vets/:id/book) and AppointmentBookingPage.tsx (SlotPicker time buttons, "Confirm booking", then navigation to
// /appointments?highlight=...).
test("vet booking: pick a vet, choose a slot and confirm", async ({ page }) => {
  const user = requireAccount("USER");
  await login(page, user);

  await page.goto("/vets");
  const vetCard = page.locator('a[href^="/vets/"]').filter({ hasText: /Dr\./ }).first();
  await expect(vetCard).toBeVisible();
  await vetCard.click();

  await page.getByRole("link", { name: "Book", exact: true }).click();
  await expect(page).toHaveURL(/\/vets\/[^/]+\/book/);

  // Slot buttons render a localized time (e.g. "09:30" or "09:30 AM"); none visible means no seeded availability.
  const slot = page.getByRole("button", { name: /^\d{1,2}:\d{2}/ }).first();
  await expect(slot, "demo data must contain an available slot for the first vet").toBeVisible();
  await slot.click();
  await page.getByRole("button", { name: "Confirm booking" }).click();

  await expect(page).toHaveURL(/\/appointments/);
});
