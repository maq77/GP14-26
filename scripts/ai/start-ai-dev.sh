#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

IMAGE_NAME="${DOCKER_REGISTRY:-localhost}/sssp-ai-dev"
TAG="${1:-dev}"
CONTAINER_NAME="sssp-ai-dev"
NETWORK_NAME="${DOCKER_NETWORK:-sssp-dev}"

echo "🚀 Starting AI Service (dev)..."
echo "Image:     ${IMAGE_NAME}:${TAG}"
echo "Container: ${CONTAINER_NAME}"
echo "Network:   ${NETWORK_NAME}"

# Create network if it doesn't exist
if ! docker network ls --format '{{.Name}}' | grep -q "^${NETWORK_NAME}\$"; then
  echo "🔧 Creating network ${NETWORK_NAME}..."
  docker network create "${NETWORK_NAME}"
fi

# Remove old container if exists
if docker ps -a --format '{{.Names}}' | grep -q "^${CONTAINER_NAME}\$"; then
  echo "🧹 Removing old container ${CONTAINER_NAME}..."
  docker rm -f "${CONTAINER_NAME}" >/dev/null 2>&1 || true
fi

# Ensure .env exists
ENV_FILE="${ROOT_DIR}/apps/ai/.env"
if [[ ! -f "${ENV_FILE}" ]]; then
  echo "⚠️  ${ENV_FILE} not found. Copying from .env.example..."
  cp "${ROOT_DIR}/apps/ai/.env.example" "${ENV_FILE}"
fi

docker run \
  --name "${CONTAINER_NAME}" \
  --network "${NETWORK_NAME}" \
  --env-file "${ENV_FILE}" \
  -p 8001:8001 \
  -p 50051:50051 \
  -v "${ROOT_DIR}/apps/ai/src:/app/src" \
  -v "${ROOT_DIR}/apps/ai/data:/app/data" \
  -v "${ROOT_DIR}/packages/contracts:/app/contracts" \
  "${IMAGE_NAME}:${TAG}"

# Note: This runs in the foreground; Ctrl+C to stop.
