#!/usr/bin/env bash
# The only command the GitHub workflow's SSH key may run (forced in authorized_keys). It accepts
# exactly "deploy <pr> <sha>" and "remove <pr>" and hands them to review-env.sh, which runs as root
# through a sudo rule for that one script.

set -euo pipefail
set -f

read -r -a args <<< "${SSH_ORIGINAL_COMMAND:-}"
script="$(dirname "$(readlink -f "$0")")/review-env.sh"

case "${args[0]:-}" in
    deploy)
        [[ ${#args[@]} -eq 3 && ${args[1]} =~ ^[0-9]{1,6}$ && ${args[2]} =~ ^[0-9a-f]{40}$ ]] || { echo "usage: deploy <pr> <sha>" >&2; exit 2; }
        exec sudo -n "$script" deploy "${args[1]}" "${args[2]}"
        ;;
    remove)
        [[ ${#args[@]} -eq 2 && ${args[1]} =~ ^[0-9]{1,6}$ ]] || { echo "usage: remove <pr>" >&2; exit 2; }
        exec sudo -n "$script" remove "${args[1]}"
        ;;
    *)
        echo "allowed: deploy <pr> <sha> | remove <pr>" >&2
        exit 2
        ;;
esac
