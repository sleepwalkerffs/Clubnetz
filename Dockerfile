# Builds the image that runs Clubnetz: the API, which also serves the Blazor WebAssembly client.
#
#   docker build -t clubnetz --build-arg SOURCE_REVISION_ID=$(git rev-parse HEAD) .

# Keep the SDK version in sync with global.json
FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build

# The WebAssembly build tools need Python
RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /source
COPY global.json ./
COPY src/ src/

RUN dotnet workload restore src/backend/Bookennis.Api/Bookennis.Api.csproj

# The commit becomes part of the app version (1.0.0+<commit>). Clients compare it with their own
# version to notice a new deployment, so every build has to get a different one.
ARG SOURCE_REVISION_ID=
# Set by Docker (amd64, arm64). Publishing for one platform leaves out the native libraries of all others.
ARG TARGETARCH
RUN dotnet publish src/backend/Bookennis.Api/Bookennis.Api.csproj \
    -c Release \
    --os linux --arch ${TARGETARCH} --self-contained false \
    -o /app \
    -p:SourceRevisionId=${SOURCE_REVISION_ID}


FROM mcr.microsoft.com/dotnet/aspnet:10.0

# curl is only there for the health check
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app .

# ASP.NET Core stores the keys that protect the login cookies and the tokens in emails here.
# Mount a volume at this path, otherwise every deployment signs all users out.
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys \
    && chown -R $APP_UID /home/app/.aspnet
VOLUME /home/app/.aspnet/DataProtection-Keys

USER $APP_UID

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
    CMD curl --fail --silent http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Bookennis.Api.dll"]
