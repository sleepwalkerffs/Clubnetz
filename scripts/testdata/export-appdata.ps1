#!/usr/bin/env pwsh
# the pg container "db" starts with the name tennis-app-dev
$postgres = docker ps --filter name=bookennis-dev-services-postgres-. --format "{{.ID}}"
if (!$postgres) {
    throw "Database container is not running"
}

$root_dir = Split-Path(Split-Path $PSScriptRoot -Parent) -Parent

$dump_name = ("{0}.dump" -f (Get-Date -Format FileDateTime))

$cmd = ("pg_dump -U postgres -Fc -d bookennis -n public > /tmp/{0}" -f $dump_name)
docker exec $postgres sh -c $cmd

docker cp ("{0}:/tmp/{1}" -f $postgres, $dump_name) ("{0}/dump/test/{1}" -f $root_dir, $dump_name)

Write-Host ("Stored database dump to {0}" -f $dump_name)
