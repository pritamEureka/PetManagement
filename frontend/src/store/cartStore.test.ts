import { beforeEach, describe, expect, it, vi } from "vitest";
import { useCartStore } from "./cartStore";
import { cartApi, type ServerCart } from "@/api/marketplace";

vi.mock("@/api/marketplace", () => ({
  cartApi: {
    get: vi.fn(),
    add: vi.fn(),
    update: vi.fn(),
    remove: vi.fn(),
    clear: vi.fn(),
  },
}));

const cart = (quantity = 2): ServerCart => ({
  id: "cart-1",
  currency: "BDT",
  subtotal: 200,
  totalItems: quantity,
  shippingFee: 0,
  tax: 0,
  total: 200,
  items: [{
    id: "line-1",
    productId: "product-1",
    productName: "Cat Food",
    imageUrl: null,
    storeId: "store-1",
    storeName: "Pet Store",
    quantity,
    unitPrice: 100,
    total: quantity * 100,
    stockAvailable: 10,
  }],
});

function resetCartStore() {
  useCartStore.setState({ lines: [], appliedCoupon: null });
}

describe("cartStore", () => {
  beforeEach(resetCartStore);

  it("hydrates local lines from the server cart", async () => {
    vi.mocked(cartApi.get).mockResolvedValue(cart());

    await useCartStore.getState().hydrate();

    expect(useCartStore.getState().lines).toEqual([{
      cartItemId: "line-1",
      productId: "product-1",
      name: "Cat Food",
      image: null,
      price: 100,
      quantity: 2,
      storeName: "Pet Store",
      stockAvailable: 10,
    }]);
    expect(useCartStore.getState().count()).toBe(2);
    expect(useCartStore.getState().subtotal()).toBe(200);
  });

  it("syncs add and quantity changes through the server API", async () => {
    vi.mocked(cartApi.add).mockResolvedValue(cart(1));
    vi.mocked(cartApi.update).mockResolvedValue(cart(3));

    await useCartStore.getState().add({
      id: "product-1",
      name: "Cat Food",
      sku: "CAT-1",
      price: 100,
      stockQuantity: 10,
      isActive: true,
      isFeatured: false,
      ratingAverage: 0,
      ratingCount: 0,
      storeId: "store-1",
      storeName: "Pet Store",
      imageUrls: [],
      createdAt: "2026-05-20T00:00:00Z",
    });
    await useCartStore.getState().setQty("product-1", 3);

    expect(cartApi.add).toHaveBeenCalledWith("product-1", 1);
    expect(cartApi.update).toHaveBeenCalledWith("line-1", 3);
    expect(useCartStore.getState().count()).toBe(3);
  });

  it("falls back to local updates when a line lacks a server cart item id", async () => {
    useCartStore.setState({
      lines: [{
        productId: "product-1",
        name: "Cat Food",
        price: 100,
        quantity: 2,
        storeName: "Pet Store",
      }],
    });

    await useCartStore.getState().setQty("product-1", 4);

    expect(cartApi.update).not.toHaveBeenCalled();
    expect(useCartStore.getState().lines[0].quantity).toBe(4);
  });

  it("clears cart lines and coupon state", async () => {
    vi.mocked(cartApi.clear).mockResolvedValue({} as never);
    useCartStore.setState({ lines: cart().items.map((item) => ({
      cartItemId: item.id,
      productId: item.productId,
      name: item.productName,
      price: item.unitPrice,
      quantity: item.quantity,
      storeName: item.storeName,
    })), appliedCoupon: { code: "SAVE10", discount: 10 } });

    await useCartStore.getState().clear();

    expect(cartApi.clear).toHaveBeenCalled();
    expect(useCartStore.getState().lines).toEqual([]);
    expect(useCartStore.getState().appliedCoupon).toBeNull();
  });
});
