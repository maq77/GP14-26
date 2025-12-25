# ==================================================
# SSSP AI Service - Multi-Stage (Dev / Prod / GPU)
# ==================================================

ARG PYTHON_VERSION=3.11

# ---------------------------
# Stage 1: base OS deps
# ---------------------------
FROM python:${PYTHON_VERSION}-slim AS base

ENV PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1 \
    PIP_NO_CACHE_DIR=1 \
    PIP_DISABLE_PIP_VERSION_CHECK=1 \
    DEBIAN_FRONTEND=noninteractive \
    # Make imports stable for "src.*" and "packages.*"
    PYTHONPATH="/app:/app/src:/app/packages/contracts/python"

WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    libgomp1 \
    libglib2.0-0 \
    libsm6 \
    libxext6 \
    libxrender1 \
    && rm -rf /var/lib/apt/lists/*

# ---------------------------
# Stage 2: dependencies (venv cached)
# ---------------------------
FROM base AS dependencies

RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential \
    git \
    && rm -rf /var/lib/apt/lists/*

COPY apps/ai/requirements.txt /app/requirements.txt

RUN python -m venv /opt/venv && \
    /opt/venv/bin/pip install --upgrade pip wheel && \
    /opt/venv/bin/pip install \
    --index-url https://download.pytorch.org/whl/cpu \ 
    -r /app/requirements.txt

# ---------------------------
# Stage 3: development (hot reload)
# - Optional libgl1 if you still use opencv-python (non-headless)
# ---------------------------
FROM dependencies AS development

ENV PATH="/opt/venv/bin:$PATH" \
    ENVIRONMENT=development

# (OPTIONAL) Only if you still use opencv-python (NOT headless)
# If you switched to opencv-python-headless, you can comment this block.
USER root
RUN apt-get update && apt-get install -y --no-install-recommends \
    libgl1 \
    && rm -rf /var/lib/apt/lists/*
# Create non-root user
RUN useradd -m -u 1000 sssp && \
    mkdir -p /app/data /app/cache && \
    chown -R sssp:sssp /app
USER sssp

EXPOSE 8001 50051
CMD ["uvicorn", "src.api.main:app", "--host", "0.0.0.0", "--port", "8001", "--reload"]

# ---------------------------
# Stage 4: production (minimal)
# ---------------------------
FROM base AS production

COPY --from=dependencies /opt/venv /opt/venv
ENV PATH="/opt/venv/bin:$PATH" \
    ENVIRONMENT=production

# Copy code + contracts
COPY apps/ai/src /app/src
COPY packages/contracts/python /app/packages/contracts/python

RUN useradd -m -u 1000 sssp && \
    mkdir -p /app/data /app/cache && \
    chown -R sssp:sssp /app

USER sssp

EXPOSE 8001 50051

HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
  CMD curl -fsS http://localhost:8001/api/v1/health/startup || exit 1

CMD ["uvicorn", "src.api.main:app", "--host", "0.0.0.0", "--port", "8001"]

# ---------------------------
# Stage 5: production-gpu (CUDA runtime)
# ---------------------------
FROM nvidia/cuda:13.1.0-runtime-ubuntu22.04 AS production-gpu

ENV PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1 \
    PIP_NO_CACHE_DIR=1 \
    DEBIAN_FRONTEND=noninteractive \
    PYTHONPATH="/app:/app/src:/app/packages/contracts/python" \
    PATH="/opt/venv/bin:$PATH" \
    ENVIRONMENT=production

WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends \
    python3.11 \
    python3-pip \
    curl \
    libgomp1 \
    libglib2.0-0 \
    libsm6 \
    libxext6 \
    libxrender1 \
    && rm -rf /var/lib/apt/lists/* && \
    ln -sf /usr/bin/python3.11 /usr/bin/python

COPY --from=dependencies /opt/venv /opt/venv
COPY apps/ai/src /app/src
COPY packages/contracts/python /app/packages/contracts/python

RUN useradd -m -u 1000 sssp && \
    mkdir -p /app/data /app/cache && \
    chown -R sssp:sssp /app

USER sssp

EXPOSE 8001 50051

HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
  CMD curl -fsS http://localhost:8001/api/v1/health/startup || exit 1

CMD ["uvicorn", "src.api.main:app", "--host", "0.0.0.0", "--port", "8001"]
