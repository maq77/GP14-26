#!/usr/bin/env bash
set -euo pipefail

# Root of the repo (SSSP/GP14-26)
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

IMAGE_NAME="${DOCKER_REGISTRY:-localhost}/sssp-ai-dev"
TAG="${1:-dev}"

echo "🔨 Building AI Service (development)..."
echo "Image: ${IMAGE_NAME}:${TAG}"
echo "Root:  ${ROOT_DIR}"

docker build \
  -f "${ROOT_DIR}/apps/ai/Dockerfile" \
  --target development \
  -t "${IMAGE_NAME}:${TAG}" \
  -t "${IMAGE_NAME}:latest" \
  "${ROOT_DIR}"

echo "✅ Done building ${IMAGE_NAME}:${TAG}"
