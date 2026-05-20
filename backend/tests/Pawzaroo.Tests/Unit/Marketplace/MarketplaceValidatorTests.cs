using FluentValidation.TestHelper;
using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Application.Modules.Marketplace.Validators;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Store;
using Xunit;

namespace Pawzaroo.Tests.Unit.Marketplace;

public class MarketplaceValidatorTests
{
    [Fact]
    public void register_store_requires_name_and_valid_optional_email()
    {
        var validator = new RegisterStoreInputValidator();

        validator.TestValidate(new RegisterStoreInput("", null, null, null, null, null, null, null, "bad-email"))
            .ShouldHaveValidationErrorFor(x => x.Name);

        validator.TestValidate(new RegisterStoreInput("Happy Paws", null, null, null, null, null, null, null, "bad-email"))
            .ShouldHaveValidationErrorFor(x => x.Email);

        validator.TestValidate(new RegisterStoreInput("Happy Paws", null, null, null, null, null, null, null, "store@example.com"))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void store_owner_profile_requires_verification_identifier()
    {
        var validator = new SubmitStoreOwnerProfileValidator();
        var missingIdentifier = new SubmitStoreOwnerProfileInput(
            "Alex Owner", null, null, null, null, null, null, null, null);

        validator.TestValidate(missingIdentifier).ShouldHaveValidationErrorFor(x => x);

        var valid = missingIdentifier with { NationalIdNumber = "NID-123" };
        validator.TestValidate(valid).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void create_product_validates_sku_prices_stock_and_image_limit()
    {
        var validator = new CreateProductInputValidator();
        var valid = new CreateProductInput(
            "Cat Food", "CAT-1", null, 100m, 90m, 5, null, null,
            new[] { "https://example.com/cat-food.jpg" });

        validator.TestValidate(valid).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(valid with { Sku = "bad sku" }).ShouldHaveValidationErrorFor(x => x.Sku);
        validator.TestValidate(valid with { DiscountPrice = 100m }).ShouldHaveValidationErrorFor(x => x.DiscountPrice);
        validator.TestValidate(valid with { StockQuantity = -1 }).ShouldHaveValidationErrorFor(x => x.StockQuantity);
        validator.TestValidate(valid with { ImageUrls = Enumerable.Range(0, 13).Select(i => $"img-{i}").ToArray() })
            .ShouldHaveValidationErrorFor(x => x.ImageUrls);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(99, true)]
    [InlineData(100, false)]
    public void add_to_cart_restricts_quantity_range(int quantity, bool valid)
    {
        var validator = new AddToCartValidator();
        var result = validator.TestValidate(new AddToCartInput(Guid.NewGuid(), quantity));

        if (valid)
            result.ShouldNotHaveValidationErrorFor(x => x.Quantity);
        else
            result.ShouldHaveValidationErrorFor(x => x.Quantity);
    }

    [Fact]
    public void checkout_requires_saved_or_inline_shipping_address()
    {
        var validator = new CheckoutInputValidator();

        validator.TestValidate(new CheckoutInput(null, null, null, null, "cod"))
            .ShouldHaveValidationErrorFor(x => x);

        validator.TestValidate(new CheckoutInput(Guid.NewGuid(), null, null, null, "cod"))
            .ShouldNotHaveAnyValidationErrors();

        validator.TestValidate(new CheckoutInput(null, "123 Market Street", "Dhaka", "Bangladesh", "cod"))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void commission_dates_and_amounts_are_bounded()
    {
        var validator = new UpsertCommissionConfigValidator();
        var valid = new UpsertCommissionConfigurationInput(
            CommissionScope.Global, null, null, 10m, 5m, 1m, 100m,
            new DateTime(2026, 5, 20), new DateTime(2026, 6, 20), null);

        validator.TestValidate(valid).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(valid with { CommissionPercent = 101m })
            .ShouldHaveValidationErrorFor(x => x.CommissionPercent);
        validator.TestValidate(valid with { EffectiveTo = new DateTime(2026, 5, 19) })
            .ShouldHaveValidationErrorFor(x => x);
    }

    [Fact]
    public void shipping_address_requires_delivery_essentials()
    {
        var validator = new UpsertShippingAddressValidator();
        var input = new UpsertShippingAddressInput(
            "", "", "", "", null, "Dhaka", null, "Bangladesh", null, true);

        validator.TestValidate(input).ShouldHaveValidationErrorFor(x => x.Label);
        validator.TestValidate(input).ShouldHaveValidationErrorFor(x => x.RecipientName);
        validator.TestValidate(input).ShouldHaveValidationErrorFor(x => x.PhoneNumber);
        validator.TestValidate(input).ShouldHaveValidationErrorFor(x => x.AddressLine1);
    }
}
