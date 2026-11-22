# Service level objectives

SLIs come from the `Pawzaroo` OpenTelemetry meter (see `Infrastructure/Observability/PawzarooTelemetry.cs`) and
ASP.NET Core / prometheus-net HTTP metrics. Windows are rolling 30 days. Alert rules: `deploy/prometheus-alerts.yml`.

| Area | SLI | SLO target |
|---|---|---|
| Booking | Share of booking requests (POST appointments) returning non-5xx | 99.9% |
| Booking | p95 latency of booking creation | < 800 ms |
| Checkout | Share of checkouts completing without 5xx (`checkout_completed{outcome="success"}` / attempts) | 99.9% |
| Checkout | p95 `checkout_duration_ms` | < 1500 ms |
| Notification | p95 `notification_latency_ms` (event occurred to notification persisted) | < 5 s |
| Notification | Share of notification events persisted (not dead-lettered) | 99.95% |
| Outbox | p99 `outbox_dispatch_latency_ms` | < 10 s |
| Outbox | `outbox_oldest_pending_age_seconds` | < 60 s for 99.5% of time; page at 300 s |
| Webhook | Valid signed webhooks accepted (`webhook_received` minus `webhook_rejected` for valid senders) | 99.9% |
| Webhook | p95 handler latency | < 500 ms |
| DLQ | `dlq_messages` rate | < 0.01% of consumed messages; every record triaged within 1 business day |

## Error budget policy
- Budget burn above 2x for 1 hour: investigate, freeze risky deploys.
- Budget exhausted: only reliability work until the SLI recovers.

## Notes
- Dead letters are visible and re-drivable at Admin, Dead letters. Permanent failures (validation, deserialization, not-found) go straight to the DLQ; transient failures retry with backoff first.
- Outbox rows end as `Failed` (retries exhausted) or `Poisoned` (permanent producer error); both need operator attention.
