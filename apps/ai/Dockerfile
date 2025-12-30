# ---------------------------
# Stage 1: base OS deps for AI Service
# ---------------------------
FROM python:3.11-slim AS base

ENV PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1 \
    PIP_NO_CACHE_DIR=1 \
    PIP_DISABLE_PIP_VERSION_CHECK=1 \
    DEBIAN_FRONTEND=noninteractive \
    PYTHONPATH="/app:/app/src:/app/packages/contracts/python"

WORKDIR /app

# Install system dependencies including libGL & X libs for full OpenCV
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    build-essential \
    git \
    libgomp1 \
    libglib2.0-0 \
    libsm6 \
    libxext6 \
    libxrender1 \
    libgl1 \
    ffmpeg \
    && rm -rf /var/lib/apt/lists/*

# ---------------------------
# Stage 2: dependencies (venv)
# ---------------------------
FROM base AS dependencies

COPY apps/ai/requirements.txt /app/requirements.txt

RUN python -m venv /opt/venv && \
    /opt/venv/bin/pip install --upgrade pip wheel

# Install PyTorch and other dependencies
RUN /opt/venv/bin/pip install \
    --index-url https://download.pytorch.org/whl/cpu \
    torch==2.9.1+cpu \
    torchvision==0.24.1+cpu || true

# Install remaining dependencies from PyPI
RUN /opt/venv/bin/pip install -r /app/requirements.txt

# ---------------------------
# Stage 3: development
# ---------------------------
FROM dependencies AS development

ENV PATH="/opt/venv/bin:$PATH" \
    ENVIRONMENT=development

# Non-root user
RUN useradd -m -u 1000 sssp && \
    mkdir -p /app/data /app/cache && \
    chown -R sssp:sssp /app
USER sssp

EXPOSE 8001 50051

CMD ["uvicorn", "src.api.main:app", "--host", "0.0.0.0", "--port", "8001", "--reload"]

# ---------------------------
# Stage 4: production (CPU)
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
