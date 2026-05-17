using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Messaging;
using Pawzaroo.Infrastructure.Messaging;

namespace Pawzaroo.Worker.Jobs;

/// <summary>
/// Maintains the Redis cache for the social feed:
///   - On <see cref="FeedPostCreated"/> invalidate the cached first page.
///   - On <see cref="FeedPostReacted"/> / <see cref="CommentAdded"/> bump
///     the per-post counters so the bell/badge update is near-instant.
/// </summary>
public class FeedDenormalizerJob : KafkaConsumerBase
{
    public FeedDenormalizerJob(IOptions<KafkaOptions> options, IServiceScopeFactory scopeFactory,
        ILogger<FeedDenormalizerJob> logger)
        : base(options, scopeFactory, logger) { }

    protected override string GroupId => KafkaConsumerGroups.FeedDenormalizer;
    protected override IReadOnlyList<string> Topics => new[]
    {
        Options.Topics.PostEvents,
        Options.Topics.CommentEvents
    };

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    protected override async Task HandleAsync(string topic, JsonElement data, EventEnvelopeHeader header,
        IServiceProvider scope, CancellationToken ct)
    {
        var feed = scope.GetRequiredService<IFeedCache>();

        switch (header.EventType)
        {
            case nameof(FeedPostCreated):
                await feed.InvalidateFeedFirstPagesAsync(ct);
                Logger.LogDebug("[feed-denorm] invalidated first pages on post create");
                break;

            case nameof(FeedPostReacted):
            {
                var ev = data.Deserialize<FeedPostReacted>(Json);
                if (ev is null) return;
                await feed.BumpReactionCountAsync(ev.PostId, 1, ct);
                break;
            }
            case nameof(CommentAdded):
            {
                var ev = data.Deserialize<CommentAdded>(Json);
                if (ev is null) return;
                await feed.BumpCommentCountAsync(ev.PostId, 1, ct);
                break;
            }
        }
    }
}
