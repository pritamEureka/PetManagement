using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Store;

namespace Pawzaroo.Application.Modules.Marketplace.Services;

public enum ProductListingScope { Public, MyStore, AdminAll }

public record ProductSearchQuery(
    ProductListingScope Scope = ProductListingScope.Public,
    string? Search = null,
    Guid? CategoryId = null,
    Guid? BrandId = null,
    Guid? StoreId = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    bool? Featured = null,
    bool? InStockOnly = null,
    string? Sort = null,       // "newest" | "price_asc" | "price_desc" | "rating_desc" | "best_selling"
    int Page = 1,
    int PageSize = 24);

public interface IStoreOwnerProfileService
{
    Task<StoreOwnerProfileDto?> GetMineAsync(CancellationToken ct = default);
    Task<StoreOwnerProfileDto> SubmitAsync(SubmitStoreOwnerProfileInput input, CancellationToken ct = default);

    Task<PageResult<StoreOwnerProfileDto>> ListForAdminAsync(ApprovalStatus? status, int page, int pageSize, CancellationToken ct = default);
    Task ApproveAsync(Guid profileId, string? adminNotes, CancellationToken ct = default);
    Task RejectAsync(Guid profileId, string? adminNotes, CancellationToken ct = default);
}

public interface IStoreService
{
    Task<StoreDto?> GetByIdAsync(Guid storeId, CancellationToken ct = default);
    Task<StoreDto?> GetMineAsync(CancellationToken ct = default);

    Task<Guid> RegisterAsync(RegisterStoreInput input, CancellationToken ct = default);
    Task UpdateAsync(Guid storeId, UpdateStoreInput input, CancellationToken ct = default);

    Task<PageResult<StoreDto>> SearchAsync(string? search, ApprovalStatus? status, int page, int pageSize, CancellationToken ct = default);

    Task ApproveAsync(Guid storeId, string? adminNotes, CancellationToken ct = default);
    Task RejectAsync(Guid storeId, string? adminNotes, CancellationToken ct = default);
    Task SuspendAsync(Guid storeId, string? adminNotes, CancellationToken ct = default);
    Task RestoreAsync(Guid storeId, CancellationToken ct = default);
    Task SetFeaturedAsync(Guid storeId, bool featured, CancellationToken ct = default);
    Task SetCommissionPercentAsync(Guid storeId, decimal percent, CancellationToken ct = default);
}

public interface IProductCategoryService
{
    Task<IReadOnlyList<ProductCategoryDto>> ListAsync(CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateProductCategoryInput input, CancellationToken ct = default);
    Task UpdateAsync(Guid id, CreateProductCategoryInput input, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IProductService
{
    Task<PageResult<ProductSummaryDto>> SearchAsync(ProductSearchQuery query, CancellationToken ct = default);
    Task<ProductDetailDto?> GetByIdAsync(Guid productId, CancellationToken ct = default);

    Task<Guid> CreateAsync(CreateProductInput input, CancellationToken ct = default);
    Task UpdateAsync(Guid productId, UpdateProductInput input, CancellationToken ct = default);
    Task DeleteAsync(Guid productId, CancellationToken ct = default);

    Task SetFeaturedAsync(Guid productId, bool featured, CancellationToken ct = default);
    Task SetActiveAsync(Guid productId, bool active, CancellationToken ct = default);
}

public interface IInventoryService
{
    Task<InventoryAdjustmentDto> AdjustAsync(Guid productId, AdjustInventoryInput input, CancellationToken ct = default);
    Task<PageResult<InventoryAdjustmentDto>> ListAsync(Guid productId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Atomically decrements stock, writes the audit row, and emits a low-stock event if needed. Throws if insufficient.</summary>
    Task DecrementForOrderAsync(Guid productId, int quantity, Guid orderId, CancellationToken ct = default);
}

public interface ICartService
{
    Task<CartDto> GetMineAsync(CancellationToken ct = default);
    Task<CartDto> AddAsync(AddToCartInput input, CancellationToken ct = default);
    Task<CartDto> UpdateItemAsync(Guid cartItemId, UpdateCartItemInput input, CancellationToken ct = default);
    Task<CartDto> RemoveItemAsync(Guid cartItemId, CancellationToken ct = default);
    Task ClearAsync(CancellationToken ct = default);
}

public interface IOrderService
{
    Task<OrderDto> CheckoutAsync(CheckoutInput input, CancellationToken ct = default);
    Task<OrderDto?> GetByIdAsync(Guid orderId, CancellationToken ct = default);
    Task<PageResult<OrderDto>> ListMineAsync(int page, int pageSize, CancellationToken ct = default);

    /// <summary>For a store owner — orders containing at least one of their products.</summary>
    Task<PageResult<OrderDto>> ListForStoreAsync(Guid storeId, OrderStatus? status, int page, int pageSize, CancellationToken ct = default);
    Task<PageResult<OrderDto>> ListForAdminAsync(OrderStatus? status, int page, int pageSize, CancellationToken ct = default);

    Task UpdateStatusAsync(Guid orderId, OrderStatus status, CancellationToken ct = default);
    Task UpdateShipmentStatusAsync(Guid orderId, ShipmentStatus status, string? trackingNumber, CancellationToken ct = default);
    Task CancelAsync(Guid orderId, string? reason, CancellationToken ct = default);
    Task RefundAsync(Guid orderId, decimal? amount, CancellationToken ct = default);
}

public interface IProductReviewService
{
    Task<PageResult<ProductReviewDto>> ListAsync(Guid productId, int page, int pageSize, CancellationToken ct = default);
    Task<ProductReviewDto> CreateAsync(Guid productId, CreateProductReviewInput input, CancellationToken ct = default);
    Task DeleteAsync(Guid reviewId, CancellationToken ct = default);
}

public interface IStoreReviewService
{
    Task<PageResult<StoreReviewDto>> ListAsync(Guid storeId, int page, int pageSize, CancellationToken ct = default);
    Task<StoreReviewDto> CreateAsync(Guid storeId, CreateStoreReviewInput input, CancellationToken ct = default);
}

public interface IShippingAddressService
{
    Task<IReadOnlyList<ShippingAddressDto>> ListMineAsync(CancellationToken ct = default);
    Task<Guid> CreateAsync(UpsertShippingAddressInput input, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpsertShippingAddressInput input, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task SetDefaultAsync(Guid id, CancellationToken ct = default);
}

public interface ICommissionConfigurationService
{
    Task<IReadOnlyList<CommissionConfigurationDto>> ListAsync(CancellationToken ct = default);
    Task<Guid> UpsertAsync(UpsertCommissionConfigurationInput input, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Resolve the effective commission for a (store, category) pair at <paramref name="atUtc"/>.</summary>
    Task<decimal> ResolveAsync(Guid storeId, Guid? categoryId, DateTime atUtc, CancellationToken ct = default);
}

public interface IReturnService
{
    Task<ReturnRequestDto> CreateAsync(CreateReturnRequestInput input, CancellationToken ct = default);
    Task ApproveAsync(Guid requestId, decimal? refundAmount, CancellationToken ct = default);
    Task RejectAsync(Guid requestId, string? notes, CancellationToken ct = default);
    Task<PageResult<ReturnRequestDto>> ListAsync(int page, int pageSize, CancellationToken ct = default);
}

public interface ISalesReportService
{
    Task<StoreSalesReportDto> ForStoreAsync(Guid storeId, DateTime from, DateTime to, CancellationToken ct = default);
}

public interface IMarketplaceCache
{
    Task<PageResult<ProductSummaryDto>?> GetProductFirstPageAsync(string variantKey, CancellationToken ct = default);
    Task SetProductFirstPageAsync(string variantKey, PageResult<ProductSummaryDto> page, CancellationToken ct = default);
    Task InvalidateProductsAsync(CancellationToken ct = default);

    Task<StoreDto?> GetStoreAsync(Guid storeId, CancellationToken ct = default);
    Task SetStoreAsync(StoreDto store, CancellationToken ct = default);
    Task InvalidateStoreAsync(Guid storeId, CancellationToken ct = default);
}
