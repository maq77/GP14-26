#!/usr/bin/env bash
set -euo pipefail

CONTAINER_NAME="sssp-ai-dev"

echo "🛑 Stopping AI Service dev container (${CONTAINER_NAME})..."

if docker ps -a --format '{{.Names}}' | grep -q "^${CONTAINER_NAME}\$"; then
  docker stop "${CONTAINER_NAME}" >/dev/null 2>&1 || true
  docker rm "${CONTAINER_NAME}" >/dev/null 2>&1 || true
  echo "✅ ${CONTAINER_NAME} stopped and removed."
else
  echo "ℹ️  No container named ${CONTAINER_NAME} found."
fi
