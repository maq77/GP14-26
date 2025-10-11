#!/bin/bash
set -e

echo "🔨 Building Docker images for SSSP..."

# Color codes
GREEN='\033[0;32m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Get version from argument or use default
VERSION=${1:-latest}
REGISTRY=${DOCKER_REGISTRY:-localhost}

echo -e "${BLUE}Version: ${VERSION}${NC}"
echo -e "${BLUE}Registry: ${REGISTRY}${NC}"

# Build API
echo -e "${GREEN}Building API (.NET)...${NC}"
docker build \
  -f apps/api/Dockerfile \
  -t ${REGISTRY}/sssp-api:${VERSION} \
  -t ${REGISTRY}/sssp-api:latest \
  --target production \
  .

# Build AI Service
echo -e "${GREEN}Building AI Service (FastAPI)...${NC}"
docker build \
  -f apps/ai/Dockerfile \
  -t ${REGISTRY}/sssp-ai:${VERSION} \
  -t ${REGISTRY}/sssp-ai:latest \
  --target production \
  .

# Build AI Service with GPU (optional)
# echo -e "${GREEN}Building AI Service with GPU...${NC}"
# docker build \
#   -f apps/ai/Dockerfile \
#   -t ${REGISTRY}/sssp-ai-gpu:${VERSION} \
#   -t ${REGISTRY}/sssp-ai-gpu:latest \
#   --target production-gpu \
#   .

# Build Web Dashboard
echo -e "${GREEN}Building Web Dashboard (React)...${NC}"
docker build \
  -f apps/web/Dockerfile \
  -t ${REGISTRY}/sssp-web:${VERSION} \
  -t ${REGISTRY}/sssp-web:latest \
  --target production \
  --build-arg VITE_API_BASE=${VITE_API_BASE:-http://localhost:8080} \
  apps/web

echo -e "${GREEN}✅ All images built successfully!${NC}"

# Show images
echo ""
echo "📦 Built images:"
docker images | grep sssp

# Optional: Push to registry
read -p "Push images to registry? (y/N): " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
  echo -e "${BLUE}Pushing images...${NC}"
  docker push ${REGISTRY}/sssp-api:${VERSION}
  docker push ${REGISTRY}/sssp-ai:${VERSION}
  docker push ${REGISTRY}/sssp-web:${VERSION}
  echo -e "${GREEN}✅ Images pushed!${NC}"
fi