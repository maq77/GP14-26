#!/bin/bash
set -e
echo "🚀 Starting SSSP Development Stack..."
cd "$(dirname "$0")/../infrastructure/docker"
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d --build
echo ""
echo "✅ All services started!"
echo "🌍 Access points:"
echo "  - Web Dashboard:  http://localhost:5173"
echo "  - API Swagger:    http://localhost:8080/swagger"
echo "  - AI Service:     http://localhost:8000/docs"
echo "  - RabbitMQ:       http://localhost:15672"
echo "  - MinIO Console:  http://localhost:9001"
