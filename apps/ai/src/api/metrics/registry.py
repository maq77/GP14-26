"""
apps/ai/src/api/metrics/registry.py
Central Prometheus metrics registry for the AI service.
"""

from prometheus_client import (
    CollectorRegistry, Counter, Gauge, Histogram, generate_latest
)
import psutil
import torch
import time

# Global registry instance
REGISTRY = CollectorRegistry(auto_describe=True)

# =============================
# Metric definitions
# =============================
REQUEST_COUNT = Counter(
    "ai_inference_requests_total",
    "Total number of inference requests processed",
    ["model", "status"],
    registry=REGISTRY,
)

INFERENCE_LATENCY = Histogram(
    "ai_inference_latency_seconds",
    "Inference latency per model (seconds)",
    ["model"],
    buckets=(0.01, 0.05, 0.1, 0.25, 0.5, 1, 2, 5),
    registry=REGISTRY,
)

CPU_USAGE = Gauge(
    "ai_system_cpu_usage_percent",
    "Current CPU utilization percentage",
    registry=REGISTRY,
)

MEMORY_USAGE = Gauge(
    "ai_system_memory_usage_percent",
    "Current memory utilization percentage",
    registry=REGISTRY,
)

GPU_USAGE = Gauge(
    "ai_system_gpu_usage_percent",
    "Current GPU utilization percentage (0 if unavailable)",
    registry=REGISTRY,
)

MODEL_LOADED = Gauge(
    "ai_model_loaded_state",
    "1 if model is loaded successfully, else 0",
    ["model"],
    registry=REGISTRY,
)

# =============================
# Updater helpers
# =============================

def update_system_metrics():
    """Refresh system resource gauges."""
    CPU_USAGE.set(psutil.cpu_percent(interval=0.2))
    MEMORY_USAGE.set(psutil.virtual_memory().percent)
    if torch.cuda.is_available():
        try:
            torch.cuda.synchronize()
            mem_info = torch.cuda.mem_get_info()
            used_percent = 100 * (1 - mem_info[0] / mem_info[1])
            GPU_USAGE.set(round(used_percent, 2))
        except Exception:
            GPU_USAGE.set(0)
    else:
        GPU_USAGE.set(0)


def track_inference(model_name: str, latency: float, success: bool = True):
    """Record inference count and latency."""
    REQUEST_COUNT.labels(model=model_name, status="success" if success else "failed").inc()
    INFERENCE_LATENCY.labels(model=model_name).observe(latency)


def mark_model_loaded(model_name: str, loaded: bool):
    """Set model load state gauge."""
    MODEL_LOADED.labels(model=model_name).set(1 if loaded else 0)


def render_prometheus_metrics():
    """Return text for Prometheus scrape endpoint."""
    update_system_metrics()
    return generate_latest(REGISTRY)
