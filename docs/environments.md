# Environment separation

| Concern | dev | staging | production |
|---|---|---|---|
| Namespace | pawzaroo-dev | pawzaroo-staging | pawzaroo |
| Postgres/Kafka/MinIO | in-cluster | in-cluster or managed | managed (base StatefulSets scaled to 0 by the overlay) |
| Secrets | throwaway | secret manager (own paths) | secret manager (own paths) |
| Ingress host | overlay patch | overlay patch | overlay patch |

Per-environment settings live in `deploy/k8s/overlays/<env>/patches/` (configmap keys such as `STORAGE_BUCKET`,
`STORAGE_ENDPOINT`, `KAFKA_BROKERS`).

## Kafka topics - separable by prefix
`Kafka:TopicPrefix` (env `Kafka__TopicPrefix`, default empty = unchanged behaviour) is prepended to every topic AND consumer
group id on the wire. It is applied in exactly one helper, `KafkaTopicNaming` (Infrastructure/Messaging), used by the producer,
the raw/DLQ producer, the outbox dispatcher and `KafkaConsumerBase` (subscribe + group id + idempotency key). Code, config
and `DeadLetterRecord.Topic` keep LOGICAL names (handlers receive the logical topic); the DLQ topic is `<prefix><topic>.dlq`;
dead-letter redrive republishes the logical topic and the producer re-applies the prefix (a stored physical name with a
leading prefix is stripped first).

| Env | `Kafka__TopicPrefix` | Set in |
|---|---|---|
| dev | `dev-` | overlays/dev/patches/configmap.yaml |
| staging | `staging-` | overlays/staging/patches/configmap.yaml |
| production | empty (historical names) | overlays/production/patches/configmap.yaml |

The prefix reaches pods through `envFrom: pawzaroo-config`. Caveats: changing the prefix on an existing environment orphans
its old topics/offsets; the docker-compose topic bootstrap script still pre-creates only unprefixed names (set
`Kafka__TopicPrefix` empty there, or rely on broker auto-create); topic ACLs must cover the prefix.

## Storage
Use a distinct bucket per environment (the k8s overlays do NOT patch `STORAGE_BUCKET`: dev/staging have their own in-cluster MinIO per namespace, and production uses the base value; set it in the secret manager/overlay when sharing a managed store) (`STORAGE_BUCKET=pawzaroo-dev|-staging|-prod`) with separate access keys, and
enable versioning on production (see docs/runbooks/backup-restore.md).

## Image pinning
Base manifests reference `ghcr.io/your-org/pawzaroo-{api,worker,web}:0.0.0` (intentionally unpullable) and rely on the
kustomize `images:` transformer. Pipelines set the tag (`kustomize edit set image`); to pin by digest use
`newName: ghcr.io/<org>/pawzaroo-api` + `digest: sha256:...` instead of `newTag`. Digests cannot be resolved offline:
the release workflow now prints them (cosign/trivy steps), and `promote.yml` accepts a tag that is verified with
`cosign verify` before applying. Third-party images (postgres:16-alpine, redis:7.4-alpine, minio RELEASE tag,
bitnami/kafka:3.7.1, curlimages/curl:8.10.1) are already version-pinned; add digests the same way when mirroring them.

## Login geolocation (impossible-travel detection)
The detection rules live in `SecuritySignalService`/`SignalRules`. Geo data comes from a pluggable `IIpGeoLookup`:
- Default (`Security:Geo:Provider` unset): `NoOpIpGeoLookup` - no coordinates exist, so the speed rule (>900 km/h) can NEVER run.
  Only the country-change rule (different country within 1 h) runs, and only when the country is known from the legacy
  `Security:Geo:CountryHeader` (default `CF-IPCountry`) or a stored `UserDevice.IpCountry`.
- `Security:Geo:Provider=headers`: `HeaderIpGeoLookup` reads `CF-IPCountry` / `CloudFront-Viewer-Country` (country) and
  `X-Geo-Lat` + `X-Geo-Lon` (+ optional `X-Geo-City`/`CloudFront-Viewer-City`) forwarded by an edge proxy. The speed rule runs only
  when BOTH logins carried a lat/lon pair. Standard CDNs do not send lat/lon by default (Cloudflare needs a Managed Transform /
  Worker; CloudFront needs the `CloudFront-Viewer-Latitude/Longitude` headers mapped to `X-Geo-*`), so expect country-only
  detection unless you configure that. The edge MUST strip client-supplied `X-Geo-*`/`CF-IPCountry` headers, otherwise users can
  spoof or evade the rule.
- When a lookup succeeds at login, `UserDevice.IpCountry`/`IpCity` are populated (previously nothing ever set them).
