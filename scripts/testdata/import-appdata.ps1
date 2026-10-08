#!/usr/bin/env pwsh
# the postgres container starts with the name bookennis-dev-services
$postgres = docker ps --filter name=bookennis-dev-services-postgres-. --format "{{.ID}}"

if (!$postgres) {
    throw "Database container is not running"
}

$root_dir = Split-Path(Split-Path $PSScriptRoot -Parent) -Parent
docker cp ("{0}/dump/test/testdata.dump" -f $root_dir) ("{0}:/tmp" -f $postgres)

# if not in docker file export password here
# drop database
Write-Host "Dropping database bookennis"
docker exec $postgres dropdb -U postgres --if-exists bookennis --force

# execute pg_restore in container - throws errors where there are none -> to out-null
Write-Host "Creating database bookennis with test data"
docker exec $postgres createdb -U postgres bookennis
docker exec $postgres pg_restore -n public -d bookennis -Fc -U postgres "/tmp/testdata.dump"

Write-Host "Done"
