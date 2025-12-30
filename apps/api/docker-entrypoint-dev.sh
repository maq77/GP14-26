#!/bin/bash
# =============================================================================
# Development Entrypoint Script for .NET API
# Handles cache cleanup and environment setup before starting the application
# =============================================================================

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}SSSP API Development Container${NC}"
echo -e "${BLUE}========================================${NC}"

# =============================================================================
# Function: Clean build artifacts
# =============================================================================
clean_artifacts() {
    echo -e "${YELLOW}Cleaning stale build artifacts...${NC}"
    
    # Remove obj and bin directories (in case they were mounted)
    find /src/src -type d \( -name "obj" -o -name "bin" \) -exec rm -rf {} + 2>/dev/null || true
    
    # Remove NuGet cache files that might have Windows paths
    find /src/src -name "project.assets.json" -delete 2>/dev/null || true
    find /src/src -name "*.csproj.nuget.g.props" -delete 2>/dev/null || true
    find /src/src -name "*.csproj.nuget.g.targets" -delete 2>/dev/null || true
    find /src/src -name "project.nuget.cache" -delete 2>/dev/null || true
    
    echo -e "${GREEN}✓ Cleanup complete${NC}"
}

# =============================================================================
# Function: Verify NuGet packages
# =============================================================================
verify_packages() {
    echo -e "${YELLOW}Verifying NuGet packages...${NC}"
    
    if [ -d "/root/.nuget/packages" ]; then
        PACKAGE_COUNT=$(find /root/.nuget/packages -maxdepth 1 -type d | wc -l)
        echo -e "${GREEN}✓ Found ${PACKAGE_COUNT} package directories${NC}"
    else
        echo -e "${RED}⚠ NuGet packages directory not found${NC}"
    fi
}

# =============================================================================
# Function: Display environment info
# =============================================================================
show_environment() {
    echo -e "${BLUE}Environment Information:${NC}"
    echo -e "  .NET SDK Version: $(dotnet --version)"
    echo -e "  Working Directory: $(pwd)"
    echo -e "  ASPNETCORE_ENVIRONMENT: ${ASPNETCORE_ENVIRONMENT:-Not Set}"
    echo -e "  ASPNETCORE_URLS: ${ASPNETCORE_URLS:-Not Set}"
    echo ""
}

# =============================================================================
# Main Execution
# =============================================================================

# Clean up stale artifacts
clean_artifacts

# Verify packages
verify_packages

# Show environment
show_environment

# Wait a moment for any file system operations to settle
sleep 1

echo -e "${GREEN}Starting .NET application...${NC}"
echo -e "${BLUE}========================================${NC}"
echo ""

# Start the application with hot reload
# The --no-restore flag prevents restore on every file change
exec dotnet watch run \
    --project /src/src/SSSP.Api/SSSP.Api.csproj \
    --urls "${ASPNETCORE_URLS:-http://0.0.0.0:8080}" \
    --no-restore \
    --no-build