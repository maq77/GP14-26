FROM python:3.11-slim AS base
ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1

WORKDIR /app
# system deps (add ffmpeg if OpenCV needs it)
RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential curl && \
    rm -rf /var/lib/apt/lists/*

# requirements first for layer caching
COPY requirements.txt .
RUN pip install -r requirements.txt

# app code
COPY . .

# compile protobufs
RUN python -m grpc_tools.protoc -Iproto \
    --python_out=. --grpc_python_out=. proto/inference.proto

# expose FastAPI (8000) + gRPC (50051)
EXPOSE 8000 50051

# (optional) healthcheck via FastAPI
HEALTHCHECK --interval=30s --timeout=3s \
  CMD curl --fail http://localhost:8000/health || exit 1

# run FastAPI and gRPC in one container
# (simple approach; for prod you might use supervisord)
CMD sh -c "uvicorn main:app --host 0.0.0.0 --port 8000 & python grpc_server.py"
