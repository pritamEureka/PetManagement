# Backup and restore runbook

## PostgreSQL
- **Managed (production)**: enable automated backups plus PITR (RDS / Cloud SQL / Azure). Retention of at least 14 days;
  cross-region snapshot copy for DR. Prefer this over anything below.
- **In-cluster logical backup**: `deploy/k8s/base/backup.cronjob.yaml` (`pg_dump --format=custom`, suspended by default).
  Enable with `kubectl -n <ns> patch cronjob postgres-backup -p '{"spec":{"suspend":false}}'` or an overlay patch.
  Set `BACKUP_S3_URI` for an off-cluster copy (the stock postgres image has no aws cli; use an image that has both).
  RPO equals the schedule interval (24h).
- **Lower RPO (self-managed)**: base backup plus WAL archiving: `archive_mode=on`, `archive_command` using wal-g or
  pgBackRest, nightly base backups, restore with `recovery_target_time`. CloudNativePG is the recommended operator.
- **Restore from dump**: `pg_restore --clean --if-exists --no-owner -d <db> <file>` into a NEW database first and swap
  only after `scripts/restore-drill.sh` style checks pass. Then run the migration job so the schema is current.

## Object storage (MinIO / S3)
Enable bucket versioning and a lifecycle rule (expire noncurrent versions after 30-90 days). Production: S3 cross-region
replication (self-hosted: `mc mirror` or MinIO site replication). Test restores by fetching a prior object version.

## Kafka
Events are derived state: the transactional outbox in Postgres is the source of truth, so a lost topic can be rebuilt by
re-dispatching outbox rows. Still set retention of at least 7 days, replication factor 3 and `min.insync.replicas=2` in
production, and use MirrorMaker 2 to a DR cluster if consumers must resume mid-stream. Dead letters are stored in the
database and survive broker loss.

## Redis
Used for cache and counters; loss is tolerated (degraded mode). If persistence is wanted enable AOF
(`appendfsync everysec`) on a PVC. No backup is required for correctness.

## Targets
RPO 24h with dumps (minutes with WAL archiving); RTO 1-2h. Drill quarterly: docs/runbooks/restore-drill.md.
