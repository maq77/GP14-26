#!/bin/bash
set -e

echo "🚀 Setting up SSSP Development Environment..."

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
  echo "❌ Docker is not running. Please start Docker first."
  exit 1
fi

# Copy .env if doesn't exist
if [ ! -f infrastructure/docker/.env ]; then
  echo "📝 Creating .env file from template..."
  cp infrastructure/docker/.env.example infrastructure/docker/.env
  echo "⚠️  Please edit infrastructure/docker/.env with your settings"
fi

# Pull base images
echo "Pulling base images..."
docker-compose -f infrastructure/docker/docker-compose.yml pull

# Build services
echo "Building services..."
docker-compose -f infrastructure/docker/docker-compose.yml \
  -f infrastructure/docker/docker-compose.dev.yml build

# Start infrastructure services first
echo "Starting infrastructure services..."
docker-compose -f infrastructure/docker/docker-compose.yml up -d \
  sqlserver redis rabbitmq minio

# Wait for services
echo "Waiting for services to be healthy..."
sleep 10

# Start application services
echo "🚀 Starting application services..."
docker-compose -f infrastructure/docker/docker-compose.yml \
  -f infrastructure/docker/docker-compose.dev.yml up -d

echo "Development environment is ready!"
echo ""
echo "Services:"
echo "  - API (.NET):        http://localhost:8080"
echo "  - AI Service:        http://localhost:8000"
echo "  - Web Dashboard:     http://localhost:5173"
echo "  - SQL Server:        localhost:1433"
echo "  - Redis:             localhost:6379"
echo "  - RabbitMQ UI:       http://localhost:15672"
echo "  - MinIO Console:     http://localhost:9001"
echo ""
echo "View logs: docker-compose -f infrastructure/docker/docker-compose.yml logs -f"
echo "Stop services: docker-compose -f infrastructure/docker/docker-compose.yml down"