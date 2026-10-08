#!/usr/bin/env pwsh

# Sets host address "host.docker.internal" for windows and for linux the address of "docker0"
$HostAddress = "host.docker.internal"
if ($IsLinux) {
    # host.docker.internal does not work yet on linux. https://github.com/docker/for-linux/issues/264
    # Instead we do some magic to get the hosts IP address. Need it to connect to the angular server that runs on it
    # With powershell core 6.2.3 there were no network commands so we use normal linux commands
    $ipAddress = (Invoke-Expression "ip addr show docker0 | grep -Po 'inet \K[\d.]+'" | Out-String -OutVariable ipAddress).Trim()

    # when an adapter is available and we found the IP address we use it as host address
    # when nothing was found we use the default and hope it works ;)
    if ($ipAddress) {
        $HostAddress = $ipAddress
    }
}
$env:HOST_ADDRESS = $HostAddress

Write-Host "HOST_ADDRESS:$env:HOST_ADDRESS"

docker stack deploy -c docker-compose.dev.yml -c docker-compose.dev.backend.yml m2-dev --with-registry-auth
