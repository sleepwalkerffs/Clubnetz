#!/usr/bin/env bash
# Starts, updates and removes the review environment of a pull request. Runs as root on the server,
# called by the GitHub workflow through ssh-entry.sh (see README.md).
#
#   review-env.sh deploy <pull request number> <commit sha>   start or update https://pr-<n>.<REVIEW_DOMAIN>
#   review-env.sh remove <pull request number>                 remove the container, its keys, database and role
#   review-env.sh list                                          show the review environments
#   review-env.sh reconnect-proxy                               put the proxy back into the review network (cron)
#
# Each pull request gets its own database, copied from the pseudonymized template when the environment
# is created, and its own database role that can only connect to that database. Later deployments keep
# the database (the app applies new migrations at startup). Closing and reopening the pull request
# starts over with a fresh copy.

set -euo pipefail
source "$(dirname "$(readlink -f "$0")")/lib.sh"

command=${1:-}
pr=${2:-}

valid_pr() { [[ $pr =~ ^[0-9]{1,6}$ ]] || die "invalid pull request number"; }

deploy() {
    local sha=$1
    [[ $sha =~ ^[0-9a-f]{40}$ ]] || die "invalid commit sha"

    local name="clubnetz-pr-$pr" database="pr_$pr" role="pr_$pr" host="pr-$pr.$REVIEW_DOMAIN"

    # Limit how many run at once, the server also runs production
    local running
    running=$(docker ps -a --filter label=clubnetz.review=true --format '{{.Names}}' | grep -cvx "$name" || true)
    if (( running >= REVIEW_MAX_ENVIRONMENTS )); then
        die "already $running review environments, the limit is $REVIEW_MAX_ENVIRONMENTS. Close a pull request or raise REVIEW_MAX_ENVIRONMENTS."
    fi

    docker pull --quiet "$REVIEW_IMAGE:$sha" >/dev/null

    # A new password on every deployment, it only lives in the container's settings
    local password
    password=$(random_password)
    if role_exists "$role"; then
        review_psql -c "alter role \"$role\" with login password '$password';"
    else
        review_psql -c "create role \"$role\" with login password '$password';"
    fi

    if ! database_exists "$database"; then
        database_exists "$REVIEW_TEMPLATE_DB" || die "the template database is missing, run refresh-template.sh first"
        echo "Creating database $database from the template"
        review_psql -c "create database \"$database\" owner \"$role\" template \"$REVIEW_TEMPLATE_DB\";"
        review_psql -c "revoke connect, temporary on database \"$database\" from public;"
        review_psql -d "$database" -c "reassign owned by \"$REVIEW_TEMPLATE_OWNER\" to \"$role\";"
    fi

    ensure_proxy_in_network
    traefik_labels "$name" "$host" 8080

    docker rm -f "$name" >/dev/null 2>&1 || true
    docker run -d --name "$name" \
        --network "$REVIEW_NETWORK" \
        --restart unless-stopped \
        --memory "$REVIEW_MEMORY_LIMIT" \
        --label clubnetz.review=true \
        --label "clubnetz.review.pr=$pr" \
        --label "clubnetz.review.sha=$sha" \
        "${LABELS[@]}" \
        -v "$name-keys:/home/app/.aspnet/DataProtection-Keys" \
        -e "ConnectionString=Server=$REVIEW_POSTGRES;Port=5432;Database=$database;User Id=$role;Password=$password" \
        -e "AppUrl=https://$host/" \
        -e "Email__SenderAddress=review@clubnetz.app" \
        -e "Email__SenderName=Clubnetz Review PR $pr" \
        -e "Email__SmtpServer=$REVIEW_MAILPIT" \
        -e "Email__SmtpPort=1025" \
        -e "Email__SmtpUsername=" \
        -e "Email__SmtpPassword=" \
        -e "Email__EnableSsl=false" \
        -e "Legal__OperatorName=Review environment of pull request $pr" \
        -e "Legal__Email=review@example.test" \
        -e "Legal__HostingProvider=Hetzner" \
        -e "Legal__EmailProvider=none, emails are caught at https://mail.$REVIEW_DOMAIN" \
        "$REVIEW_IMAGE:$sha" >/dev/null
    # No Push__*, Stripe__* or SumUp__* settings: push notifications and payments stay switched off.

    echo "Waiting for the app to start"
    local status
    for _ in $(seq 1 60); do
        status=$(docker inspect -f '{{.State.Health.Status}}' "$name")
        case $status in
            healthy) echo "Ready: https://$host/"; return 0 ;;
            unhealthy) break ;;
        esac
        sleep 5
    done
    # The logs stay on the server: they would end up in the public workflow log.
    die "the app did not become healthy ($status). Look at the logs on the server: docker logs $name"
}

remove() {
    local name="clubnetz-pr-$pr"
    docker rm -f "$name" >/dev/null 2>&1 || true
    docker volume rm "$name-keys" >/dev/null 2>&1 || true
    drop_database "pr_$pr"
    review_psql -c "drop role if exists \"pr_$pr\";"
    echo "Removed the review environment of pull request $pr"
}

list() {
    docker ps -a --filter label=clubnetz.review=true \
        --format 'table {{.Label "clubnetz.review.pr"}}\t{{.Status}}\t{{.Label "clubnetz.review.sha"}}'
}

load_config
case $command in
    deploy) valid_pr; take_lock; deploy "${3:-}" ;;
    remove) valid_pr; take_lock; remove ;;
    list) list ;;
    reconnect-proxy) ensure_proxy_in_network ;;
    *) die "usage: review-env.sh deploy <pr> <sha> | remove <pr> | list | reconnect-proxy" ;;
esac
