#!/usr/bin/env bash
# Restore a pg_dump (custom format) into a scratch Postgres container and run sanity checks.
# Non-destructive: only creates/removes its own container. Usage: scripts/restore-drill.sh path/to/file.dump
set -euo pipefail

DUMP="${1:?usage: $0 <dump-file>}"
[ -f "$DUMP" ] || { echo "dump not found: $DUMP" >&2; exit 2; }

NAME="pawzaroo-restore-drill-$$"
IMAGE="${PG_IMAGE:-postgres:16-alpine}"
DB=pawzaroo_drill
cleanup() { docker rm -f "$NAME" >/dev/null 2>&1 || true; }
trap cleanup EXIT

echo "==> starting scratch $IMAGE"
docker run -d --name "$NAME" -e POSTGRES_PASSWORD=drill -e POSTGRES_DB="$DB" "$IMAGE" >/dev/null
for _ in $(seq 1 60); do
  docker exec "$NAME" pg_isready -U postgres -d "$DB" >/dev/null 2>&1 && break
  sleep 1
done
docker exec "$NAME" pg_isready -U postgres -d "$DB" >/dev/null

echo "==> restoring $DUMP"
START=$(date +%s)
docker cp "$DUMP" "$NAME:/tmp/restore.dump"
docker exec "$NAME" pg_restore --no-owner --exit-on-error -U postgres -d "$DB" /tmp/restore.dump
echo "restore took $(( $(date +%s) - START ))s"

q() { docker exec "$NAME" psql -U postgres -d "$DB" -tA -c "$1"; }
fail=0
check() {  # check <label> <table> <must_be_positive:0|1>
  local n
  if ! n=$(q "SELECT COUNT(*) FROM $2" 2>/dev/null); then echo "FAIL  $1: table $2 missing"; fail=1; return; fi
  echo "count $1 ($2) = $n"
  if [ "$3" = 1 ] && [ "$n" -le 0 ]; then echo "FAIL  $1 is empty"; fail=1; fi
}

check users users 1
check pets pets 0
check appointments appointments 0
check orders orders 0
check migrations '"__EFMigrationsHistory"' 1

echo "latest migration: $(q 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1' || true)"
echo "orphan pets (no owner): $(q 'SELECT COUNT(*) FROM pets p LEFT JOIN users u ON u.id = p.owner_id WHERE u.id IS NULL' || echo n/a)"

if [ "$fail" -ne 0 ]; then echo "DRILL FAILED"; exit 1; fi
echo "DRILL PASSED"
