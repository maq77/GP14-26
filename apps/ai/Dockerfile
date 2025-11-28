# ==================================================
# SSSP AI Service - Optimized Multi-Stage
# Supports: development + production
# ==================================================

ARG PYTHON_VERSION=3.11

# ==================================================
# Stage 1: Base (System dependencies)
# ==================================================
FROM python:${PYTHON_VERSION}-slim AS base

ENV PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1 \
    PIP_NO_CACHE_DIR=1 \
    PIP_DISABLE_PIP_VERSION_CHECK=1 \
    DEBIAN_FRONTEND=noninteractive

# Install minimal system dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    libgomp1 \
    libglib2.0-0 \
    libsm6 \
    libxext6 \
    libxrender1 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# ==================================================
# Stage 2: Dependencies Builder (Cached layer)
# ==================================================
FROM base AS dependencies

# Install build tools
RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential \
    git \
    && rm -rf /var/lib/apt/lists/*

# Copy only requirements (for Docker cache)
COPY apps/ai/requirements.txt .

# Install dependencies in virtual environment
RUN python -m venv /opt/venv && \
    /opt/venv/bin/pip install --upgrade pip wheel && \
    /opt/venv/bin/pip install -r requirements.txt

# ==================================================
# Stage 3: Development (Hot-reload)
# ==================================================
FROM dependencies AS development

ENV PATH="/opt/venv/bin:$PATH"

# Install dev tools
COPY apps/ai/requirements-dev.txt .
RUN /opt/venv/bin/pip install -r requirements-dev.txt

WORKDIR /app

# Create non-root user
RUN useradd -m -u 1000 sssp && \
    mkdir -p /app/data /app/cache && \
    chown -R sssp:sssp /app

USER sssp

EXPOSE 8001 50051

# Development command (hot-reload)
CMD ["uvicorn", "src.api.main:app", "--host", "0.0.0.0", "--port", "8001", "--reload"]

# ==================================================
# Stage 4: Production (Minimal runtime)
# ==================================================
FROM base AS production

# Copy only virtual environment (no build tools)
COPY --from=dependencies /opt/venv /opt/venv
ENV PATH="/opt/venv/bin:$PATH"

# Copy application code
COPY apps/ai/src /app/src
COPY packages/contracts/python /app/packages/contracts/python

# Create non-root user
RUN useradd -m -u 1000 sssp && \
    mkdir -p /app/data /app/cache && \
    chown -R sssp:sssp /app

USER sssp

EXPOSE 8001 50051

HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:8001/health || exit 1

# Production command (gunicorn)
CMD ["gunicorn", "src.api.main:app", \
     "--workers", "4", \
     "--worker-class", "uvicorn.workers.UvicornWorker", \
     "--bind", "0.0.0.0:8001", \
     "--timeout", "120", \
     "--access-logfile", "-"]

# ==================================================
# Stage 5: GPU Production (NVIDIA CUDA)
# ==================================================
FROM nvidia/cuda:13.0.2-runtime-ubuntu22.04 AS production-gpu

ENV PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1 \
    PIP_NO_CACHE_DIR=1 \
    PATH="/opt/venv/bin:$PATH"

WORKDIR /app

# Install Python + minimal deps
RUN apt-get update && apt-get install -y --no-install-recommends \
    python3.11 \
    python3-pip \
    curl \
    libgomp1 \
    libglib2.0-0 \
    && rm -rf /var/lib/apt/lists/* && \
    ln -s /usr/bin/python3.11 /usr/bin/python

# Copy virtual environment from builder
COPY --from=dependencies /opt/venv /opt/venv

# Copy application
COPY apps/ai/src /app/src
COPY packages/contracts/python /app/packages/contracts/python

RUN useradd -m -u 1000 sssp && \
    mkdir -p /app/data /app/cache && \
    chown -R sssp:sssp /app

USER sssp

EXPOSE 8001 50051

HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:8001/health || exit 1

ENV PYTHONPATH=/app

CMD ["uvicorn", "apps.ai.src.api.main:app", \
     "--workers", "2", \
     "--worker-class", "uvicorn.workers.UvicornWorker", \
     "--bind", "0.0.0.0:8001", \
     "--timeout", "180"]