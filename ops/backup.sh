#!/usr/bin/env bash
# WediPlan — backup PostgreSQL baze (Faza 5 §8; očvršćeno u Zadatku 12, PLAN-PRIORITETI-LANSIRANJE-3.md).
#
# Što radi:
#   1. pg_dump u custom formatu (-Fc)  →  wediplan-YYYYMMDD-HHMMSS.dump
#   2. (preporučeno) enkripcija s `age`  →  wediplan-YYYYMMDD-HHMMSS.dump.age   (backup sadrži OSOBNE PODATKE)
#   3. (preporučeno) kopija izvan servera preko `rclone`  (backup na istom disku ne štiti od gubitka diska)
#   4. lokalna rotacija (KEEP_DAYS)
#
# Uporaba (cron/systemd timer, jednom dnevno):
#   0 3 * * *  /opt/wediplan/ops/backup.sh >> /var/log/wediplan-backup.log 2>&1
#
# Env varijable (npr. u /etc/wediplan-backup.env, chmod 600, `set -a; . /etc/wediplan-backup.env; set +a`):
#   Veza na bazu (jedno od):
#     WEDIPLAN_DB            libpq connection string: URI  postgresql://user:pass@host:5432/wediplan
#                            ILI  "host=… port=… dbname=… user=… password=…".
#                            ⚠ NE Npgsql format ("Host=…;Port=…;…") kojim .NET aplikacija čita bazu — pg_dump ga ne razumije.
#     PGHOST, PGPORT, PGUSER, PGPASSWORD, PGDATABASE   (standardne libpq varijable)
#   Odredište i rotacija:
#     BACKUP_DIR             lokalni folder (default: /var/backups/wediplan)
#     KEEP_DAYS              koliko dana čuvati lokalno (default: 14)
#   Enkripcija (age, https://github.com/FiloSottile/age):
#     BACKUP_AGE_RECIPIENT   javni ključ "age1…"; više ključeva odvoji razmakom ili zarezom
#                            (preporuka: 2 ključa — radni i rezervni). Bez ovoga backup NIJE enkriptiran (upozorenje).
#   Off-site kopija (rclone):
#     BACKUP_RCLONE_REMOTE   npr. r2-backup:wediplan-backups/db   (remote:bucket/putanja iz `rclone config`)
#                            Ako upload padne, skripta završava s greškom (exit 2), a lokalni backup ostaje.
#
# Izlazni kodovi: 0 = sve OK · 1 = greška (dump/enkripcija/konfiguracija) · 2 = lokalni backup OK, upload NIJE uspio.
#
# Restore i provjera: ops/restore-test.sh <datoteka>   ·   ručni restore: v. DEPLOY.md §4.
# Retencija izvan servera: postavi lifecycle pravilo na bucketu (isti rok kao KEEP_DAYS) — skripta tamo ništa ne briše.
# Za upload koristi ZASEBAN token ograničen samo na backup bucket (ne aplikacijski token), v. DEPLOY.md §4.
set -euo pipefail

log() { echo "[$(date -Is)] $*"; }
die() { log "GREŠKA: $*" >&2; exit 1; }

BACKUP_DIR="${BACKUP_DIR:-/var/backups/wediplan}"
KEEP_DAYS="${KEEP_DAYS:-14}"
[[ "$KEEP_DAYS" =~ ^[0-9]+$ ]] || die "KEEP_DAYS mora biti cijeli broj (dobio: '$KEEP_DAYS')"

command -v pg_dump >/dev/null 2>&1 || die "pg_dump nije instaliran (apt install postgresql-client)"

# Povjerljivo: samo vlasnik smije čitati backupe
umask 077
mkdir -p "$BACKUP_DIR"
chmod 700 "$BACKUP_DIR"

# Ne dopuštaj dva istodobna backupa (npr. prethodni još traje)
if command -v flock >/dev/null 2>&1; then
  exec 9>"$BACKUP_DIR/.backup.lock"
  flock -n 9 || die "drugi backup je već u tijeku (lock: $BACKUP_DIR/.backup.lock)"
fi

# Veza na bazu
DUMP_ARGS=(-Fc)
if [[ -n "${WEDIPLAN_DB:-}" ]]; then
  if [[ "$WEDIPLAN_DB" == *";"* ]]; then
    die "WEDIPLAN_DB izgleda kao Npgsql connection string (sadrži ';'). pg_dump traži libpq format: URI (postgresql://…) ili 'host=… dbname=… user=… password=…', ili koristi PGHOST/PGUSER/PGDATABASE."
  fi
  DUMP_ARGS+=(-d "$WEDIPLAN_DB")
fi

# Enkripcija
AGE_ARGS=()
if [[ -n "${BACKUP_AGE_RECIPIENT:-}" ]]; then
  command -v age >/dev/null 2>&1 || die "BACKUP_AGE_RECIPIENT je postavljen, ali 'age' nije instaliran (apt install age)"
  # razmaci ili zarezi → više -r argumenata
  IFS=$' \t,' read -r -a _recipients <<< "$BACKUP_AGE_RECIPIENT"
  for r in "${_recipients[@]}"; do
    [[ -n "$r" ]] || continue
    [[ "$r" == age1* ]] || die "BACKUP_AGE_RECIPIENT: '$r' nije age javni ključ (mora počinjati s 'age1')"
    AGE_ARGS+=(-r "$r")
  done
  [[ ${#AGE_ARGS[@]} -gt 0 ]] || die "BACKUP_AGE_RECIPIENT je prazan"
  EXT="dump.age"
else
  log "UPOZORENJE: BACKUP_AGE_RECIPIENT nije postavljen — backup NIJE enkriptiran, a sadrži osobne podatke (GDPR). Postavi age ključ."
  EXT="dump"
fi

if [[ -n "${BACKUP_RCLONE_REMOTE:-}" ]]; then
  command -v rclone >/dev/null 2>&1 || die "BACKUP_RCLONE_REMOTE je postavljen, ali 'rclone' nije instaliran"
else
  log "UPOZORENJE: BACKUP_RCLONE_REMOTE nije postavljen — backup ostaje SAMO na ovom serveru (ne štiti od gubitka diska)."
fi

STAMP="$(date +%Y%m%d-%H%M%S)"
OUT="${BACKUP_DIR}/wediplan-${STAMP}.${EXT}"
TMP="${OUT}.partial"
trap 'rm -f "$TMP"' EXIT   # nepotpun backup nikad ne ostaje pod pravim imenom

log "backup → ${OUT}"
# pipefail: pad pg_dump-a (ili age-a) obara cijeli pipeline
if [[ ${#AGE_ARGS[@]} -gt 0 ]]; then
  pg_dump "${DUMP_ARGS[@]}" | age "${AGE_ARGS[@]}" > "$TMP"
else
  pg_dump "${DUMP_ARGS[@]}" > "$TMP"
fi

[[ -s "$TMP" ]] || die "backup je prazan — provjeri vezu na bazu"

# Brza provjera čitljivosti (samo za nekriptirani; enkriptirani se provjerava restore-testom)
if [[ ${#AGE_ARGS[@]} -eq 0 ]] && command -v pg_restore >/dev/null 2>&1; then
  pg_restore --list "$TMP" >/dev/null || die "pg_restore ne može pročitati tek napravljeni backup"
fi

mv "$TMP" "$OUT"
log "OK: $(du -h "$OUT" | cut -f1)  ${OUT}"

# Off-site kopija
UPLOAD_FAILED=0
if [[ -n "${BACKUP_RCLONE_REMOTE:-}" ]]; then
  log "rclone copy → ${BACKUP_RCLONE_REMOTE}"
  if rclone copy "$OUT" "$BACKUP_RCLONE_REMOTE" --immutable; then
    log "off-site kopija OK"
  else
    UPLOAD_FAILED=1
    log "GREŠKA: upload na ${BACKUP_RCLONE_REMOTE} nije uspio (lokalni backup postoji)" >&2
  fi
fi

# Lokalna rotacija (samo gotove datoteke; .partial/.lock se ne diraju). Uključuje i stari format
# (wediplan-*.sql.gz, plain SQL iz prve verzije skripte) da stari backupi s osobnim podacima ne ostanu zauvijek.
find "$BACKUP_DIR" -maxdepth 1 -type f \
  \( -name 'wediplan-*.dump' -o -name 'wediplan-*.dump.age' -o -name 'wediplan-*.sql.gz' \) \
  -mtime "+${KEEP_DAYS}" -print -delete | sed 's/^/obrisan (rotacija): /'

log "trenutni lokalni backupi:"
# shellcheck disable=SC2012  # imena su naša (wediplan-YYYYMMDD-HHMMSS.*), samo za ispis
ls -lh "$BACKUP_DIR"/wediplan-*.dump* 2>/dev/null | tail -5 || true

[[ $UPLOAD_FAILED -eq 0 ]] || exit 2
log "gotovo."

# Restore (podsjetnik): ops/restore-test.sh <datoteka>  ·  ručno:
#   nekriptirani:  pg_restore --no-owner --no-privileges -d <baza> wediplan-….dump
#   enkriptirani:  age -d -i ~/wediplan-backup.key wediplan-….dump.age | pg_restore --no-owner --no-privileges -d <baza>
