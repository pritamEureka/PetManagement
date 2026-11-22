using System.Text.Json;
using Confluent.Kafka;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Messaging;

public enum FailureKind { Transient, Permanent }

/// <summary>
/// Exception taxonomy. Permanent = retrying cannot help (bad payload, validation, missing entity) so the
/// message goes straight to the DLQ / is poisoned. Everything else, including unknown exceptions, is
/// transient: retried with exponential backoff up to a bounded number of attempts.
/// </summary>
public static class FailureClassifier
{
    public static FailureKind ClassifyConsumerFailure(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException!)
        {
            switch (e)
            {
                case JsonException:
                case FormatException:
                case ArgumentException:           // includes ArgumentNullException
                case InvalidCastException:
                case NotSupportedException:
                case KeyNotFoundException:
                case NotFoundException:
                case Pawzaroo.Shared.Exceptions.ValidationException:
                case ConflictException:
                case ForbiddenException:
                    return FailureKind.Permanent;
            }
            if (e.InnerException is null) break;
        }
        return FailureKind.Transient;
    }

    /// <summary>Producer-side: broker errors that no retry will fix (outbox poison).</summary>
    public static FailureKind ClassifyProducerFailure(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException!)
        {
            if (e is ArgumentException or JsonException) return FailureKind.Permanent;
            if (e is KafkaException ke)
            {
                switch (ke.Error.Code)
                {
                    case ErrorCode.MsgSizeTooLarge:
                    case ErrorCode.InvalidMsgSize:
                    case ErrorCode.Local_KeySerialization:
                    case ErrorCode.Local_ValueSerialization:
                    case ErrorCode.TopicAuthorizationFailed:
                    case ErrorCode.ClusterAuthorizationFailed:
                    case ErrorCode.TopicException:
                        return FailureKind.Permanent;
                }
            }
            if (e.InnerException is null) break;
        }
        return FailureKind.Transient;
    }
}

/// <summary>Exponential backoff with bounded jitter for the outbox dispatcher (NextAttemptAt).</summary>
public static class OutboxBackoff
{
    public static readonly TimeSpan Base = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan Max = TimeSpan.FromMinutes(5);
    public const double JitterFraction = 0.2;

    /// <param name="attempts">Failed attempts so far (1 = first failure).</param>
    /// <param name="jitter01">Random in [0,1]; 0.5 = no jitter.</param>
    public static TimeSpan Delay(int attempts, double jitter01 = 0.5)
    {
        if (attempts < 1) attempts = 1;
        var exp = attempts >= 20 ? Max.TotalSeconds : Base.TotalSeconds * Math.Pow(2, attempts - 1);
        var factor = 1 + (Math.Clamp(jitter01, 0, 1) * 2 - 1) * JitterFraction;
        return TimeSpan.FromSeconds(Math.Min(Max.TotalSeconds, exp * factor));
    }

    public static DateTime NextAttemptAt(DateTime nowUtc, int attempts, double jitter01 = 0.5)
        => nowUtc + Delay(attempts, jitter01);
}

/// <summary>Consumer handler retry backoff: base * 2^attempt (+ jitter), capped.</summary>
public static class ConsumerBackoff
{
    public static readonly TimeSpan Cap = TimeSpan.FromSeconds(30);

    /// <param name="attemptIndex">0-based index of the attempt that just failed.</param>
    public static TimeSpan Delay(int baseDelayMs, int attemptIndex, int jitterMs = 0)
    {
        var ms = baseDelayMs * Math.Pow(2, Math.Max(0, Math.Min(attemptIndex, 30))) + Math.Max(0, jitterMs);
        return TimeSpan.FromMilliseconds(Math.Min(Cap.TotalMilliseconds, ms));
    }
}
