#!/usr/bin/env bash
# Starts the app in the background for a UI check in GitHub Actions (Claude workflow, UI check workflow).
#
#   scripts/ui-check/start-app.sh          build and (re)start the app, then fill in the test data
#   scripts/ui-check/start-app.sh stop     stop it
#
# The app runs at http://localhost:5080 against its own database "clubnetz_ui" in the PostgreSQL
# service of the job (127.0.0.1:5432, password "developer"). Run it again after changing code, it
# stops the old app, migrates the database and starts the new build. The log is in /tmp/clubnetz-ui.log.
#
# Only invented test data (seed.sh), never real member data.

set -euo pipefail

cd "$(dirname "$0")/../.."

PORT="${UI_PORT:-5080}"
DB="${UI_DB:-clubnetz_ui}"
LOG="${UI_LOG:-/tmp/clubnetz-ui.log}"
PID_FILE=/tmp/clubnetz-ui.pid
export PGPASSWORD=developer

stop() {
    if [[ -f "$PID_FILE" ]]; then
        kill "$(cat "$PID_FILE")" 2>/dev/null || true
        rm -f "$PID_FILE"
    fi
    # dotnet run starts the app as a child process, make sure nothing still holds the port
    pkill -f "Bookennis.Api.*--urls http://localhost:$PORT" 2>/dev/null || true
    for _ in $(seq 1 20); do
        curl -s -o /dev/null "http://localhost:$PORT/health" || return 0
        sleep 1
    done
}

if [[ "${1:-}" == "stop" ]]; then
    stop
    echo "App stopped."
    exit 0
fi

stop

# The app migrates the database at startup but does not create it
if ! psql -h 127.0.0.1 -U postgres -tAc "select 1 from pg_database where datname = '$DB'" | grep -q 1; then
    psql -h 127.0.0.1 -U postgres -c "create database \"$DB\"" > /dev/null
fi

echo "Building the app..."
dotnet build src/backend/Bookennis.Api -v q -nologo > /tmp/clubnetz-ui-build.log 2>&1 || {
    tail -40 /tmp/clubnetz-ui-build.log
    echo "Build failed, see /tmp/clubnetz-ui-build.log"
    exit 1
}

export ASPNETCORE_ENVIRONMENT=Development
export ConnectionString="Server=127.0.0.1;Port=5432;Database=$DB;User Id=postgres;Password=developer"
export AppUrl="http://localhost:$PORT"

nohup dotnet run --no-build --no-launch-profile --project src/backend/Bookennis.Api -- --urls "http://localhost:$PORT" > "$LOG" 2>&1 &
echo $! > "$PID_FILE"

echo "Waiting for the app..."
for _ in $(seq 1 180); do
    if curl -sf -o /dev/null "http://localhost:$PORT/health"; then
        "$(dirname "$0")/seed.sh"
        echo "App runs at http://localhost:$PORT (log: $LOG)"
        exit 0
    fi
    if ! kill -0 "$(cat "$PID_FILE")" 2>/dev/null; then
        tail -60 "$LOG"
        echo "The app stopped during startup, see $LOG"
        exit 1
    fi
    sleep 1
done

tail -60 "$LOG"
echo "The app did not answer on /health within 3 minutes, see $LOG"
exit 1
