using FluentValidation;
using Pawzaroo.Application.Modules.Marketplace.Dtos;

namespace Pawzaroo.Application.Modules.Marketplace.Validators;

public class RegisterStoreInputValidator : AbstractValidator<RegisterStoreInput>
{
    public RegisterStoreInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Address).MaximumLength(256);
        RuleFor(x => x.City).MaximumLength(128);
        RuleFor(x => x.Country).MaximumLength(64);
        RuleFor(x => x.PhoneNumber).MaximumLength(32);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).MaximumLength(256);
        RuleFor(x => x.LogoUrl).MaximumLength(1024);
        RuleFor(x => x.BannerUrl).MaximumLength(1024);
    }
}

public class UpdateStoreInputValidator : AbstractValidator<UpdateStoreInput>
{
    public UpdateStoreInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).MaximumLength(256);
        RuleFor(x => x.PhoneNumber).MaximumLength(32);
        RuleFor(x => x.LogoUrl).MaximumLength(1024);
        RuleFor(x => x.BannerUrl).MaximumLength(1024);
    }
}

public class SubmitStoreOwnerProfileValidator : AbstractValidator<SubmitStoreOwnerProfileInput>
{
    public SubmitStoreOwnerProfileValidator()
    {
        RuleFor(x => x.LegalName).NotEmpty().MaximumLength(256);
        RuleFor(x => x.BusinessName).MaximumLength(256);
        RuleFor(x => x.TradeLicenseNumber).MaximumLength(128);
        RuleFor(x => x.NationalIdNumber).MaximumLength(64);
        RuleFor(x => x.TaxId).MaximumLength(64);

        // Require at least one of NID or Trade License so admins have something to verify.
        RuleFor(x => x).Must(p =>
            !string.IsNullOrWhiteSpace(p.NationalIdNumber) ||
            !string.IsNullOrWhiteSpace(p.TradeLicenseNumber))
            .WithMessage("Provide a trade license number or national ID.");
    }
}

public class CreateProductInputValidator : AbstractValidator<CreateProductInput>
{
    public CreateProductInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(64)
            .Matches("^[A-Za-z0-9_-]+$").WithMessage("SKU may only contain letters, digits, '_' or '-'.");
        RuleFor(x => x.Description).MaximumLength(8000);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DiscountPrice).GreaterThanOrEqualTo(0)
            .LessThan(x => x.Price).When(x => x.DiscountPrice.HasValue)
            .WithMessage("Discount price must be less than price.");
        RuleFor(x => x.StockQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ImageUrls).Must(u => u == null || u.Count <= 12)
            .WithMessage("Up to 12 images per product.");
    }
}

public class UpdateProductInputValidator : AbstractValidator<UpdateProductInput>
{
    public UpdateProductInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DiscountPrice).GreaterThanOrEqualTo(0)
            .LessThan(x => x.Price).When(x => x.DiscountPrice.HasValue);
        RuleFor(x => x.ImageUrls).Must(u => u == null || u.Count <= 12);
    }
}

public class AdjustInventoryValidator : AbstractValidator<AdjustInventoryInput>
{
    public AdjustInventoryValidator()
    {
        RuleFor(x => x.QuantityChange).NotEqual(0).WithMessage("QuantityChange cannot be zero.");
        RuleFor(x => x.Notes).MaximumLength(512);
    }
}

public class AddToCartValidator : AbstractValidator<AddToCartInput>
{
    public AddToCartValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(99);
    }
}

public class UpdateCartItemValidator : AbstractValidator<UpdateCartItemInput>
{
    public UpdateCartItemValidator()
    {
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0).LessThanOrEqualTo(99);
    }
}

public class CheckoutInputValidator : AbstractValidator<CheckoutInput>
{
    public CheckoutInputValidator()
    {
        // Either a saved address id OR an inline address is required.
        RuleFor(x => x).Must(c =>
            c.ShippingAddressId.HasValue ||
            (!string.IsNullOrWhiteSpace(c.ShippingAddress) && c.ShippingAddress.Length >= 5))
            .WithMessage("Provide a saved shipping address or a shipping address line.");

        RuleFor(x => x.ShippingAddress).MaximumLength(512);
        RuleFor(x => x.ShippingCity).MaximumLength(128);
        RuleFor(x => x.ShippingCountry).MaximumLength(64);
        RuleFor(x => x.PaymentMethod).MaximumLength(32);
    }
}

public class CreateProductReviewValidator : AbstractValidator<CreateProductReviewInput>
{
    public CreateProductReviewValidator()
    {
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Comment).MaximumLength(2000);
    }
}

public class CreateStoreReviewValidator : AbstractValidator<CreateStoreReviewInput>
{
    public CreateStoreReviewValidator()
    {
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Comment).MaximumLength(2000);
    }
}

public class UpsertShippingAddressValidator : AbstractValidator<UpsertShippingAddressInput>
{
    public UpsertShippingAddressValidator()
    {
        RuleFor(x => x.Label).NotEmpty().MaximumLength(32);
        RuleFor(x => x.RecipientName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.PhoneNumber).NotEmpty().MaximumLength(32);
        RuleFor(x => x.AddressLine1).NotEmpty().MaximumLength(256);
        RuleFor(x => x.AddressLine2).MaximumLength(256);
        RuleFor(x => x.City).MaximumLength(128);
        RuleFor(x => x.State).MaximumLength(128);
        RuleFor(x => x.Country).MaximumLength(64);
        RuleFor(x => x.PostalCode).MaximumLength(32);
    }
}

public class UpsertCommissionConfigValidator : AbstractValidator<UpsertCommissionConfigurationInput>
{
    public UpsertCommissionConfigValidator()
    {
        RuleFor(x => x.CommissionPercent).InclusiveBetween(0, 100);
        RuleFor(x => x.FlatFee).GreaterThanOrEqualTo(0).When(x => x.FlatFee.HasValue);
        RuleFor(x => x.MinCommission).GreaterThanOrEqualTo(0).When(x => x.MinCommission.HasValue);
        RuleFor(x => x.MaxCommission).GreaterThanOrEqualTo(0).When(x => x.MaxCommission.HasValue);
        RuleFor(x => x).Must(c => !c.EffectiveTo.HasValue || c.EffectiveTo > c.EffectiveFrom)
            .WithMessage("EffectiveTo must be after EffectiveFrom.");
    }
}

public class CreateReturnRequestValidator : AbstractValidator<CreateReturnRequestInput>
{
    public CreateReturnRequestValidator()
    {
        RuleFor(x => x.OrderItemId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}
