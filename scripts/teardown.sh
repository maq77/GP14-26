#!/bin/bash
set -e

echo "🧹 Tearing down SSSP environment..."

cd infrastructure/docker

# Stop and remove containers
docker-compose down

# Optional: Remove volumes (data will be lost!)
read -p "Remove all data volumes? (y/N): " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
  docker-compose down -v
  echo "✅ All data removed"
else
  echo "✅ Containers stopped (data preserved)"
fi
echo "Teardown complete."