<#
.SYNOPSIS
    Creates a pseudonymized copy of the development database for screenshots and demos.

.DESCRIPTION
    Copies the database "bookennis" of the developer services container into a new
    database (default "clubnetz_screens"), then replaces all names, email addresses,
    addresses, phone numbers, birthdays, profile pictures and free-text comments with
    invented values and renames club 1. The source database is not changed.

    Then it adds invented demo content to club 1 (a busy booking week around today,
    upcoming events with registrations, news and membership fee orders) in the
    language given with -Language.

    Every user of the copy gets the password of the template account, so you can log
    in as any member, admin or treasurer. Start the app against the copy with:

        $env:ConnectionString = "Server=127.0.0.1;Port=8432;Database=clubnetz_screens;User Id=postgres;Password=password"
        dotnet run --project src/backend/Bookennis.Api

.PARAMETER TemplateEmail
    Email of an existing local account whose password all users get.

.PARAMETER Language
    Language of the demo content: de or en.
#>
param(
    [Parameter(Mandatory)] [string] $TemplateEmail,
    [ValidateSet("de", "en")] [string] $Language = "de",
    [string] $Target = "clubnetz_screens",
    [string] $Source = "bookennis",
    [string] $Container = "bookennis-dev-services-postgres-1"
)

$ErrorActionPreference = "Stop"

if ($Target -eq $Source) { throw "Target must differ from the source database." }

# A database can only be used as template while nobody is connected to it.
docker exec $Container psql -U postgres -v ON_ERROR_STOP=1 -c "select pg_terminate_backend(pid) from pg_stat_activity where datname in ('$Source', '$Target') and pid <> pg_backend_pid();" | Out-Null
docker exec $Container psql -U postgres -v ON_ERROR_STOP=1 -c "drop database if exists `"$Target`";"
docker exec $Container psql -U postgres -v ON_ERROR_STOP=1 -c "create database `"$Target`" template `"$Source`";"
if ($LASTEXITCODE -ne 0) { throw "Copying the database failed." }

Get-Content -Raw -Encoding utf8 "$PSScriptRoot/pseudonymize.sql" |
    docker exec -i $Container psql -U postgres -d $Target -v ON_ERROR_STOP=1 -v "template_email=$TemplateEmail" -q
$pseudonymized = $LASTEXITCODE -eq 0
if ($pseudonymized) {
    Get-Content -Raw -Encoding utf8 "$PSScriptRoot/demo-content.sql" |
        docker exec -i $Container psql -U postgres -d $Target -v ON_ERROR_STOP=1 -v "lang=$Language" -q
}
if (-not $pseudonymized) {
    docker exec $Container psql -U postgres -c "drop database if exists `"$Target`";" | Out-Null
    throw "Pseudonymizing failed, the copy was removed."
}

if ($LASTEXITCODE -ne 0) { throw "Adding the demo content failed. The pseudonymized copy '$Target' is kept." }

Write-Host "Database '$Target' is ready. All users have the password of $TemplateEmail."
