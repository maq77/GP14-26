# REPLACE your entire main.py with this corrected version:

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

import signal
import sys
import time
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

<<<<<<< HEAD
from src.core.config import settings
from src.core.logging import setup_logging, get_logger
from src.api.lifespan.manager import lifespan
from src.api.routes import health, detection
from src.api.lifespan.health_registry import get_health_registry
=======
from ..core.config import settings
from ..core.logging import setup_logging, get_logger
from .lifespan.manager import lifespan
from .routes import health, detection
from .lifespan.health_registry import get_health_registry
>>>>>>> main

# Setup logging first
setup_logging()
logger = get_logger("main")


# ============================================================================
# Signal Handlers for Graceful Shutdown (MOVED TO TOP)
# ============================================================================

def signal_handler(signum, frame):
    """Handle shutdown signals gracefully"""
    logger.warning(
        "shutdown_signal_received",
        signal=signal.Signals(signum).name
    )
    sys.exit(0)


# Register signal handlers at module level
signal.signal(signal.SIGINT, signal_handler)   # Ctrl+C
signal.signal(signal.SIGTERM, signal_handler)  # Docker/K8s termination
logger.info("signal_handlers_registered")


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
            "health_detailed": "/health/detailed",
            "metrics": "/metrics",
            "detection_test": "/api/v1/detect"
        }
    }


# ============================================================================
# Enhanced Health Endpoint
# ============================================================================

@app.get("/health/detailed", tags=["Health"])
async def detailed_health():
    """Comprehensive health check with component status"""
    
    health_registry = get_health_registry()
    health_summary = health_registry.get_health_summary()
    
    # Add service status details
    grpc_running = (
        hasattr(app.state, 'grpc_server') 
        and app.state.grpc_server is not None 
        and app.state.grpc_server.server is not None
    )
    
    face_models_loaded = (
        hasattr(app.state, 'face_service') 
        and app.state.face_service is not None
        and app.state.face_service.detector is not None
    )
    
    detection_model_loaded = (
        hasattr(app.state, 'model_loader') 
        and app.state.model_loader is not None
    )
    
    health_summary["services"] = {
        "grpc_server": {
            "status": "running" if grpc_running else "stopped",
            "host": settings.GRPC_HOST if grpc_running else None,
            "port": settings.GRPC_PORT if grpc_running else None,
        },
        "face_recognition": {
            "status": "loaded" if face_models_loaded else "not_loaded",
            "detector": "ready" if face_models_loaded else "not_ready",
            "embedder": "ready" if face_models_loaded else "not_ready",
        },
        "object_detection": {
            "status": "loaded" if detection_model_loaded else "not_loaded",
        }
    }
    
    # Add uptime if available
    if hasattr(app.state, 'startup_time'):
        uptime_seconds = time.time() - app.state.startup_time
        health_summary["uptime"] = {
            "seconds": round(uptime_seconds, 2),
            "human_readable": format_uptime(uptime_seconds)
        }
    
    return health_summary


# ============================================================================
# Metrics Endpoint (Prometheus-compatible)
# ============================================================================

@app.get("/metrics", tags=["Monitoring"])
async def get_metrics():
    """
    Prometheus-style metrics endpoint for monitoring
    """
    health_registry = get_health_registry()
    components = health_registry.get_all_health()
    
    metrics = []
    
    # Service uptime
    if hasattr(app.state, 'startup_time'):
        uptime = time.time() - app.state.startup_time
        metrics.append('# HELP service_uptime_seconds Service uptime in seconds')
        metrics.append('# TYPE service_uptime_seconds gauge')
        metrics.append(f'service_uptime_seconds {uptime:.2f}')
        metrics.append('')
    
    # Component health status (1=healthy, 0.5=degraded, 0=unhealthy)
    metrics.append('# HELP component_health Component health status')
    metrics.append('# TYPE component_health gauge')
    for name, health in components.items():
        if health.status.value == "healthy":
            status_value = 1.0
        elif health.status.value == "degraded":
            status_value = 0.5
        else:
            status_value = 0.0
        
        metrics.append(
            f'component_health{{component="{name}",status="{health.status.value}"}} {status_value}'
        )
    metrics.append('')
    
    # Component consecutive failures
    metrics.append('# HELP component_consecutive_failures Consecutive failure count')
    metrics.append('# TYPE component_consecutive_failures gauge')
    for name, health in components.items():
        metrics.append(
            f'component_consecutive_failures{{component="{name}"}} {health.consecutive_failures}'
        )
    metrics.append('')
    
    # Face service frame counter
    if hasattr(app.state, 'face_service') and hasattr(app.state.face_service, '_frame_counter'):
        frame_count = app.state.face_service._frame_counter
        metrics.append('# HELP frames_processed_total Total frames processed by face recognition')
        metrics.append('# TYPE frames_processed_total counter')
        metrics.append(f'frames_processed_total {frame_count}')
        metrics.append('')
    
    # gRPC server status
    grpc_running = (
        hasattr(app.state, 'grpc_server') 
        and app.state.grpc_server is not None 
        and app.state.grpc_server.server is not None
    )
    metrics.append('# HELP grpc_server_running gRPC server status (1=running, 0=stopped)')
    metrics.append('# TYPE grpc_server_running gauge')
    metrics.append(f'grpc_server_running {1 if grpc_running else 0}')
    metrics.append('')
    
    return "\n".join(metrics)


# ============================================================================
# Helper Functions
# ============================================================================

def format_uptime(seconds: float) -> str:
    """Format uptime in human-readable format"""
    days, remainder = divmod(int(seconds), 86400)
    hours, remainder = divmod(remainder, 3600)
    minutes, seconds = divmod(remainder, 60)
    
    parts = []
    if days > 0:
        parts.append(f"{days}d")
    if hours > 0:
        parts.append(f"{hours}h")
    if minutes > 0:
        parts.append(f"{minutes}m")
    parts.append(f"{seconds}s")
    
    return " ".join(parts)


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