#!/usr/bin/env pwsh

<#
.PARAMETER DeployStack
    Deploy the services in the local docker swarm. 
    If deployed in the local docker swarm, the services start automatically with docker.
    To use this, you need to run docker swarm init once.
.SYNOPSIS
    Starts development services for Bookennis.
#>

#Requires -PSEdition Core

param (
    [switch] $DeployStack
)

$env:PGADMIN_STORAGE_VOLUME = $IsWindows ? "C:/Daten/bookennis" : "/tmp/bookennis/data";

if (!(Test-Path -Path $env:PGADMIN_STORAGE_VOLUME)) { 
    mkdir -p $env:PGADMIN_STORAGE_VOLUME

    if ($IsLinux) {
        Write-Host "Need to set owner permissions for DB backup dir. Need root for that."
        sudo chown 5050:5050 $env:PGADMIN_STORAGE_VOLUME
    }
}

if ($DeployStack) {
    docker stack deploy --with-registry-auth -c docker/development/services.yml bookennis
}
else {
    docker compose -f docker/development/services.yml down
    docker compose -f docker/development/services.yml build
    docker compose -p bookennis-dev-services -f docker/development/services.yml up 
}

$env:PGADMIN_STORAGE_VOLUME = $IsWindows ? "C:/Daten/bookennis" : "/tmp/bookennis/data";