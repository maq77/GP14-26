"""
apps/ai/src/api/routes/metrics.py
Exposes Prometheus-compatible metrics endpoint.
"""

from fastapi import APIRouter, Response
from prometheus_client import CONTENT_TYPE_LATEST
from ..metrics.registry import render_prometheus_metrics

router = APIRouter(tags=["Metrics"])

@router.get("/metrics")
async def metrics():
    """Prometheus scrape endpoint."""
    data = render_prometheus_metrics()
    return Response(content=data, media_type=CONTENT_TYPE_LATEST)
