namespace Pawzaroo.Infrastructure.Messaging;

/// <summary>
/// Single place that maps LOGICAL topic / consumer-group names (what code, config and the
/// DeadLetterRecord table use) to PHYSICAL names on the wire (<c>Kafka:TopicPrefix</c> + logical).
/// An empty prefix is a no-op, so existing deployments are unchanged.
/// </summary>
public static class KafkaTopicNaming
{
    /// <summary>Logical to physical. Null/empty prefix returns the topic unchanged.</summary>
    public static string Apply(string? prefix, string topic)
        => string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(topic) ? topic : prefix + topic;

    /// <summary>Physical to logical: strips one leading prefix when present, otherwise returns the name unchanged.</summary>
    public static string Strip(string? prefix, string topic)
        => !string.IsNullOrEmpty(prefix) && !string.IsNullOrEmpty(topic)
           && topic.StartsWith(prefix, StringComparison.Ordinal)
            ? topic[prefix.Length..]
            : topic;

    /// <summary>Consumer group ids are prefixed exactly like topics so environments never share offsets.</summary>
    public static string ApplyGroup(string? prefix, string groupId) => Apply(prefix, groupId);
}
