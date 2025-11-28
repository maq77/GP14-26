# ==================================================
# SSSP API - ASP.NET Core 9.0
# Optimized Multi-Stage Build
# ==================================================

# --------------------------------------------------
# Stage 1: Base Runtime
# --------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0-bookworm-slim AS base

WORKDIR /app
EXPOSE 8080

# Install curl for health checks
RUN apt-get update && apt-get install -y --no-install-recommends curl && \
    rm -rf /var/lib/apt/lists/*

# Non-root user
RUN groupadd -r -g 1000 sssp && \
    useradd -r -u 1000 -g sssp -s /bin/bash sssp

# --------------------------------------------------
# Stage 2: Build Cache (Dependencies only)
# --------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0-bookworm-slim AS restore

WORKDIR /src

# Copy only project files (for caching)
COPY apps/api/SSSP.sln .
COPY apps/api/src/SSSP.Api/SSSP.Api.csproj src/SSSP.Api/
COPY apps/api/src/SSSP.BL/SSSP.BL.csproj src/SSSP.BL/
COPY apps/api/src/SSSP.DAL/SSSP.DAL.csproj src/SSSP.DAL/
COPY apps/api/src/SSSP.Infrastructure.AI.Grpc/SSSP.Infrastructure.AI.Grpc.csproj src/SSSP.Infrastructure.AI.Grpc/

# Restore (cached layer)
RUN dotnet restore

# --------------------------------------------------
# Stage 3: Development (Hot Reload)
# --------------------------------------------------
FROM restore AS development

# Copy source code
COPY apps/api/src/ src/
COPY packages/contracts/csharp/ /packages/contracts/csharp/

WORKDIR /src/src/SSSP.Api

# Development server with hot-reload
CMD ["dotnet", "watch", "run", "--urls=http://0.0.0.0:8080", "--no-restore"]

# --------------------------------------------------
# Stage 4: Build
# --------------------------------------------------
FROM restore AS build

ARG BUILD_CONFIGURATION=Release

COPY apps/api/src/ src/
COPY packages/contracts/csharp/ /packages/contracts/csharp/

WORKDIR /src/src/SSSP.Api

RUN dotnet build -c $BUILD_CONFIGURATION -o /app/build --no-restore

# --------------------------------------------------
# Stage 5: Publish
# --------------------------------------------------
FROM build AS publish

ARG BUILD_CONFIGURATION=Release

RUN dotnet publish -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    --no-build \
    /p:UseAppHost=false

# --------------------------------------------------
# Stage 6: Production Runtime
# --------------------------------------------------
FROM base AS production

WORKDIR /app

COPY --from=publish --chown=sssp:sssp /app/publish .

USER sssp

HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "SSSP.Api.dll"]