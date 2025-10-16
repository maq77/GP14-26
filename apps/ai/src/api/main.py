"""
apps/ai/src/api/main.py
Clean and simple FastAPI application entry point.

Architecture:
- Thin main.py (just app creation)
- Lifespan handles startup/shutdown
- Minimal middleware
- REST API for testing/debugging
- gRPC for production (.NET communication)
"""

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from ..core.config import settings
from ..core.logging import setup_logging, get_logger
from .lifespan.manager import lifespan
from .routes import health, detection

# Setup logging first
setup_logging()
logger = get_logger("main")


# ============================================================================
# Create Application
# ============================================================================

def create_app() -> FastAPI:
    """
    Create FastAPI application with minimal configuration.
    
    Returns:
        Configured FastAPI app
    """
    logger.info(
        "creating_app",
        name=settings.APP_NAME,
        version=settings.APP_VERSION,
        environment=settings.ENVIRONMENT
    )
    
    # Create app with lifespan
    app = FastAPI(
        title=settings.APP_NAME,
        version=settings.APP_VERSION,
        description="AI Inference Service for SSSP - REST API for testing, gRPC for production",
        lifespan=lifespan,  # ✅ Handles model loading + gRPC server
        
        # Docs (enabled even in production for testing)
        docs_url="/api/docs",
        redoc_url="/api/redoc",
        openapi_url="/api/openapi.json",
    )
    
    # CORS (allow all for development)
    app.add_middleware(
        CORSMiddleware,
        allow_origins=["*"],
        allow_credentials=True,
        allow_methods=["*"],
        allow_headers=["*"],
    )
    
    # Register routes
    app.include_router(health.router, tags=["Health"])
    app.include_router(detection.router, prefix="/api/v1", tags=["Detection"])
    
    logger.info("app_created_successfully")
    return app


# Create app instance
app = create_app()


# ============================================================================
# Root Endpoint
# ============================================================================

@app.get("/", tags=["Root"])
async def root():
    """Service info endpoint"""
    return {
        "service": settings.APP_NAME,
        "version": settings.APP_VERSION,
        "status": "running",
        "protocols": {
            "rest_api": f"http://localhost:{settings.API_PORT}/api/docs",
            "grpc": f"localhost:{settings.GRPC_PORT}"
        },
        "endpoints": {
            "health": "/health",
            "detection_test": "/api/v1/detect"
        }
    }


# ============================================================================
# CLI Entry Point
# ============================================================================

if __name__ == "__main__":
    import uvicorn
    
    logger.info(
        "starting_server",
        host=settings.API_HOST,
        port=settings.API_PORT
    )
    
    uvicorn.run(
        "apps.ai.src.api.main:app",
        host=settings.API_HOST,
        port=settings.API_PORT,
        reload=settings.DEBUG,
        log_level=settings.LOG_LEVEL.lower()
    )


# ============================================================================
# Exports
# ============================================================================

__all__ = ["app", "create_app"]