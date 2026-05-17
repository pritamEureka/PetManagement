using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Pawzaroo.Infrastructure.Caching;

/// <summary>
/// Singleton ConnectionMultiplexer factory.
///
/// Why factory: <c>ConnectionMultiplexer.Connect()</c> is expensive (TCP + SSL),
/// and the multiplexer is designed to be shared. Wraps configuration parsing,
/// names the connection (so server output shows our app), and turns connection
/// failures into a fast crash with an actionable error rather than a hang.
/// </summary>
public static class RedisConnection
{
    public static IConnectionMultiplexer Build(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        var cs = configuration.GetConnectionString("Redis") ?? "localhost:6379";

        var options = ConfigurationOptions.Parse(cs);
        options.AbortOnConnectFail   = false;     // retry instead of failing app start
        options.ConnectRetry         = 5;
        options.ConnectTimeout       = 5000;
        options.SyncTimeout          = 5000;
        options.ClientName           = $"pawzaroo-{Environment.MachineName}";
        options.KeepAlive            = 60;
        options.ReconnectRetryPolicy = new ExponentialRetry(500);

        var logger = loggerFactory.CreateLogger("Redis");
        var muxer = ConnectionMultiplexer.Connect(options);

        // StackExchange.Redis surfaces lifecycle via events; we relay them to ILogger
        // instead of using the TextWriter overload so structured logging keeps working.
        muxer.ConnectionFailed   += (_, e) => logger.LogWarning("[redis] connection failed {Type} {Failure}", e.ConnectionType, e.FailureType);
        muxer.ConnectionRestored += (_, e) => logger.LogInformation("[redis] connection restored {Type}", e.ConnectionType);
        muxer.ErrorMessage       += (_, e) => logger.LogWarning("[redis] error {Message}", e.Message);
        logger.LogInformation("[redis] connected to {Endpoints}", string.Join(", ", muxer.GetEndPoints().Select(e => e.ToString())));
        return muxer;
    }
}
