#!/usr/bin/env pwsh

docker stack deploy -c docker-compose.dev.yml -c docker-compose.dev.frontend.yml m2-dev --with-registry-auth
