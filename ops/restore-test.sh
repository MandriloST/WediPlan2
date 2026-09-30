#!/usr/bin/env bash
# WediPlan — provjera da se backup stvarno može vratiti (Zadatak 12, PLAN-PRIORITETI-LANSIRANJE-3.md).
# "Backup koji nikad nije vraćen nije backup." Ova skripta vraća backup u PRIVREMENU bazu, provjeri sadržaj
# i obriše je. Produkcijska baza se NE dira.
#
# Uporaba:
#   ops/restore-test.sh /var/backups/wediplan/wediplan-20260930-030001.dump.age
#   ops/restore-test.sh ./wediplan-20260930-1530.dump
#
# Veza na PostgreSQL server: standardne libpq varijable PGHOST, PGPORT, PGUSER, PGPASSWORD
# (korisnik mora smjeti CREATEDB; za produkcijski server koristi korisnika s tim pravom, ne aplikacijskog).
# Skripta se spaja na bazu "postgres" radi kreiranja/brisanja privremene baze (PGDATABASE se ignorira).
#
# Ostale varijable:
#   BACKUP_AGE_IDENTITY   putanja do PRIVATNOG age ključa — obavezno za .age datoteke.
#                         Privatni ključ NE drži na produkcijskom serveru (v. DEPLOY.md §4).
#   RESTORE_MIN_VENDORS   najmanji očekivani broj redaka u "vendors" (default 1; 0 isključuje provjeru)
#   KEEP_TEST_DB          =1 → ne briši privremenu bazu nakon testa (za ručni pregled; ispiše njeno ime)
#
# Izlazni kod: 0 = backup je ispravan · ≠ 0 = nešto nije u redu (poruka objašnjava što).
set -euo pipefail

log() { echo "[$(date -Is)] $*"; }
die() { log "GREŠKA: $*" >&2; exit 1; }

FILE="${1:-}"
[[ -n "$FILE" ]] || die "Uporaba: $0 <datoteka .dump | .dump.age>"
[[ -f "$FILE" ]] || die "datoteka ne postoji: $FILE"
[[ -s "$FILE" ]] || die "datoteka je prazna: $FILE"

for c in psql pg_restore createdb dropdb; do
  command -v "$c" >/dev/null 2>&1 || die "$c nije instaliran (apt install postgresql-client)"
done

ENCRYPTED=0
case "$FILE" in
  *.age)
    ENCRYPTED=1
    command -v age >/dev/null 2>&1 || die "age nije instaliran (apt install age)"
    [[ -n "${BACKUP_AGE_IDENTITY:-}" ]] || die "za .age datoteku postavi BACKUP_AGE_IDENTITY (putanja do privatnog age ključa)"
    [[ -f "$BACKUP_AGE_IDENTITY" ]] || die "BACKUP_AGE_IDENTITY ne postoji: $BACKUP_AGE_IDENTITY"
    ;;
  *.dump) ;;
  *) die "nepoznata ekstenzija (očekujem .dump ili .dump.age): $FILE" ;;
esac

MIN_VENDORS="${RESTORE_MIN_VENDORS:-1}"
[[ "$MIN_VENDORS" =~ ^[0-9]+$ ]] || die "RESTORE_MIN_VENDORS mora biti cijeli broj"

TESTDB="wediplan_restore_test_$(date +%Y%m%d%H%M%S)"
export PGDATABASE=postgres   # admin veza (createdb/dropdb); restore ide u -d "$TESTDB"

created=0
cleanup() {
  local rc=$?
  if [[ $created -eq 1 ]]; then
    if [[ "${KEEP_TEST_DB:-0}" == "1" ]]; then
      log "KEEP_TEST_DB=1 — privremena baza ostaje: $TESTDB (obriši ručno: dropdb $TESTDB)"
    else
      if dropdb --if-exists "$TESTDB" >/dev/null 2>&1; then
        log "privremena baza obrisana: $TESTDB"
      else
        log "UPOZORENJE: nisam uspio obrisati $TESTDB — obriši ručno: dropdb $TESTDB" >&2
      fi
    fi
  fi
  exit $rc
}
trap cleanup EXIT

log "kreiram privremenu bazu ${TESTDB}"
createdb "$TESTDB"
created=1

log "restore ${FILE} → ${TESTDB}"
RESTORE_ARGS=(--no-owner --no-privileges --exit-on-error -d "$TESTDB")
do_restore() {
  if [[ $ENCRYPTED -eq 1 ]]; then
    # dekriptirano ide ravno u pg_restore (cjevovod) — nekriptirani podaci se ne zapisuju na disk
    age -d -i "$BACKUP_AGE_IDENTITY" "$FILE" | pg_restore "${RESTORE_ARGS[@]}"
  else
    pg_restore "${RESTORE_ARGS[@]}" "$FILE"
  fi
}
if ! do_restore; then
  die "restore nije uspio: ${FILE} (za .age provjeri da je BACKUP_AGE_IDENTITY ključ koji odgovara javnom ključu kojim je backup šifriran; inače je datoteka oštećena)"
fi

q() { psql -d "$TESTDB" -X -A -t -v ON_ERROR_STOP=1 -c "$1"; }

log "provjera sadržaja:"
fail=0
for t in vendors users user_reviews vendor_photos; do
  if n="$(q "select count(*) from ${t};" 2>&1)"; then
    printf '  %-16s %s\n' "$t" "$n"
    if [[ "$t" == "vendors" && "$n" -lt "$MIN_VENDORS" ]]; then
      log "GREŠKA: vendors ima $n redaka (očekivano ≥ $MIN_VENDORS)" >&2
      fail=1
    fi
  else
    log "GREŠKA: tablica ${t} nije čitljiva nakon restorea: ${n}" >&2
    fail=1
  fi
done

if last="$(q 'select "MigrationId" from "__EFMigrationsHistory" order by "MigrationId" desc limit 1;' 2>&1)" && [[ -n "$last" ]]; then
  printf '  %-16s %s\n' "zadnja migracija" "$last"
else
  log "GREŠKA: __EFMigrationsHistory prazna ili nečitljiva: ${last:-}" >&2
  fail=1
fi

[[ $fail -eq 0 ]] || die "restore-test NIJE prošao: ${FILE}"
log "OK: backup je ispravan i vraća se bez grešaka (${FILE})"
