# ==================================================
# SSSP API - ASP.NET Core 9.0
# Multi-stage Dockerfile for production builds
# ==================================================

# ==================================================
# Stage 1: Base Runtime
# ==================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8443

# Create non-root user
RUN addgroup -g 1000 sssp && \
    adduser -u 1000 -G sssp -s /bin/sh -D sssp

# Install curl for healthchecks
RUN apk add --no-cache curl

# ==================================================
# Stage 2: Build
# ==================================================
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy solution and project files
COPY ["apps/api/SSSP.sln", "apps/api/"]
COPY ["apps/api/src/SSSP.Api/SSSP.Api.csproj", "apps/api/src/SSSP.Api/"]
COPY ["apps/api/src/SSSP.Application/SSSP.Application.csproj", "apps/api/src/SSSP.Application/"]
COPY ["apps/api/src/SSSP.Domain/SSSP.Domain.csproj", "apps/api/src/SSSP.Domain/"]
COPY ["apps/api/src/SSSP.Infrastructure/SSSP.Infrastructure.csproj", "apps/api/src/SSSP.Infrastructure/"]

# Restore dependencies
RUN dotnet restore "apps/api/SSSP.sln"

# Copy source code
COPY apps/api/src/ apps/api/src/
COPY packages/contracts/ packages/contracts/

# Build
WORKDIR /src/apps/api/src/SSSP.Api
RUN dotnet build "SSSP.Api.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/build \
    --no-restore

# ==================================================
# Stage 3: Publish
# ==================================================
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "SSSP.Api.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    --no-build \
    /p:UseAppHost=false

# ==================================================
# Stage 4: Final Runtime
# ==================================================
FROM base AS final
WORKDIR /app

# Copy published files
COPY --from=publish --chown=sssp:sssp /app/publish .

# Switch to non-root user
USER sssp

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Entry point
ENTRYPOINT ["dotnet", "SSSP.Api.dll"]
