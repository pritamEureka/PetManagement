using FluentAssertions;
using Pawzaroo.Domain.Notifications;
using Pawzaroo.Infrastructure.Modules.Push;
using Xunit;

namespace Pawzaroo.Tests.Unit.Push;

public class NotificationDigestFormatterTests
{
    [Fact]
    public void Title_pluralises_total()
    {
        NotificationDigestFormatter.Title(new Dictionary<string, int> { ["orders"] = 1 }).Should().Be("1 new notification");
        NotificationDigestFormatter.Title(new Dictionary<string, int> { ["orders"] = 3, ["social"] = 2 }).Should().Be("5 new notifications");
    }

    [Fact]
    public void Body_orders_by_count_then_name_and_replaces_underscores()
    {
        var body = NotificationDigestFormatter.Body(new Dictionary<string, int>
        {
            ["social"] = 2, ["orders"] = 3, ["lost_found"] = 2,
        });
        body.Should().Be("3 orders, 2 lost found, 2 social");
    }

    [Fact]
    public void Body_folds_extra_categories()
    {
        var counts = new Dictionary<string, int> { ["a"] = 5, ["b"] = 4, ["c"] = 3, ["d"] = 2 };
        NotificationDigestFormatter.Body(counts, 2).Should().Be("5 a, 4 b and 5 more");
    }

    [Fact]
    public void Body_empty_when_no_counts() =>
        NotificationDigestFormatter.Body(new Dictionary<string, int>()).Should().BeEmpty();

    [Fact]
    public void Mute_filter_applies_category_and_source_mutes()
    {
        var mutes = new List<(NotificationMuteScope, string)>
        {
            (NotificationMuteScope.Category, "orders"),
            (NotificationMuteScope.Conversation, "abc"),
        };
        NotificationMuteFilter.IsMuted(mutes, null, "/orders/1").Should().BeTrue();
        NotificationMuteFilter.IsMuted(mutes, "{\"conversationId\":\"ABC\"}", "/messages").Should().BeTrue();
        NotificationMuteFilter.IsMuted(mutes, "{\"conversationId\":\"zzz\"}", "/messages").Should().BeFalse();
        NotificationMuteFilter.IsMuted(Array.Empty<(NotificationMuteScope, string)>(), null, "/orders/1").Should().BeFalse();
    }
}
