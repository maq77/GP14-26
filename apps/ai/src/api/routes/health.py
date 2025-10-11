from fastapi import APIRouter
from datetime import datetime, timezone

router = APIRouter()


@router.get("/health")
async def health_check():
    return {
        "status": "healthy",
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "service": "SSSP AI Service"
    }


@router.get("/ready")
async def readiness_check():
    # Add checks for model loading, DB connections, etc.
    return {
        "status": "ready",
        "models_loaded": True,
        "timestamp": datetime.now(timezone.utc).isoformat()
    }
