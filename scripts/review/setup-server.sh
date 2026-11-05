#!/usr/bin/env bash
# Prepares the server for review environments. Run as root after filling in /etc/clubnetz-review/review.env.
# Safe to run again, e.g. after changing the password prompt (it recreates the mail catcher).
#
#   setup-server.sh <file with the public SSH key of the GitHub workflow>
#
# Creates:
#   - the internal network clubnetz-review: no route to the host, the internet or Coolify's networks,
#     only the proxy is connected to it as well
#   - clubnetz-review-postgres: the database server of the review environments (no published port)
#   - clubnetz-review-mailpit: catches every email of the review environments, at https://mail.<REVIEW_DOMAIN>
#   - the user clubnetz-review: the workflow's SSH key may only run ssh-entry.sh, which may only run
#     review-env.sh as root (sudo rule for that one script)
#   - a cron job that puts the proxy back into the review network after Coolify restarted it

set -euo pipefail
script_dir=$(dirname "$(readlink -f "$0")")
source "$script_dir/lib.sh"

public_key_file=${1:-}
[[ -f $public_key_file ]] || die "usage: setup-server.sh <public key file>"
load_config
[[ $(stat -c %a "$REVIEW_CONFIG") == 600 ]] || die "$REVIEW_CONFIG must only be readable by root: chmod 600 $REVIEW_CONFIG"

# The scripts run as root, so only root may change them or any folder above them
check_root_only() {
    [[ $(stat -c %U "$1") == root ]] || die "$1 must belong to root"
    [[ -z $(find "$1" -maxdepth 0 -perm /022) ]] || die "$1 must not be writable by group or others"
}
dir=$script_dir
while [[ $dir != / ]]; do check_root_only "$dir"; dir=$(dirname "$dir"); done
for file in "$script_dir"/*.sh "$script_dir"/pseudonymize.sql; do check_root_only "$file"; done
chmod 755 "$script_dir"/*.sh

if ! docker network inspect "$REVIEW_NETWORK" >/dev/null 2>&1; then
    echo "Creating the network $REVIEW_NETWORK"
    docker network create --internal "$REVIEW_NETWORK" >/dev/null
fi
[[ $(docker network inspect -f '{{.Internal}}' "$REVIEW_NETWORK") == true ]] \
    || die "the network $REVIEW_NETWORK exists but is not internal. Remove it (docker network rm $REVIEW_NETWORK) and run this again."
ensure_proxy_in_network

if ! docker inspect "$REVIEW_POSTGRES" >/dev/null 2>&1; then
    echo "Starting $REVIEW_POSTGRES"
    docker run -d --name "$REVIEW_POSTGRES" \
        --network "$REVIEW_NETWORK" \
        --restart unless-stopped \
        --memory 1g \
        -e "POSTGRES_PASSWORD=$REVIEW_POSTGRES_PASSWORD" \
        -v clubnetz-review-postgres:/var/lib/postgresql/data \
        "$POSTGRES_IMAGE" >/dev/null
    for _ in $(seq 1 60); do
        docker exec "$REVIEW_POSTGRES" pg_isready -q -h 127.0.0.1 -U postgres && break
        sleep 1
    done
fi
# Each review environment has its own role and may only connect to its own database
review_psql -c "revoke connect, temporary on database postgres from public;"
review_psql -c "revoke connect, temporary on database template1 from public;"

echo "Starting $REVIEW_MAILPIT"
traefik_labels "$REVIEW_MAILPIT" "mail.$REVIEW_DOMAIN" 8025
docker rm -f "$REVIEW_MAILPIT" >/dev/null 2>&1 || true
docker run -d --name "$REVIEW_MAILPIT" \
    --network "$REVIEW_NETWORK" \
    --restart unless-stopped \
    --memory 256m \
    -e MP_MAX_MESSAGES=1000 \
    "${LABELS[@]}" \
    "$MAILPIT_IMAGE" >/dev/null

if ! id clubnetz-review >/dev/null 2>&1; then
    echo "Creating the user clubnetz-review"
    useradd --create-home --shell /bin/bash clubnetz-review
fi
install -d -m 700 -o clubnetz-review -g clubnetz-review /home/clubnetz-review/.ssh
key=$(head -n 1 "$public_key_file")
[[ $key == ssh-* ]] || die "$public_key_file does not look like a public SSH key"
echo "command=\"$script_dir/ssh-entry.sh\",restrict $key" > /home/clubnetz-review/.ssh/authorized_keys
chown clubnetz-review:clubnetz-review /home/clubnetz-review/.ssh/authorized_keys
chmod 600 /home/clubnetz-review/.ssh/authorized_keys

sudoers=/etc/sudoers.d/clubnetz-review
echo "clubnetz-review ALL=(root) NOPASSWD: $script_dir/review-env.sh" > "$sudoers.tmp"
visudo -cf "$sudoers.tmp" >/dev/null || { rm -f "$sudoers.tmp"; die "the sudo rule is invalid"; }
chmod 440 "$sudoers.tmp"
mv "$sudoers.tmp" "$sudoers"

if [[ -d /etc/cron.d ]]; then
    echo "*/5 * * * * root $script_dir/review-env.sh reconnect-proxy >/dev/null" > /etc/cron.d/clubnetz-review
    chmod 644 /etc/cron.d/clubnetz-review
fi

echo "Done. Next: refresh-template.sh <your email>, then open a pull request."
