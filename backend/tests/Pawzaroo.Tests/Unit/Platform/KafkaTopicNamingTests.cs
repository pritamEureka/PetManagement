using FluentAssertions;
using Pawzaroo.Infrastructure.Messaging;
using Xunit;

namespace Pawzaroo.Tests.Unit.Platform;

public class KafkaTopicNamingTests
{
    [Theory]
    [InlineData("", "pawzaroo.user.events", "pawzaroo.user.events")]
    [InlineData(null, "pawzaroo.user.events", "pawzaroo.user.events")]
    [InlineData("dev-", "pawzaroo.user.events", "dev-pawzaroo.user.events")]
    [InlineData("staging-", "pawzaroo.user.events.dlq", "staging-pawzaroo.user.events.dlq")]
    public void Apply_prepends_prefix_only_when_set(string? prefix, string topic, string expected)
        => KafkaTopicNaming.Apply(prefix, topic).Should().Be(expected);

    [Theory]
    [InlineData("dev-", "dev-pawzaroo.user.events", "pawzaroo.user.events")]
    [InlineData("dev-", "pawzaroo.user.events", "pawzaroo.user.events")]
    [InlineData("", "dev-pawzaroo.user.events", "dev-pawzaroo.user.events")]
    public void Strip_removes_one_leading_prefix(string prefix, string topic, string expected)
        => KafkaTopicNaming.Strip(prefix, topic).Should().Be(expected);

    [Fact]
    public void Apply_then_Strip_round_trips_and_group_ids_are_prefixed()
    {
        KafkaTopicNaming.Strip("dev-", KafkaTopicNaming.Apply("dev-", "t")).Should().Be("t");
        KafkaTopicNaming.ApplyGroup("dev-", "pawzaroo-api-inbox").Should().Be("dev-pawzaroo-api-inbox");
    }

    [Fact]
    public void Dlq_topic_is_prefixed_once_from_logical_name()
    {
        var logical = "pawzaroo.order.events";
        var dlq = logical + Pawzaroo.Application.Common.Messaging.KafkaTopics.DeadLetterSuffix;
        KafkaTopicNaming.Apply("dev-", dlq).Should().Be("dev-pawzaroo.order.events.dlq");
    }
}
