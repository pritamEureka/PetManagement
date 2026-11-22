using Pawzaroo.Application.Modules.Marketplace;
using Pawzaroo.Domain.Store;
using Xunit;

namespace Pawzaroo.Tests.Unit.Commerce;

public class GiftCardPurchaseTests
{
    [Theory]
    [InlineData(99.99, false)]
    [InlineData(100, true)]
    [InlineData(50000, true)]
    [InlineData(50000.01, false)]
    [InlineData(-5, false)]
    public void purchase_amount_limits(double amount, bool ok) =>
        Assert.Equal(ok, GiftCardMath.IsValidPurchaseAmount((decimal)amount));

    [Fact]
    public void code_is_masked_until_the_card_is_active()
    {
        const string code = "ABCD-EFGH-JKLM-NPQR";
        Assert.Equal("****-NPQR", GiftCardMath.DisplayCode(code, GiftCardStatus.Pending));
        Assert.Equal("****-NPQR", GiftCardMath.DisplayCode(code, GiftCardStatus.Cancelled));
        Assert.Equal(code, GiftCardMath.DisplayCode(code, GiftCardStatus.Active));
    }

    [Fact]
    public void pending_cards_are_never_usable()
    {
        Assert.False(GiftCardMath.IsUsable(GiftCardStatus.Pending, 500m, null, DateTime.UtcNow));
    }
}
