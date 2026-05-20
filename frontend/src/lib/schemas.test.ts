import { describe, expect, it } from "vitest";
import {
  adoptionListingSchema,
  checkoutSchema,
  createPostSchema,
  productSchema,
  registerSchema,
  selfRegisterRoles,
} from "./schemas";

describe("schemas", () => {
  it("keeps privileged roles out of self-registration", () => {
    const roleValues = selfRegisterRoles.map((role) => role.value);

    expect(roleValues).toContain("User");
    expect(roleValues).toContain("Veterinarian");
    expect(roleValues).not.toContain("Admin");
    expect(roleValues).not.toContain("SuperAdmin");
    expect(registerSchema.safeParse({
      displayName: "Root User",
      email: "root@example.com",
      password: "Password1",
      requestedRole: "Admin",
    }).success).toBe(false);
  });

  it("validates registration password complexity and accepts allowed roles", () => {
    expect(registerSchema.safeParse({
      displayName: "Taylor",
      email: "taylor@example.com",
      password: "Password1",
      phoneNumber: "",
      requestedRole: "StoreOwner",
    }).success).toBe(true);

    const weak = registerSchema.safeParse({
      displayName: "Taylor",
      email: "taylor@example.com",
      password: "password",
      requestedRole: "User",
    });

    expect(weak.success).toBe(false);
  });

  it("requires text or media for posts", () => {
    expect(createPostSchema.safeParse({ content: "", mediaUrls: [] }).success).toBe(false);
    expect(createPostSchema.safeParse({ content: "Found a lost cat", mediaUrls: [] }).success).toBe(true);
    expect(createPostSchema.safeParse({ content: "", mediaUrls: ["https://example.com/cat.jpg"] }).success).toBe(true);
  });

  it("guards adoption listing bounds", () => {
    const base = {
      title: "Friendly young dog",
      animalType: "Dog",
      gender: "Male",
      vaccinated: true,
      contactPreference: "Chat",
      adoptionFee: 0,
      photoUrls: ["https://example.com/a.jpg"],
    };

    expect(adoptionListingSchema.safeParse({ ...base, ageMonths: 24 }).success).toBe(true);
    expect(adoptionListingSchema.safeParse({ ...base, ageMonths: 601 }).success).toBe(false);
    expect(adoptionListingSchema.safeParse({
      ...base,
      photoUrls: Array.from({ length: 13 }, (_, i) => `https://example.com/${i}.jpg`),
    }).success).toBe(false);
  });

  it("requires either a saved or inline shipping address for checkout", () => {
    expect(checkoutSchema.safeParse({ paymentMethod: "cod" }).success).toBe(false);
    expect(checkoutSchema.safeParse({
      shippingAddress: "123 Market Street",
      paymentMethod: "cod",
    }).success).toBe(true);
  });

  it("prevents invalid marketplace product pricing and SKU values", () => {
    const base = {
      name: "Premium Kibble",
      sku: "KIBBLE-1",
      price: 120,
      stockQuantity: 5,
    };

    expect(productSchema.safeParse(base).success).toBe(true);
    expect(productSchema.safeParse({ ...base, sku: "bad sku" }).success).toBe(false);
    expect(productSchema.safeParse({ ...base, discountPrice: 130 }).success).toBe(false);
  });
});
