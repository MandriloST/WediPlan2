#!/usr/bin/env bash
# WediPlan — dnevni backup PostgreSQL baze (Faza 5, §8).
# Uporaba: pokreni iz crona/systemd timera jednom dnevno, npr.:
#   0 3 * * *  /opt/wediplan/ops/backup.sh >> /var/log/wediplan-backup.log 2>&1
#
# Env varijable (postavi u /etc/wediplan-backup.env ili u crontabu):
#   WEDIPLAN_DB   — connection string ILI koristi PG* varijable ispod
#   PGHOST, PGPORT, PGUSER, PGPASSWORD, PGDATABASE
#   BACKUP_DIR    — odredišni folder (default: /var/backups/wediplan)
#   KEEP_DAYS     — koliko dana čuvati (default: 14)
set -euo pipefail

BACKUP_DIR="${BACKUP_DIR:-/var/backups/wediplan}"
KEEP_DAYS="${KEEP_DAYS:-14}"
STAMP="$(date +%Y%m%d-%H%M%S)"
OUT="${BACKUP_DIR}/wediplan-${STAMP}.sql.gz"

mkdir -p "$BACKUP_DIR"

echo "[$(date -Is)] backup → ${OUT}"
# -Fc bi dao custom format; ovdje plain + gzip radi lakšeg pregleda i restore-a.
if [[ -n "${WEDIPLAN_DB:-}" ]]; then
  pg_dump "$WEDIPLAN_DB" | gzip -9 > "$OUT"
else
  pg_dump | gzip -9 > "$OUT"   # koristi PGHOST/PGUSER/PGDATABASE iz env-a
fi

# Rotacija: obriši starije od KEEP_DAYS
find "$BACKUP_DIR" -name 'wediplan-*.sql.gz' -mtime "+${KEEP_DAYS}" -delete

echo "[$(date -Is)] gotovo. Trenutni backupi:"
ls -lh "$BACKUP_DIR"/wediplan-*.sql.gz | tail -5

# Restore (podsjetnik):
#   gunzip -c wediplan-YYYYMMDD-HHMMSS.sql.gz | psql "$WEDIPLAN_DB"
#
# PREPORUKA: povremeno kopiraj backupe izvan servera (npr. u isti R2 bucket, odvojeni prefix,
# ili rclone na drugu lokaciju) — backup na istom disku ne štiti od gubitka diska.
