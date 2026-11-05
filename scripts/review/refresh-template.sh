#!/usr/bin/env bash
# Builds the pseudonymized template that new review environments start from, from the production
# database. Run it as root on the server whenever the review data should be fresher (in tmux or screen,
# so a lost SSH connection does not stop it halfway):
#
#   refresh-template.sh <owner email>
#
# <owner email> is your own account in production. It keeps its email address so you can log in as
# yourself. Every account gets the review password REVIEW_LOGIN_PASSWORD, never a real password.
#
# How the real data is kept away from the review environments:
#   1. pg_dump of production goes straight into a throwaway PostgreSQL container without any network
#      and with its data in memory (tmpfs). It is never written to a file or disk.
#   2. pseudonymize.sql runs there.
#   3. Only the pseudonymized result is dumped into the review database server. A fresh restore holds no
#      old row versions, so the review databases cannot contain the replaced values, not even on disk.
#   4. The throwaway container is removed, also when something fails.
# Existing review environments keep their databases; reopen a pull request to start it from the new template.

set -euo pipefail
script_dir=$(dirname "$(readlink -f "$0")")
source "$script_dir/lib.sh"

owner_email=${1:-}
[[ $owner_email =~ ^[^[:space:]\'\"]+@[^[:space:]\'\"]+$ ]] || die "usage: refresh-template.sh <owner email>"

load_config
for name in REVIEW_LOGIN_PASSWORD PRODUCTION_DB_CONTAINER PRODUCTION_DB_NAME PRODUCTION_DB_USER; do
    [[ -n ${!name:-} && ${!name} != *replace-me* ]] || die "$name is not set in $REVIEW_CONFIG"
done
command -v python3 >/dev/null || die "python3 is needed to hash the review password"
take_lock

new_db="${REVIEW_TEMPLATE_DB}_new"
finished=false
cleanup() {
    docker rm -f -v "$REVIEW_SCRUB" >/dev/null 2>&1 || true
    if [[ $finished == false ]]; then
        echo "Failed, removing the unfinished copy. The previous template is unchanged." >&2
        drop_database "$new_db" >/dev/null 2>&1 || true
    fi
}
trap cleanup EXIT

# The review password in the format of ASP.NET Core Identity (version 3: PBKDF2 with HMAC-SHA512)
password_hash=$(PASSWORD="$REVIEW_LOGIN_PASSWORD" python3 -c '
import base64, hashlib, os, struct
salt, iterations = os.urandom(16), 100000
subkey = hashlib.pbkdf2_hmac("sha512", os.environ["PASSWORD"].encode(), salt, iterations, 32)
print(base64.b64encode(b"\x01" + struct.pack(">III", 2, iterations, len(salt)) + salt + subkey).decode())')

echo "Starting the throwaway database"
docker rm -f -v "$REVIEW_SCRUB" >/dev/null 2>&1 || true
docker run -d --name "$REVIEW_SCRUB" --network none \
    --tmpfs /var/lib/postgresql/data \
    -e POSTGRES_HOST_AUTH_METHOD=trust \
    "$POSTGRES_IMAGE" >/dev/null
# During its first start the image runs a temporary server on the socket only, so wait for TCP
for _ in $(seq 1 60); do
    docker exec "$REVIEW_SCRUB" pg_isready -q -h 127.0.0.1 -U postgres && break
    sleep 1
done
docker exec "$REVIEW_SCRUB" pg_isready -q -h 127.0.0.1 -U postgres || die "the throwaway database did not start"
docker exec "$REVIEW_SCRUB" createdb -h 127.0.0.1 -U postgres scrub

echo "Copying the production database"
# Hangfire's tables are left out: old jobs carry real email addresses and the app recreates them.
docker exec "$PRODUCTION_DB_CONTAINER" \
    pg_dump -U "$PRODUCTION_DB_USER" -d "$PRODUCTION_DB_NAME" --format=custom --no-owner --no-acl --exclude-schema=hangfire \
  | docker exec -i "$REVIEW_SCRUB" pg_restore -h 127.0.0.1 -U postgres -d scrub --no-owner --no-acl --exit-on-error

echo "Pseudonymizing"
docker exec -i "$REVIEW_SCRUB" psql -h 127.0.0.1 -U postgres -d scrub -v ON_ERROR_STOP=1 -q \
    -v "owner_email=$owner_email" -v "password_hash=$password_hash" < "$script_dir/pseudonymize.sql"

echo "Copying the pseudonymized data into the review database server"
role_exists "$REVIEW_TEMPLATE_OWNER" || review_psql -c "create role \"$REVIEW_TEMPLATE_OWNER\" nologin;"
drop_database "$new_db"
review_psql -c "create database \"$new_db\";"
review_psql -c "revoke connect, temporary on database \"$new_db\" from public;"
review_psql -c "grant create on database \"$new_db\" to \"$REVIEW_TEMPLATE_OWNER\";"
review_psql -d "$new_db" -c "grant create on schema public to \"$REVIEW_TEMPLATE_OWNER\";"
docker exec "$REVIEW_SCRUB" pg_dump -h 127.0.0.1 -U postgres -d scrub --format=custom --no-owner --no-acl \
  | docker exec -i "$REVIEW_POSTGRES" pg_restore -U postgres -d "$new_db" --no-owner --no-acl \
        --role="$REVIEW_TEMPLATE_OWNER" --exit-on-error

echo "Replacing the template"
finished=true
if database_exists "$REVIEW_TEMPLATE_DB"; then
    review_psql -c "alter database \"$REVIEW_TEMPLATE_DB\" is_template false;"
    drop_database "$REVIEW_TEMPLATE_DB"
fi
review_psql -c "alter database \"$new_db\" rename to \"$REVIEW_TEMPLATE_DB\";"
# Nobody may connect to the template, otherwise copying it fails
review_psql -c "alter database \"$REVIEW_TEMPLATE_DB\" is_template true allow_connections false;"

echo "Done. New review environments start from the refreshed template."
