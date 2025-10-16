"""
apps/ai/src/api/main.py

Production-grade FastAPI application entry point.
Follows Netflix/Google microservice patterns with proper separation of concerns.

Architecture:
- Application factory pattern (create_app)
- Centralized middleware configuration
- Router auto-registration
- Environment-based configuration
- Production-ready settings
"""
import sys
from typing import Optional
from fastapi import FastAPI, Request, status
from fastapi.middleware.cors import CORSMiddleware
from fastapi.middleware.gzip import GZipMiddleware
from fastapi.middleware.trustedhost import TrustedHostMiddleware
from fastapi.responses import JSONResponse
from fastapi.exceptions import RequestValidationError
from starlette.exceptions import HTTPException as StarletteHTTPException
import structlog

# Core imports
from ..core.config import settings
from ..core.logging import setup_logging, get_logger
from ..core.exceptions import AIServiceException

# Lifecycle management
from .lifespan.manager import lifespan

# Import modules to trigger @register_component decorators
from .lifespan.modules import (
    DetectionModelComponent,
    GRPCServerComponent,
    # RabbitMQComponent,
)

# Router imports
from .routes import (
    health,
    detection,
    metrics,
)

# Initialize structured logging FIRST
setup_logging()
logger = get_logger("main")


# ============================================================================
# Exception Handlers (Global Error Handling)
# ============================================================================

async def validation_exception_handler(
    request: Request,
    exc: RequestValidationError
) -> JSONResponse:
    """
    Handle request validation errors with detailed logging.
    Returns 422 with error details.
    """
    logger.warning(
        "validation_error",
        path=request.url.path,
        method=request.method,
        errors=exc.errors(),
        client=request.client.host if request.client else None
    )
    
    return JSONResponse(
        status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
        content={
            "error": "Validation Error",
            "detail": exc.errors(),
            "path": request.url.path
        }
    )


async def http_exception_handler(
    request: Request,
    exc: StarletteHTTPException
) -> JSONResponse:
    """
    Handle HTTP exceptions with consistent format.
    """
    logger.warning(
        "http_exception",
        status_code=exc.status_code,
        detail=exc.detail,
        path=request.url.path,
        method=request.method
    )
    
    return JSONResponse(
        status_code=exc.status_code,
        content={
            "error": exc.detail,
            "status_code": exc.status_code,
            "path": request.url.path
        }
    )


async def ai_service_exception_handler(
    request: Request,
    exc: AIServiceException
) -> JSONResponse:
    """
    Handle custom AI service exceptions.
    """
    logger.error(
        "ai_service_error",
        error=str(exc),
        error_type=type(exc).__name__,
        path=request.url.path,
        method=request.method,
        exc_info=True
    )
    
    return JSONResponse(
        status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
        content={
            "error": "AI Service Error",
            "detail": str(exc),
            "type": type(exc).__name__,
            "path": request.url.path
        }
    )


async def generic_exception_handler(
    request: Request,
    exc: Exception
) -> JSONResponse:
    """
    Catch-all exception handler for unexpected errors.
    """
    logger.critical(
        "unhandled_exception",
        error=str(exc),
        error_type=type(exc).__name__,
        path=request.url.path,
        method=request.method,
        exc_info=True
    )
    
    # Don't expose internal errors in production
    detail = str(exc) if settings.DEBUG else "Internal server error"
    
    return JSONResponse(
        status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
        content={
            "error": "Internal Server Error",
            "detail": detail,
            "path": request.url.path
        }
    )


# ============================================================================
# Middleware Configuration
# ============================================================================

def configure_middleware(app: FastAPI) -> None:
    """
    Configure all middleware in correct order.
    
    Order matters! Middleware is applied in reverse order of addition.
    First added = outermost layer (processes request first, response last)
    """
    
    # 1. Trusted Host (Security - prevent host header attacks)
    if not settings.DEBUG:
        allowed_hosts = getattr(settings, 'ALLOWED_HOSTS', ['*'])
        if allowed_hosts and allowed_hosts != ['*']:
            app.add_middleware(
                TrustedHostMiddleware,
                allowed_hosts=allowed_hosts
            )
            logger.info("trusted_host_middleware_enabled", hosts=allowed_hosts)
    
    # 2. CORS (Cross-Origin Resource Sharing)
    cors_origins = getattr(settings, 'CORS_ORIGINS', ['*'])
    app.add_middleware(
        CORSMiddleware,
        allow_origins=cors_origins,
        allow_credentials=True,
        allow_methods=["*"],
        allow_headers=["*"],
        expose_headers=["X-Request-ID"],
    )
    logger.info("cors_middleware_enabled", origins=cors_origins)
    
    # 3. GZip Compression (reduce bandwidth)
    app.add_middleware(
        GZipMiddleware,
        minimum_size=1000,  # Only compress responses > 1KB
        compresslevel=6  # Balance between speed and compression
    )
    logger.info("gzip_middleware_enabled")
    
    # 4. Request ID Middleware (for tracing)
    @app.middleware("http")
    async def add_request_id_middleware(request: Request, call_next):
        """Add unique request ID for tracing."""
        import uuid
        request_id = str(uuid.uuid4())
        
        # Add to request state
        request.state.request_id = request_id
        
        # Log request
        logger.info(
            "request_received",
            request_id=request_id,
            method=request.method,
            path=request.url.path,
            client=request.client.host if request.client else None
        )
        
        # Process request
        response = await call_next(request)
        
        # Add to response headers
        response.headers["X-Request-ID"] = request_id
        
        # Log response
        logger.info(
            "request_completed",
            request_id=request_id,
            status_code=response.status_code
        )
        
        return response
    
    # 5. Request Timing Middleware
    @app.middleware("http")
    async def add_timing_middleware(request: Request, call_next):
        """Measure request processing time."""
        import time
        
        start_time = time.perf_counter()
        response = await call_next(request)
        process_time = time.perf_counter() - start_time
        
        # Add timing header
        response.headers["X-Process-Time"] = f"{process_time:.4f}"
        
        # Log slow requests
        if process_time > 1.0:  # > 1 second
            logger.warning(
                "slow_request",
                path=request.url.path,
                method=request.method,
                process_time=process_time
            )
        
        return response


# ============================================================================
# Router Registration
# ============================================================================

def register_routers(app: FastAPI) -> None:
    """
    Register all API routers.
    
    Organization:
    - Health/monitoring endpoints (no prefix)
    - API v1 endpoints (/api/v1)
    - Future versions (/api/v2, etc.)
    """
    
    # Health endpoints (for load balancers & k8s probes)
    app.include_router(
        health.router,
        tags=["health", "monitoring"]
    )
    logger.debug("router_registered", router="health")
    
    # Metrics endpoint (for Prometheus)
    app.include_router(
        metrics.router,
        tags=["metrics", "monitoring"]
    )
    logger.debug("router_registered", router="metrics")
    
    # API v1 routes
    api_v1_routers = [
        (detection.router, "detection"),
        # Add more v1 routers here
        # (face.router, "face"),
        # (behavior.router, "behavior"),
    ]
    
    for router, name in api_v1_routers:
        app.include_router(
            router,
            prefix="/api/v1",
            tags=["v1", name]
        )
        logger.debug("router_registered", router=name, version="v1")
    
    logger.info(
        "all_routers_registered",
        total=len(api_v1_routers) + 2  # +2 for health and metrics
    )


# ============================================================================
# Event Handlers
# ============================================================================

def register_event_handlers(app: FastAPI) -> None:
    """
    Register startup and shutdown event handlers.
    
    Note: Most lifecycle logic is in lifespan context manager.
    These are for additional application-level events.
    """
    
    @app.on_event("startup")
    async def on_startup():
        """Additional startup tasks (after lifespan startup)."""
        logger.info(
            "fastapi_app_ready",
            app_name=app.title,
            version=app.version,
            docs_url=app.docs_url,
            openapi_url=app.openapi_url
        )
    
    @app.on_event("shutdown")
    async def on_shutdown():
        """Additional shutdown tasks (before lifespan shutdown)."""
        logger.info("fastapi_app_shutdown_initiated")


# ============================================================================
# Application Factory
# ============================================================================

def create_app() -> FastAPI:
    """
    Application factory pattern.
    
    Creates and configures the FastAPI application with all
    middleware, routers, exception handlers, and settings.
    
    Benefits:
    - Testability (can create multiple app instances)
    - Configurability (different settings per environment)
    - Clean separation of concerns
    
    Returns:
        Configured FastAPI application
    """
    
    logger.info(
        "creating_application",
        environment=settings.ENVIRONMENT,
        debug=settings.DEBUG,
        app_name=settings.APP_NAME,
        version=settings.APP_VERSION
    )
    
    # Create FastAPI app with enterprise settings
    app = FastAPI(
        title=settings.APP_NAME,
        version=settings.APP_VERSION,
        description=(
            "Enterprise AI Inference Service for SSSP Platform\n\n"
            "Features:\n"
            "- Object Detection (YOLO)\n"
            "- Face Recognition\n"
            "- Behavior Analysis\n"
            "- Air Quality Index (AQI)\n\n"
            "Protocols: REST API + gRPC"
        ),
        lifespan=lifespan,  # ✅ Enterprise lifecycle management
        
        # API Documentation
        docs_url="/api/docs" if settings.DEBUG else None,
        redoc_url="/api/redoc" if settings.DEBUG else None,
        openapi_url="/api/openapi.json",
        
        # OpenAPI metadata
        contact={
            "name": "SSSP Development Team",
            "url": "https://github.com/maq77/GP14-26",
            "email": "support@sssp.example.com",
        },
        license_info={
            "name": "Proprietary",
            "url": "https://example.com/license",
        },
        
        # API versioning
        #version=settings.APP_VERSION,
        
        # Response configuration
        default_response_class=JSONResponse,
        
        # Security
        swagger_ui_parameters={
            "persistAuthorization": True,
            "displayRequestDuration": True,
        } if settings.DEBUG else None
    )
    
    # Configure middleware
    configure_middleware(app)
    
    # Register exception handlers
    app.add_exception_handler(RequestValidationError, validation_exception_handler)
    app.add_exception_handler(StarletteHTTPException, http_exception_handler)
    app.add_exception_handler(AIServiceException, ai_service_exception_handler)
    app.add_exception_handler(Exception, generic_exception_handler)
    logger.debug("exception_handlers_registered")
    
    # Register routers
    register_routers(app)
    
    # Register event handlers
    register_event_handlers(app)
    
    logger.info(
        "application_created_successfully",
        environment=settings.ENVIRONMENT,
        debug=settings.DEBUG
    )
    
    return app


# ============================================================================
# Create Application Instance
# ============================================================================

app = create_app()


# ============================================================================
# Root Endpoint (Service Info)
# ============================================================================

@app.get(
    "/",
    tags=["root"],
    summary="Service information",
    response_model=dict
)
async def root():
    """
    Root endpoint providing service information and available endpoints.
    
    Useful for:
    - Service discovery
    - Health monitoring
    - API exploration
    """
    return {
        "service": settings.APP_NAME,
        "version": settings.APP_VERSION,
        "environment": settings.ENVIRONMENT,
        "status": "operational",
        "protocols": {
            "rest": {
                "host": f"{settings.API_HOST}:{settings.API_PORT}",
                "docs": f"http://{settings.API_HOST}:{settings.API_PORT}/api/docs" if settings.DEBUG else None
            },
            "grpc": {
                "host": f"{settings.GRPC_HOST}:{settings.GRPC_PORT}",
                "reflection": getattr(settings, 'GRPC_REFLECTION', False)
            }
        },
        "endpoints": {
            "health": "/api/v1/health",
            "metrics": "/metrics",
            "detection": "/api/v1/detect",
            "documentation": "/api/docs" if settings.DEBUG else None
        },
        "features": [
            "Object Detection",
            "Face Recognition",
            "Behavior Analysis",
            "Air Quality Index"
        ]
    }


# ============================================================================
# CLI Entry Point (Production)
# ============================================================================

def run_production():
    """
    Run application in production mode with Gunicorn + Uvicorn workers.
    
    Usage:
        gunicorn apps.ai.src.api.main:app \
            --worker-class uvicorn.workers.UvicornWorker \
            --workers 4 \
            --bind 0.0.0.0:8000 \
            --timeout 120 \
            --graceful-timeout 30 \
            --access-logfile - \
            --error-logfile -
    """
    pass  # Gunicorn handles this


def run_development():
    """
    Run application in development mode with auto-reload.
    
    Usage:
        python -m apps.ai.src.api.main
    """
    import uvicorn
    
    logger.info(
        "starting_development_server",
        host=settings.API_HOST,
        port=settings.API_PORT,
        reload=settings.DEBUG
    )
    
    uvicorn.run(
        "apps.ai.src.api.main:app",
        host=settings.API_HOST,
        port=settings.API_PORT,
        reload=settings.DEBUG,
        log_config=None,  # Use structlog
        access_log=False,  # Custom logging middleware
    )


# ============================================================================
# Main Entry Point
# ============================================================================

if __name__ == "__main__":
    """
    Development entry point.
    
    For production, use Gunicorn:
        gunicorn apps.ai.src.api.main:app \
            --worker-class uvicorn.workers.UvicornWorker \
            --workers 4
    """
    try:
        run_development()
    except KeyboardInterrupt:
        logger.info("received_keyboard_interrupt")
        sys.exit(0)
    except Exception as e:
        logger.critical(
            "application_startup_failed",
            error=str(e),
            exc_info=True
        )
        sys.exit(1)


# ============================================================================
# Exports
# ============================================================================

__all__ = [
    "app",
    "create_app",
    "run_production",
    "run_development",
]
