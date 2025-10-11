# ==================================================
# SSSP AI Service - FastAPI + PyTorch
# Multi-stage Dockerfile with GPU support
# ==================================================

# ==================================================
# Stage 1: Base Python
# ==================================================
ARG PYTHON_VERSION=3.11
FROM python:${PYTHON_VERSION}-slim AS base

# Set environment variables
ENV PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1 \
    PIP_NO_CACHE_DIR=1 \
    PIP_DISABLE_PIP_VERSION_CHECK=1 \
    DEBIAN_FRONTEND=noninteractive

WORKDIR /app

# Install system dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    build-essential \
    libglib2.0-0 \
    libsm6 \
    libxext6 \
    libxrender-dev \
    libgomp1 \
    libgl1-mesa-glx \
    && rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN groupadd -r sssp && useradd -r -g sssp sssp

# ==================================================
# Stage 2: Dependencies
# ==================================================
FROM base AS dependencies

# Copy requirements
COPY apps/ai/requirements.txt .
COPY apps/ai/pyproject.toml .

# Install Python dependencies
RUN pip install --upgrade pip setuptools wheel && \
    pip install -r requirements.txt

# ==================================================
# Stage 3: Build (for production)
# ==================================================
FROM dependencies AS build

# Copy source code
COPY apps/ai/src/ /app/src/
COPY packages/contracts/proto/ /app/proto/

# Compile protobuf files
RUN python -m grpc_tools.protoc \
    -I/app/proto \
    --python_out=/app/src/schemas \
    --grpc_python_out=/app/src/schemas \
    /app/proto/*.proto

# Pre-download models (optional - can be done at runtime)
# RUN python -c "from ultralytics import YOLO; YOLO('yolov8n.pt')"

# ==================================================
# Stage 4: Development
# ==================================================
FROM dependencies AS development

# Install development dependencies
RUN pip install \
    pytest \
    pytest-asyncio \
    pytest-cov \
    black \
    ruff \
    ipython \
    jupyter

COPY apps/ai/ /app/
COPY packages/contracts/ /app/contracts/

USER sssp
EXPOSE 8000 50051

CMD ["uvicorn", "src.api.main:app", "--host", "0.0.0.0", "--port", "8000", "--reload"]

# ==================================================
# Stage 5: Production
# ==================================================
FROM base AS production

# Copy installed dependencies
COPY --from=dependencies /usr/local/lib/python${PYTHON_VERSION}/site-packages/ \
     /usr/local/lib/python${PYTHON_VERSION}/site-packages/

# Copy application
COPY --from=build --chown=sssp:sssp /app /app

# Create directories for models and cache
RUN mkdir -p /app/models /app/cache && \
    chown -R sssp:sssp /app

USER sssp

EXPOSE 8000 50051

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:8000/health || exit 1

# Use gunicorn with uvicorn workers for production
CMD ["gunicorn", "src.api.main:app", \
     "--workers", "4", \
     "--worker-class", "uvicorn.workers.UvicornWorker", \
     "--bind", "0.0.0.0:8000", \
     "--timeout", "120", \
     "--access-logfile", "-", \
     "--error-logfile", "-"]

# ==================================================
# Stage 6: Production with GPU support
# ==================================================
FROM nvidia/cuda:12.1.0-runtime-ubuntu22.04 AS production-gpu

ENV PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1 \
    PIP_NO_CACHE_DIR=1 \
    DEBIAN_FRONTEND=noninteractive

WORKDIR /app

# Install Python and dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    python3.11 \
    python3-pip \
    curl \
    libglib2.0-0 \
    libsm6 \
    libxext6 \
    libxrender-dev \
    libgomp1 \
    libgl1-mesa-glx \
    && rm -rf /var/lib/apt/lists/*

# Copy from build stage
COPY --from=dependencies /usr/local/lib/python3.11/site-packages/ \
     /usr/local/lib/python3.11/site-packages/
COPY --from=build /app /app

RUN groupadd -r sssp && useradd -r -g sssp sssp && \
    mkdir -p /app/models /app/cache && \
    chown -R sssp:sssp /app

USER sssp
EXPOSE 8000 50051

HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:8000/health || exit 1

CMD ["gunicorn", "src.api.main:app", \
     "--workers", "2", \
     "--worker-class", "uvicorn.workers.UvicornWorker", \
     "--bind", "0.0.0.0:8000", \
     "--timeout", "180"]
