#!/bin/bash
# =============================================================================
# Developer Helper Scripts for SSSP Project
# Usage: ./scripts/dev.sh [command]
# =============================================================================

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m'

# Project paths
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DOCKER_COMPOSE_FILE="$PROJECT_ROOT/docker/local/docker-compose.yaml"

# =============================================================================
# Helper Functions
# =============================================================================

print_header() {
    echo -e "${BLUE}========================================${NC}"
    echo -e "${BLUE}$1${NC}"
    echo -e "${BLUE}========================================${NC}"
}

print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

print_error() {
    echo -e "${RED}✗ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠ $1${NC}"
}

print_info() {
    echo -e "${CYAN}ℹ $1${NC}"
}

# =============================================================================
# Command: Start Development Environment
# =============================================================================
cmd_start() {
    print_header "Starting Development Environment"
    
    cd "$PROJECT_ROOT"
    docker-compose -f "$DOCKER_COMPOSE_FILE" up -d
    
    print_success "Services started"
    print_info "API: http://localhost:8080"
    print_info "AI: http://localhost:8001"
    print_info "gRPC: localhost:50051"
    
    echo ""
    print_info "View logs: ./scripts/dev.sh logs"
    print_info "Stop services: ./scripts/dev.sh stop"
}

# =============================================================================
# Command: Stop Development Environment
# =============================================================================
cmd_stop() {
    print_header "Stopping Development Environment"
    
    cd "$PROJECT_ROOT"
    docker-compose -f "$DOCKER_COMPOSE_FILE" down
    
    print_success "Services stopped"
}

# =============================================================================
# Command: Restart Services
# =============================================================================
cmd_restart() {
    print_header "Restarting Services"
    
    cmd_stop
    sleep 2
    cmd_start
}

# =============================================================================
# Command: View Logs
# =============================================================================
cmd_logs() {
    SERVICE=${1:-}
    
    if [ -z "$SERVICE" ]; then
        print_header "Viewing All Logs"
        docker-compose -f "$DOCKER_COMPOSE_FILE" logs -f
    else
        print_header "Viewing $SERVICE Logs"
        docker-compose -f "$DOCKER_COMPOSE_FILE" logs -f "$SERVICE"
    fi
}

# =============================================================================
# Command: Clean Build (Complete Rebuild)
# =============================================================================
cmd_clean() {
    print_header "Cleaning Development Environment"
    
    print_warning "This will remove all containers, images, and volumes"
    read -p "Are you sure? (y/N) " -n 1 -r
    echo
    
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        print_info "Cancelled"
        exit 0
    fi
    
    cd "$PROJECT_ROOT"
    
    # Stop containers
    print_info "Stopping containers..."
    docker-compose -f "$DOCKER_COMPOSE_FILE" down -v
    
    # Remove images
    print_info "Removing images..."
    docker rmi sssp-api-dev:latest 2>/dev/null || true
    docker rmi sssp-ai-dev:latest 2>/dev/null || true
    
    # Remove volumes
    print_info "Removing volumes..."
    docker volume rm sssp-nuget-packages 2>/dev/null || true
    docker volume rm sssp-nuget-http-cache 2>/dev/null || true
    docker volume rm sssp-ai-data 2>/dev/null || true
    docker volume rm sssp-ai-cache 2>/dev/null || true
    
    # Clean local build artifacts
    print_info "Cleaning local build artifacts..."
    find "$PROJECT_ROOT/apps/api/src" -type d \( -name "obj" -o -name "bin" \) -exec rm -rf {} + 2>/dev/null || true
    
    print_success "Cleanup complete"
}

# =============================================================================
# Command: Rebuild Specific Service
# =============================================================================
cmd_rebuild() {
    SERVICE=${1:-}
    
    if [ -z "$SERVICE" ]; then
        print_error "Please specify a service: api or ai"
        exit 1
    fi
    
    print_header "Rebuilding $SERVICE Service"
    
    cd "$PROJECT_ROOT"
    
    # Stop the service
    docker-compose -f "$DOCKER_COMPOSE_FILE" stop "$SERVICE"
    
    # Remove the container
    docker-compose -f "$DOCKER_COMPOSE_FILE" rm -f "$SERVICE"
    
    # Rebuild without cache
    docker-compose -f "$DOCKER_COMPOSE_FILE" build --no-cache "$SERVICE"
    
    # Start the service
    docker-compose -f "$DOCKER_COMPOSE_FILE" up -d "$SERVICE"
    
    print_success "$SERVICE rebuilt and started"
}

# =============================================================================
# Command: Execute Shell in Container
# =============================================================================
cmd_shell() {
    SERVICE=${1:-api}
    
    print_header "Opening Shell in $SERVICE Container"
    
    docker-compose -f "$DOCKER_COMPOSE_FILE" exec "$SERVICE" /bin/bash
}

# =============================================================================
# Command: Show Service Status
# =============================================================================
cmd_status() {
    print_header "Service Status"
    
    docker-compose -f "$DOCKER_COMPOSE_FILE" ps
}

# =============================================================================
# Command: Run Tests
# =============================================================================
cmd_test() {
    SERVICE=${1:-api}
    
    print_header "Running Tests for $SERVICE"
    
    if [ "$SERVICE" == "api" ]; then
        docker-compose -f "$DOCKER_COMPOSE_FILE" exec api dotnet test
    elif [ "$SERVICE" == "ai" ]; then
        docker-compose -f "$DOCKER_COMPOSE_FILE" exec ai pytest
    else
        print_error "Unknown service: $SERVICE"
        exit 1
    fi
}

# =============================================================================
# Command: Database Migrations
# =============================================================================
cmd_migrate() {
    print_header "Running Database Migrations"
    
    docker-compose -f "$DOCKER_COMPOSE_FILE" exec api \
        dotnet ef database update --project /src/src/SSSP.Infrastructure.Persistence
    
    print_success "Migrations applied"
}

# =============================================================================
# Command: Prune Docker System
# =============================================================================
cmd_prune() {
    print_header "Pruning Docker System"
    
    print_warning "This will remove all unused containers, networks, images, and volumes"
    read -p "Are you sure? (y/N) " -n 1 -r
    echo
    
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        print_info "Cancelled"
        exit 0
    fi
    
    docker system prune -af --volumes
    
    print_success "Docker system pruned"
}

# =============================================================================
# Command: Show Help
# =============================================================================
cmd_help() {
    cat << EOF
${BLUE}SSSP Development Helper Script${NC}

${YELLOW}Usage:${NC}
  ./scripts/dev.sh [command] [options]

${YELLOW}Commands:${NC}
  ${GREEN}start${NC}              Start all development services
  ${GREEN}stop${NC}               Stop all services
  ${GREEN}restart${NC}            Restart all services
  ${GREEN}logs [service]${NC}     View logs (all or specific service)
  ${GREEN}clean${NC}              Complete cleanup (containers, images, volumes)
  ${GREEN}rebuild <service>${NC}  Rebuild specific service (api or ai)
  ${GREEN}shell <service>${NC}    Open shell in container (default: api)
  ${GREEN}status${NC}             Show status of all services
  ${GREEN}test [service]${NC}     Run tests (default: api)
  ${GREEN}migrate${NC}            Run database migrations
  ${GREEN}prune${NC}              Prune entire Docker system
  ${GREEN}help${NC}               Show this help message

${YELLOW}Examples:${NC}
  ./scripts/dev.sh start
  ./scripts/dev.sh logs api
  ./scripts/dev.sh rebuild api
  ./scripts/dev.sh shell ai
  ./scripts/dev.sh test

${YELLOW}Services:${NC}
  - ${CYAN}api${NC}  : .NET API service (http://localhost:8080)
  - ${CYAN}ai${NC}   : Python AI service (http://localhost:8001, grpc://localhost:50051)

EOF
}

# =============================================================================
# Main Script
# =============================================================================

COMMAND=${1:-help}

case "$COMMAND" in
    start)
        cmd_start
        ;;
    stop)
        cmd_stop
        ;;
    restart)
        cmd_restart
        ;;
    logs)
        cmd_logs "${2:-}"
        ;;
    clean)
        cmd_clean
        ;;
    rebuild)
        cmd_rebuild "${2:-}"
        ;;
    shell)
        cmd_shell "${2:-api}"
        ;;
    status)
        cmd_status
        ;;
    test)
        cmd_test "${2:-api}"
        ;;
    migrate)
        cmd_migrate
        ;;
    prune)
        cmd_prune
        ;;
    help|--help|-h)
        cmd_help
        ;;
    *)
        print_error "Unknown command: $COMMAND"
        echo ""
        cmd_help
        exit 1
        ;;
esac