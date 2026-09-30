#!/usr/bin/env bash
# WediPlan — backup SLIKA pružatelja (Zadatak 12, PLAN-PRIORITETI-LANSIRANJE-3.md).
# backup.sh pokriva samo bazu; fotografije su izvan baze (R2 bucket ili lokalni folder) pa trebaju svoj backup.
#
# Uporaba (cron, jednom dnevno, nakon backupa baze):
#   30 3 * * *  /opt/wediplan/ops/backup-photos.sh >> /var/log/wediplan-photos-backup.log 2>&1
#
# Kako radi (rclone sync + arhiva):
#   IZVOR  →  ${PHOTOS_BACKUP_REMOTE}/current/           (zrcalo trenutnog stanja)
#   Datoteke koje su u međuvremenu OBRISANE ili PREPISANE na izvoru ne nestaju iz backupa, nego se presele u
#             ${PHOTOS_BACKUP_REMOTE}/archive/YYYY-MM-DD/   (rclone --backup-dir)
#   → slučajno brisanje (ili napad) na izvoru NE briše i backup. Arhivu čisti lifecycle pravilo na bucketu
#     (preporuka 30 dana; uskladi s politikom privatnosti — obrisana fotografija ostaje u arhivi toliko dana).
#   Dodatna zaštita: --max-delete (default 100) ograničava broj brisanja po prolazu — kad se dosegne, rclone STANE i
#   završi s greškom (exit 7). Do tog trenutka pomaknute datoteke su ionako u arhivi; ostatak se nastavlja sljedeći
#   prolaz nakon provjere zašto je toliko obrisano (ili nakon privremenog podizanja PHOTOS_MAX_DELETE).
#
# Env varijable:
#   PHOTOS_MODE            "r2" (Cloudflare R2 bucket) ili "local" (LocalPhotoStorage, wwwroot/uploads)   [obavezno]
#   PHOTOS_BACKUP_REMOTE   odredište, rclone remote:bucket/putanja, npr. r2-backup:wediplan-media-backup   [obavezno]
#                          Mora biti ODVOJEN od izvora (drugi bucket ili drugi račun/dobavljač).
#   PHOTOS_R2_REMOTE       (mode=r2) izvor, npr. r2:wediplan-media  (bucket iz Storage__R2__Bucket)
#   PHOTOS_LOCAL_DIR       (mode=local) izvorni folder, npr. /opt/wediplan/api/wwwroot/uploads
#   PHOTOS_MAX_DELETE      najviše brisanja po prolazu (default 100); za namjerno masovno čišćenje privremeno povećaj
#   DRY_RUN                =1 → samo ispiši što bi se napravilo (rclone --dry-run)
#
# Izlazni kod: 0 = OK · ≠ 0 = greška (cron/monitoring to mora vidjeti).
set -euo pipefail

log() { echo "[$(date -Is)] $*"; }
die() { log "GREŠKA: $*" >&2; exit 1; }

command -v rclone >/dev/null 2>&1 || die "rclone nije instaliran"

MODE="${PHOTOS_MODE:-}"
DEST="${PHOTOS_BACKUP_REMOTE:-}"
[[ -n "$MODE" ]] || die "postavi PHOTOS_MODE=r2 ili PHOTOS_MODE=local"
[[ -n "$DEST" ]] || die "postavi PHOTOS_BACKUP_REMOTE (npr. r2-backup:wediplan-media-backup)"
DEST="${DEST%/}"

case "$MODE" in
  r2)
    SRC="${PHOTOS_R2_REMOTE:-}"
    [[ -n "$SRC" ]] || die "mode=r2: postavi PHOTOS_R2_REMOTE (npr. r2:wediplan-media)"
    ;;
  local)
    SRC="${PHOTOS_LOCAL_DIR:-}"
    [[ -n "$SRC" ]] || die "mode=local: postavi PHOTOS_LOCAL_DIR (npr. /opt/wediplan/api/wwwroot/uploads)"
    [[ -d "$SRC" ]] || die "PHOTOS_LOCAL_DIR ne postoji ili nije folder: $SRC (pogrešna putanja bi izgledala kao 'sve obrisano')"
    ;;
  *) die "PHOTOS_MODE mora biti 'r2' ili 'local' (dobio: '$MODE')" ;;
esac
SRC="${SRC%/}"

# Izvor i odredište ne smiju se preklapati (sync bi inače uništio izvor)
[[ "$SRC" != "$DEST" && "$DEST" != "$SRC"/* && "$SRC" != "$DEST"/* ]] \
  || die "izvor i odredište se preklapaju ($SRC ↔ $DEST) — odredište mora biti odvojeno"

MAX_DELETE="${PHOTOS_MAX_DELETE:-100}"
[[ "$MAX_DELETE" =~ ^[0-9]+$ ]] || die "PHOTOS_MAX_DELETE mora biti cijeli broj"

DAY="$(date +%F)"
ARGS=(sync "$SRC" "$DEST/current" --backup-dir "$DEST/archive/$DAY" --max-delete "$MAX_DELETE" --fast-list)
LABEL=""
if [[ "${DRY_RUN:-0}" == "1" ]]; then ARGS+=(--dry-run); LABEL=" [DRY RUN]"; fi

log "slike (${MODE}): ${SRC} → ${DEST}/current  (izmijenjeno/obrisano → ${DEST}/archive/${DAY})${LABEL}"
rclone "${ARGS[@]}" --stats-one-line --stats 0 -v 2>&1 | sed 's/^/  rclone: /'
# (pipeline ima pipefail → pad rclone-a vrati grešku iako je izlaz proslijeđen kroz sed)

log "gotovo."
