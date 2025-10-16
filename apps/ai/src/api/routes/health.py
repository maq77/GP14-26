"""Health check endpoints for monitoring and observability."""
from fastapi import APIRouter, Request, status
from fastapi.responses import JSONResponse
from typing import Dict, Any
import structlog

from ..lifespan.health_registry import get_health_registry, HealthStatus

router = APIRouter(prefix="/api/v1/health", tags=["health"])
logger = structlog.get_logger(__name__)


@router.get("", response_model=Dict[str, Any], summary="System health check")
@router.get("/", include_in_schema=False)
async def health_check(request: Request):
    """
    Comprehensive system health check.
    
    Returns:
    - overall_status: Overall system health (healthy/degraded/unhealthy)
    - timestamp: Current timestamp
    - components: Detailed status of each component
    - summary: Component count by status
    
    Status Codes:
    - 200: All components healthy
    - 503: One or more components unhealthy or degraded
    """
    health_registry = get_health_registry()
    health_summary = health_registry.get_health_summary()
    
    overall_status = health_summary["overall_status"]
    
    # Return 503 if system is degraded or unhealthy
    status_code = status.HTTP_200_OK
    if overall_status in ("degraded", "unhealthy"):
        status_code = status.HTTP_503_SERVICE_UNAVAILABLE
    
    return JSONResponse(
        status_code=status_code,
        content=health_summary
    )


@router.get("/live", summary="Kubernetes liveness probe")
async def liveness():
    """
    Liveness probe for Kubernetes.
    
    Returns 200 if the application process is running.
    Does not check component health - only process health.
    
    Use this for Kubernetes liveness probes.
    """
    return {"status": "alive", "probe": "liveness"}


@router.get("/ready", summary="Kubernetes readiness probe")
async def readiness():
    """
    Readiness probe for Kubernetes.
    
    Returns:
    - 200: Application is ready to serve traffic
    - 503: Application is starting up or degraded
    
    Use this for Kubernetes readiness probes.
    """
    health_registry = get_health_registry()
    overall_status = health_registry.get_overall_status()
    
    # Ready only if all components are healthy
    if overall_status == HealthStatus.HEALTHY:
        return JSONResponse(
            status_code=status.HTTP_200_OK,
            content={
                "status": "ready",
                "probe": "readiness"
            }
        )
    else:
        return JSONResponse(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            content={
                "status": "not_ready",
                "probe": "readiness",
                "reason": overall_status.value
            }
        )


@router.get("/startup", summary="Kubernetes startup probe")
async def startup():
    """
    Startup probe for Kubernetes.
    
    Returns:
    - 200: Application has completed startup
    - 503: Application is still starting
    
    Use this for Kubernetes startup probes to avoid killing
    the container during slow model loading.
    """
    health_registry = get_health_registry()
    overall_status = health_registry.get_overall_status()
    
    # Startup complete if status is not UNKNOWN
    if overall_status != HealthStatus.UNKNOWN:
        return JSONResponse(
            status_code=status.HTTP_200_OK,
            content={
                "status": "started",
                "probe": "startup"
            }
        )
    else:
        return JSONResponse(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            content={
                "status": "starting",
                "probe": "startup"
            }
        )


@router.get("/components/{component_name}", summary="Component-specific health")
async def component_health(component_name: str):
    """
    Get health status for a specific component.
    
    Args:
        component_name: Name of the component (e.g., "DetectionModel", "GRPCServer")
    
    Returns:
        Component health details or 404 if not found
    """
    health_registry = get_health_registry()
    component = health_registry.get_component_health(component_name)
    
    if not component:
        return JSONResponse(
            status_code=status.HTTP_404_NOT_FOUND,
            content={
                "error": "Component not found",
                "component": component_name
            }
        )
    
    return component.to_dict()


@router.get("/metrics", summary="Startup and performance metrics")
async def metrics(request: Request):
    """
    Get application startup and performance metrics.
    
    Includes:
    - Startup duration
    - Component success/failure counts
    - Individual component metrics
    """
    try:
        manager = request.app.state.lifespan_manager
        
        startup_metrics = manager.get_startup_metrics()
        
        # Get component-specific metrics
        component_metrics = {}
        for component in manager.components:
            component_metrics[component.name] = component.get_metrics()
        
        return {
            "startup": startup_metrics,
            "components": component_metrics
        }
        
    except AttributeError:
        return JSONResponse(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            content={
                "error": "Lifespan manager not available",
                "message": "Application may still be starting up"
            }
        )

