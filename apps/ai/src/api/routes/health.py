"""
apps/ai/src/api/routes/health.py
Health check endpoints
"""

from fastapi import APIRouter, status
from pydantic import BaseModel
from typing import Dict, Any
import torch

from ...core.config import settings
from ...core.logging import get_logger
from ...models.object.model_loader import get_model_loader

logger = get_logger("health")

router = APIRouter(tags=["Health"])


# ============================================================================
# Response Models
# ============================================================================

class HealthResponse(BaseModel):
    """Health check response"""
    status: str
    service: str
    version: str
    environment: str
    model_loaded: bool
    device: str


class DetailedHealthResponse(BaseModel):
    """Detailed health check response"""
    status: str
    service: str
    version: str
    environment: str
    model: Dict[str, Any]
    system: Dict[str, Any]


# ============================================================================
# Endpoints
# ============================================================================

@router.get(
    "/health",
    response_model=HealthResponse,
    status_code=status.HTTP_200_OK,
    summary="Basic health check"
)
async def health_check():
    """
    Basic health check endpoint
    Returns service status and model status
    """
    try:
        model_loader = get_model_loader()
        is_loaded = model_loader.is_loaded()
        
        return HealthResponse(
            status="healthy",
            service=settings.APP_NAME,
            version=settings.APP_VERSION,
            environment=settings.ENVIRONMENT,
            model_loaded=is_loaded,
            device=settings.DETECTION_DEVICE
        )
    except Exception as e:
        logger.error("health_check_failed", error=str(e))
        return HealthResponse(
            status="unhealthy",
            service=settings.APP_NAME,
            version=settings.APP_VERSION,
            environment=settings.ENVIRONMENT,
            model_loaded=False,
            device="unknown"
        )


@router.get(
    "/health/detailed",
    response_model=DetailedHealthResponse,
    status_code=status.HTTP_200_OK,
    summary="Detailed health check"
)
async def detailed_health_check():
    """
    Detailed health check endpoint
    Returns comprehensive system and model information
    """
    try:
        model_loader = get_model_loader()
        model_info = model_loader.get_model_info()
        
        # System info
        system_info = {
            "cuda_available": torch.cuda.is_available(),
            "cuda_version": torch.version.cuda if torch.cuda.is_available() else None,
            "device_count": torch.cuda.device_count() if torch.cuda.is_available() else 0,
            "pytorch_version": torch.__version__,
        }
        
        # GPU info if available
        if torch.cuda.is_available():
            system_info["gpu_name"] = torch.cuda.get_device_name(0)
            system_info["gpu_memory_total_gb"] = round(
                torch.cuda.get_device_properties(0).total_memory / 1024**3, 2
            )
        
        return DetailedHealthResponse(
            status="healthy",
            service=settings.APP_NAME,
            version=settings.APP_VERSION,
            environment=settings.ENVIRONMENT,
            model=model_info,
            system=system_info
        )
        
    except Exception as e:
        logger.error("detailed_health_check_failed", error=str(e), exc_info=True)
        return DetailedHealthResponse(
            status="unhealthy",
            service=settings.APP_NAME,
            version=settings.APP_VERSION,
            environment=settings.ENVIRONMENT,
            model={"is_loaded": False, "error": str(e)},
            system={"error": str(e)}
        )


@router.get(
    "/ready",
    status_code=status.HTTP_200_OK,
    summary="Readiness probe"
)
async def readiness_check():
    """
    Kubernetes readiness probe
    Returns 200 if service is ready to accept requests
    """
    try:
        model_loader = get_model_loader()
        if not model_loader.is_loaded():
            return {"ready": False, "reason": "Model not loaded"}
        
        return {"ready": True}
        
    except Exception as e:
        logger.error("readiness_check_failed", error=str(e))
        return {"ready": False, "reason": str(e)}


@router.get(
    "/live",
    status_code=status.HTTP_200_OK,
    summary="Liveness probe"
)
async def liveness_check():
    """
    Kubernetes liveness probe
    Returns 200 if service is alive
    """
    return {"alive": True}


# ============================================================================
# Export
# ============================================================================

__all__ = ["router"]