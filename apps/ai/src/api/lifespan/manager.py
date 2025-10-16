"""
apps/ai/src/api/lifespan/manager.py
Simple lifespan manager for FastAPI.

Responsibilities:
1. Load YOLO model on startup
2. Start gRPC server on startup
3. Stop gRPC server on shutdown
4. Unload model on shutdown

Clean and simple - no over-engineering.
"""

from contextlib import asynccontextmanager
import structlog

from ...core.config import settings
from ...models.object.model_loader import get_model_loader
from ..grpc.server import GRPCServer

logger = structlog.get_logger("lifespan")


@asynccontextmanager
async def lifespan(app):
    """
    FastAPI lifespan context manager.
    
    Startup:
    - Load YOLO detection model
    - Start gRPC server
    
    Shutdown:
    - Stop gRPC server
    - Unload model
    """
    
    # ========================================================================
    # STARTUP
    # ========================================================================
    
    logger.info(
        "application_starting",
        app_name=settings.APP_NAME,
        version=settings.APP_VERSION,
        environment=settings.ENVIRONMENT
    )
    
    # 1. Load Detection Model
    logger.info("loading_detection_model")
    try:
        model_loader = get_model_loader()
        model_loader.load_detector()
        logger.info("detection_model_loaded_successfully")
    except Exception as e:
        logger.error("failed_to_load_model", error=str(e), exc_info=True)
        raise  # Fail fast if model doesn't load
    
    # 2. Start gRPC Server
    logger.info("starting_grpc_server")
    grpc_server = GRPCServer()
    try:
        grpc_server.start()
        logger.info("grpc_server_started_successfully")
    except Exception as e:
        logger.error("failed_to_start_grpc_server", error=str(e), exc_info=True)
        raise  # Fail fast if gRPC doesn't start
    
    # Store in app state for access
    app.state.grpc_server = grpc_server
    app.state.model_loader = model_loader
    
    logger.info("startup_completed_successfully")
    
    # ========================================================================
    # APP RUNNING (yield control to FastAPI)
    # ========================================================================
    
    yield
    
    # ========================================================================
    # SHUTDOWN
    # ========================================================================
    
    logger.info("application_shutting_down")
    
    # 1. Stop gRPC Server
    logger.info("stopping_grpc_server")
    try:
        grpc_server.stop(grace_period=5)
        logger.info("grpc_server_stopped")
    except Exception as e:
        logger.error("error_stopping_grpc_server", error=str(e))
    
    # 2. Unload Model
    logger.info("unloading_model")
    try:
        model_loader.unload_detector()
        logger.info("model_unloaded")
    except Exception as e:
        logger.error("error_unloading_model", error=str(e))
    
    logger.info("shutdown_completed")


# ============================================================================
# Export
# ============================================================================

__all__ = ["lifespan"]