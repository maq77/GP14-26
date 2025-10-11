#!/bin/bash
set -e

echo "Testing Docker builds..."

# Test API build
echo "Testing API build..."
docker build -f apps/api/Dockerfile --target build -t test-api-build . > /dev/null
echo "✅ API build successful"

# Test AI build
echo "Testing AI build..."
docker build -f apps/ai/Dockerfile --target build -t test-ai-build . > /dev/null
echo "✅ AI build successful"

# Test Web build
echo "Testing Web build..."
docker build -f apps/web/Dockerfile --target build -t test-web-build apps/web > /dev/null
echo "✅ Web build successful"

# Cleanup
docker rmi test-api-build test-ai-build test-web-build > /dev/null 2>&1 || true

echo ""
echo "All builds passed!"