#!/usr/bin/env bash
# Shared settings and helpers of the review environment scripts. Sourced, not run.
# shellcheck disable=SC2034  # the settings are used by the scripts that source this file

REVIEW_CONFIG=${REVIEW_CONFIG:-/etc/clubnetz-review/review.env}
REVIEW_NETWORK=clubnetz-review
REVIEW_POSTGRES=clubnetz-review-postgres
REVIEW_MAILPIT=clubnetz-review-mailpit
REVIEW_SCRUB=clubnetz-review-scrub
REVIEW_TEMPLATE_DB=review_template
# Owns the tables of the template. Each review database hands them to its own role.
REVIEW_TEMPLATE_OWNER=review_template_owner
POSTGRES_IMAGE=postgres:16
MAILPIT_IMAGE=axllent/mailpit:v1.33.0

die() { echo "Error: $*" >&2; exit 1; }

load_config() {
    [[ $EUID -eq 0 ]] || die "run as root"
    [[ -f $REVIEW_CONFIG ]] || die "$REVIEW_CONFIG is missing, see scripts/review/README.md"
    # shellcheck disable=SC1090
    source "$REVIEW_CONFIG"
    local name
    for name in REVIEW_DOMAIN REVIEW_IMAGE REVIEW_MAX_ENVIRONMENTS REVIEW_MEMORY_LIMIT REVIEW_BASIC_AUTH REVIEW_POSTGRES_PASSWORD \
                TRAEFIK_CONTAINER TRAEFIK_ENTRYPOINT_HTTP TRAEFIK_ENTRYPOINT_HTTPS TRAEFIK_CERT_RESOLVER; do
        [[ -n ${!name:-} && ${!name} != *replace-me* ]] || die "$name is not set in $REVIEW_CONFIG"
    done
}

# One script at a time: deployments, removals and template refreshes must not overlap
# (a cancelled workflow run can leave its script running on the server).
take_lock() {
    exec 9>/run/clubnetz-review.lock
    flock -w 900 9 || die "another review script has been running for 15 minutes, try again later"
}

random_password() {
    head -c 24 /dev/urandom | od -An -tx1 | tr -d ' \n'
}

# psql as superuser in the review database server
review_psql() {
    docker exec -i "$REVIEW_POSTGRES" psql -U postgres -v ON_ERROR_STOP=1 -q "$@"
}

database_exists() {
    [[ $(review_psql -At -c "select 1 from pg_database where datname = '$1'") == 1 ]]
}

role_exists() {
    [[ $(review_psql -At -c "select 1 from pg_roles where rolname = '$1'") == 1 ]]
}

drop_database() {
    review_psql -c "select pg_terminate_backend(pid) from pg_stat_activity where datname = '$1' and pid <> pg_backend_pid();" >/dev/null
    review_psql -c "drop database if exists \"$1\";"
}

# The proxy has to be in the review network to reach the review containers. Coolify does not know
# this network, so a restart of the proxy drops it; the cron job of setup-server.sh reconnects it.
ensure_proxy_in_network() {
    if ! docker inspect -f '{{json .NetworkSettings.Networks}}' "$TRAEFIK_CONTAINER" | grep -q "\"$REVIEW_NETWORK\""; then
        echo "Connecting $TRAEFIK_CONTAINER to $REVIEW_NETWORK"
        docker network connect "$REVIEW_NETWORK" "$TRAEFIK_CONTAINER"
    fi
}

# Docker labels that publish a container at https://<host> behind the username and password prompt.
# Usage: traefik_labels <router name> <host> <container port>; fills the array LABELS.
traefik_labels() {
    local name=$1 host=$2 port=$3
    LABELS=(
        --label "traefik.enable=true"
        --label "traefik.docker.network=$REVIEW_NETWORK"
        --label "traefik.http.routers.$name.rule=Host(\`$host\`)"
        --label "traefik.http.routers.$name.entrypoints=$TRAEFIK_ENTRYPOINT_HTTPS"
        --label "traefik.http.routers.$name.tls=true"
        --label "traefik.http.routers.$name.tls.certresolver=$TRAEFIK_CERT_RESOLVER"
        --label "traefik.http.routers.$name.middlewares=$name-auth,$name-headers"
        --label "traefik.http.routers.$name.service=$name"
        --label "traefik.http.routers.$name-http.rule=Host(\`$host\`)"
        --label "traefik.http.routers.$name-http.entrypoints=$TRAEFIK_ENTRYPOINT_HTTP"
        --label "traefik.http.routers.$name-http.middlewares=$name-redirect"
        --label "traefik.http.routers.$name-http.service=$name"
        --label "traefik.http.middlewares.$name-redirect.redirectscheme.scheme=https"
        --label "traefik.http.middlewares.$name-auth.basicauth.users=$REVIEW_BASIC_AUTH"
        --label "traefik.http.middlewares.$name-headers.headers.customresponseheaders.X-Robots-Tag=noindex, nofollow"
        --label "traefik.http.services.$name.loadbalancer.server.port=$port"
    )
}
