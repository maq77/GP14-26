# ==================================================
# SSSP API - ASP.NET Core 9.0 (Dev / Prod)
# ==================================================

# ---------------------------
# Stage 1: base runtime
# ---------------------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0-bookworm-slim AS base
WORKDIR /app
EXPOSE 8080

RUN apt-get update && apt-get install -y --no-install-recommends curl && \
    rm -rf /var/lib/apt/lists/*

RUN groupadd -r -g 1000 sssp && \
    useradd -r -u 1000 -g sssp -s /bin/bash sssp

# --------------------------------------------------
# Stage 2: Restore Dependencies (Cached)
# --------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0-bookworm-slim AS restore
WORKDIR /src

# Put NuGet.Config for Linux container (NO Windows fallback paths)
COPY apps/api/NuGet.Config /root/.nuget/NuGet/NuGet.Config

# Optional but recommended: keep packages cache predictable
ENV NUGET_PACKAGES=/root/.nuget/packages \
    NUGET_HTTP_TIMEOUT_SECONDS=600 \
    NUGET_RETRY_COUNT=10 \
    NUGET_RETRY_DELAY_SECONDS=5 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1

# Copy solution + csproj only for restore cache
COPY apps/api/SSSP.sln ./
COPY apps/api/src/SSSP.Api/SSSP.Api.csproj src/SSSP.Api/
COPY apps/api/src/SSSP.BL/SSSP.BL.csproj src/SSSP.BL/
COPY apps/api/src/SSSP.DAL/SSSP.DAL.csproj src/SSSP.DAL/
COPY apps/api/src/SSSP.Infrastructure.AI.Grpc/SSSP.Infrastructure.AI.Grpc.csproj src/SSSP.Infrastructure.AI.Grpc/
COPY apps/api/src/SSSP.Infrastructure.Persistence/SSSP.Infrastructure.Persistence.csproj src/SSSP.Infrastructure.Persistence/
COPY apps/api/src/SSSP.Telemetry.Abstractions/SSSP.Telemetry.Abstractions.csproj src/SSSP.Telemetry.Abstractions/

# Restore packages
RUN dotnet restore

# --------------------------------------------------
# Stage 3: Development (Hot Reload)
# --------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0-bookworm-slim AS development
WORKDIR /src

# Copy restored packages from restore stage
COPY --from=restore /root/.nuget/packages /root/.nuget/packages

# Copy project files
COPY apps/api/SSSP.sln .
COPY apps/api/src/SSSP.Api/SSSP.Api.csproj src/SSSP.Api/
COPY apps/api/src/SSSP.BL/SSSP.BL.csproj src/SSSP.BL/
COPY apps/api/src/SSSP.DAL/SSSP.DAL.csproj src/SSSP.DAL/
COPY apps/api/src/SSSP.Infrastructure.AI.Grpc/SSSP.Infrastructure.AI.Grpc.csproj src/SSSP.Infrastructure.AI.Grpc/
COPY apps/api/src/SSSP.Infrastructure.Persistence/SSSP.Infrastructure.Persistence.csproj src/SSSP.Infrastructure.Persistence/
COPY apps/api/src/SSSP.Telemetry.Abstractions/SSSP.Telemetry.Abstractions.csproj src/SSSP.Telemetry.Abstractions/

# Restore to generate project.assets.json
RUN dotnet restore

WORKDIR /src/src/SSSP.Api

# Development server with hot-reload
CMD ["dotnet", "watch", "run", "--urls", "http://0.0.0.0:8080"]

# ---------------------------
# Stage 4: build
# ---------------------------
FROM restore AS build
ARG BUILD_CONFIGURATION=Release

COPY apps/api/src/ src/
COPY packages/contracts/csharp/ /packages/contracts/csharp/

WORKDIR /src/src/SSSP.Api
RUN dotnet build -c $BUILD_CONFIGURATION -o /app/build --no-restore

# ---------------------------
# Stage 5: publish
# ---------------------------
FROM build AS publish
ARG BUILD_CONFIGURATION=Release

RUN dotnet publish -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    --no-build \
    /p:UseAppHost=false

# ---------------------------
# Stage 6: production runtime
# ---------------------------
FROM base AS production
WORKDIR /app
COPY --from=publish --chown=sssp:sssp /app/publish .

USER sssp

HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
  CMD curl -fsS http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "SSSP.Api.dll"]
