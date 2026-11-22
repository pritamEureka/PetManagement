# Restore drill (quarterly)

Goal: prove the latest backup restores and the data is sane, without touching production.

Checklist
- [ ] Pick the newest dump (PVC `postgres-backups` or the S3 copy); note its timestamp (data age = achieved RPO).
- [ ] Copy it to the drill machine: `kubectl cp <ns>/<pod>:/backups/<file> ./latest.dump` (or `aws s3 cp`).
- [ ] Run `scripts/restore-drill.sh ./latest.dump` (needs Docker; starts a throwaway `postgres:16-alpine`, restores,
      runs sanity counts, removes the container). Exit code 0 means pass.
- [ ] Compare the printed counts with the previous drill (users, pets, orders). Unexpected zeros mean fail.
- [ ] Optional: point a staging API at the scratch DB and sign in with a demo account.
- [ ] Record date, dump age, restore duration (RTO), result and follow-ups in the ops log.
- [ ] Verify object storage: restore one previous object version and open it.
