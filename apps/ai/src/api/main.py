"""
apps/ai/src/api/main.py
Main Application Entry Point
Runs both FastAPI (for testing) and gRPC server
"""

import asyncio
import signal
import sys
from contextlib import asynccontextmanager
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from ..core.config import settings
from ..core.logging import setup_logging, get_logger
from ..models.object.model_loader import get_model_loader
from .grpc.server import GRPCServer
from .routes.health import router as health_router
from .routes.detection import router as detection_router  # Optional, for REST testing

# Setup logging first
setup_logging()
logger = get_logger("main")


# ============================================================================
# Application Lifespan
# ============================================================================

@asynccontextmanager
async def lifespan(app: FastAPI):
    """
    Application lifespan manager
    Handles startup and shutdown events
    """
    # Startup
    logger.info(
        "application_starting",
        app_name=settings.APP_NAME,
        version=settings.APP_VERSION,
        environment=settings.ENVIRONMENT
    )
    
    # Load detection model
    try:
        logger.info("loading_detection_model_on_startup")
        model_loader = get_model_loader()
        model_loader.load_detector()
        logger.info("detection_model_loaded_successfully")
    except Exception as e:
        logger.error("failed_to_load_model_on_startup", error=str(e), exc_info=True)
        # Don't fail startup, model can be loaded on first request
    
    # Start gRPC server in background
    grpc_server = GRPCServer()
    
    try:
        logger.info("starting_grpc_server_in_background")
        grpc_server.start()
        logger.info("grpc_server_started_successfully")
    except Exception as e:
        logger.error("failed_to_start_grpc_server", error=str(e), exc_info=True)
        raise
    
    logger.info("application_startup_completed")
    
    yield  # Application runs here
    
    # Shutdown
    logger.info("application_shutting_down")
    
    # Stop gRPC server
    try:
        grpc_server.stop(grace_period=5)
        logger.info("grpc_server_stopped")
    except Exception as e:
        logger.error("error_stopping_grpc_server", error=str(e))
    
    # Unload model
    try:
        model_loader = get_model_loader()
        model_loader.unload_detector()
        logger.info("model_unloaded")
    except Exception as e:
        logger.error("error_unloading_model", error=str(e))
    
    logger.info("application_shutdown_completed")


# ============================================================================
# FastAPI Application
# ============================================================================

app = FastAPI(
    title=settings.APP_NAME,
    version=settings.APP_VERSION,
    description="AI Inference Service for SSSP Platform",
    lifespan=lifespan,
    docs_url="/docs",          # always enable Swagger UI
    redoc_url="/redoc",        # always enable ReDoc
    #docs_url="/docs" if settings.DEBUG else None,
    #redoc_url="/redoc" if settings.DEBUG else None
)

# ============================================================================
# Middleware
# ============================================================================

# CORS (for development/testing)
if settings.DEBUG:
    app.add_middleware(
        CORSMiddleware,
        allow_origins=["*"],
        allow_credentials=True,
        allow_methods=["*"],
        allow_headers=["*"],
    )

# ============================================================================
# Routes
# ============================================================================

# Health check endpoint
app.include_router(health_router)

# TODO: Add other REST routes for testing (optional)
app.include_router(detection_router, prefix="/api/v1")

# ============================================================================
# Root Endpoint
# ============================================================================

@app.get("/")
async def root():
    """Root endpoint - Service info"""
    return {
        "service": settings.APP_NAME,
        "version": settings.APP_VERSION,
        "environment": settings.ENVIRONMENT,
        "status": "running",
        "grpc_port": settings.GRPC_PORT,
        "api_port": settings.API_PORT
    }


# ============================================================================
# Signal Handlers
# ============================================================================

def signal_handler(sig, frame):
    """Handle SIGINT and SIGTERM"""
    logger.info("received_shutdown_signal", signal=sig)
    sys.exit(0)


signal.signal(signal.SIGINT, signal_handler)
signal.signal(signal.SIGTERM, signal_handler)


# ============================================================================
# Main Entry Point
# ============================================================================

if __name__ == "__main__":
    import uvicorn
    
    logger.info(
        "starting_application",
        host=settings.API_HOST,
        port=settings.API_PORT,
        #workers=settings.API_WORKERS
    )
    
    uvicorn.run(
        "apps.ai.src.api.main:app",
        host=settings.API_HOST,
        port=settings.API_PORT,
        #workers=settings.API_WORKERS,
        log_level=settings.LOG_LEVEL.lower(),
        reload=settings.DEBUG
    )


# ============================================================================
# Export
# ============================================================================

__all__ = ["app"]