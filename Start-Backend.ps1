#!/usr/bin/env pwsh

<#
.SYNOPSIS
    A script to start the backend..
#>

#Requires -PSEdition Core

$userDir = $env:USERPROFILE;

dotnet dev-certs https -v -ep $userDir/.aspnet/https/aspnetapp.pfx -p developer
$certPathPfx = "$userDir/.aspnet/https/aspnetapp.pfx"

if (!(Test-Path $certPathPfx))
{
    Write-Host "## You have to create a developer certificate"
    exit
}

docker-compose -p bookennis -f docker/development/backend.yml down
docker-compose -p bookennis -f docker/development/backend.yml build
docker-compose -p bookennis -f docker/development/backend.yml up
