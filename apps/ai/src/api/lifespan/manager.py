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
from ...services.ml.Face_Recognition_Service import FaceRecognitionService
from ..grpc.server import GRPCServer

logger = structlog.get_logger("lifespan")


@asynccontextmanager
async def lifespan(app):
    """
    FastAPI lifespan context manager.
    
    Startup:
    - Load YOLO detection model
    - load face recognition model
    - Start gRPC server
    
    Shutdown:
    - Stop gRPC server
    - Unload models
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
    
    # Load Face Recognition Service
    logger.info("initializing_face_recognition_service")
    try:        
        face_service = FaceRecognitionService()
        
        # Warmup models to avoid cold start latency
        logger.info("warming_up_face_models")
        face_service.warmup()  # Call the warmup method we added earlier
        
        logger.info(
            "face_service_ready",
            detector_loaded=face_service.detector is not None,
            embedder_loaded=face_service.embedder is not None
        )
    except Exception as e:
        logger.error("failed_to_initialize_face_service", error=str(e), exc_info=True)
        raise  # Fail fast if face models don't load

    # 2. Start gRPC Server
    logger.info("starting_grpc_server")
    try:
        grpc_server = GRPCServer(face_service=face_service)  # Pass initialized service
        grpc_server.start()
        logger.info("grpc_server_started_successfully")
    except Exception as e:
        logger.error("failed_to_start_grpc_server", error=str(e), exc_info=True)
        raise  # Fail fast if gRPC doesn't start
    
    # Store in app state for access
    app.state.grpc_server = grpc_server
    app.state.model_loader = model_loader
    app.state.face_service = face_service
    
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
    
    # 2. Cleanup Face Service (NEW!)
    logger.info("cleaning_up_face_service")
    try:
        if hasattr(app.state, 'face_service'):
            app.state.face_service.cleanup()  # Call cleanup method we added
            logger.info("face_service_cleaned_up")
    except Exception as e:
        logger.error("error_cleaning_face_service", error=str(e))

    # 3. Unload Detection Model
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