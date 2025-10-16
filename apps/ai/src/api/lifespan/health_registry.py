"""Health monitoring and status tracking for all components."""
from typing import Dict, Optional, Any
from datetime import datetime
from enum import Enum
from dataclasses import dataclass, field
from threading import RLock
import structlog

logger = structlog.get_logger(__name__)


class HealthStatus(Enum):
    """Component health status levels."""
    HEALTHY = "healthy"          # Fully operational
    DEGRADED = "degraded"        # Partially functional
    UNHEALTHY = "unhealthy"      # Not functional
    UNKNOWN = "unknown"          # Status not yet determined


@dataclass
class ComponentHealth:
    """Health information for a single component."""
    name: str
    status: HealthStatus
    last_check: datetime = field(default_factory=datetime.utcnow)
    error_message: Optional[str] = None
    metadata: Dict[str, Any] = field(default_factory=dict)
    consecutive_failures: int = 0
    total_checks: int = 0
    
    def to_dict(self) -> Dict[str, Any]:
        """Serialize to dictionary for API responses."""
        return {
            "name": self.name,
            "status": self.status.value,
            "last_check": self.last_check.isoformat(),
            "error_message": self.error_message,
            "metadata": self.metadata,
            "consecutive_failures": self.consecutive_failures,
            "total_checks": self.total_checks,
        }


class HealthRegistry:
    """
    Thread-safe singleton registry for component health tracking.
    
    Provides centralized health monitoring with:
    - Real-time status updates
    - Failure tracking
    - Overall system health calculation
    - API-ready health summaries
    """
    
    _instance: Optional['HealthRegistry'] = None
    _lock = RLock()
    
    def __new__(cls):
        if cls._instance is None:
            with cls._lock:
                if cls._instance is None:
                    cls._instance = super().__new__(cls)
                    cls._instance._initialized = False
        return cls._instance
    
    def __init__(self):
        if getattr(self, '_initialized', False):
            return
        
        self._components: Dict[str, ComponentHealth] = {}
        self._component_lock = RLock()
        self._initialized = True
        
        logger.debug("health_registry_initialized")
    
    def register_component(
        self,
        name: str,
        status: HealthStatus = HealthStatus.UNKNOWN,
        **metadata
    ) -> None:
        """
        Register or update a component's health status.
        
        Args:
            name: Component name
            status: Current health status
            **metadata: Additional metadata (device, port, etc.)
        """
        with self._component_lock:
            if name in self._components:
                component = self._components[name]
                component.status = status
                component.last_check = datetime.utcnow()
                component.metadata.update(metadata)
                component.total_checks += 1
                
                if status == HealthStatus.HEALTHY:
                    component.consecutive_failures = 0
            else:
                self._components[name] = ComponentHealth(
                    name=name,
                    status=status,
                    metadata=metadata,
                    total_checks=1
                )
        
        logger.debug(
            "health_status_updated",
            component=name,
            status=status.value,
            metadata=metadata
        )
    
    def mark_healthy(self, name: str, **metadata) -> None:
        """Mark a component as healthy."""
        self.register_component(name, HealthStatus.HEALTHY, **metadata)
    
    def mark_degraded(self, name: str, reason: str, **metadata) -> None:
        """Mark a component as degraded (partially functional)."""
        with self._component_lock:
            if name in self._components:
                component = self._components[name]
                component.status = HealthStatus.DEGRADED
                component.error_message = reason
                component.last_check = datetime.utcnow()
                component.metadata.update(metadata)
                component.consecutive_failures += 1
                component.total_checks += 1
            else:
                self._components[name] = ComponentHealth(
                    name=name,
                    status=HealthStatus.DEGRADED,
                    error_message=reason,
                    metadata=metadata,
                    consecutive_failures=1,
                    total_checks=1
                )
        
        logger.warning(
            "component_degraded",
            component=name,
            reason=reason,
            metadata=metadata
        )
    
    def mark_failed(self, name: str, error: str, **metadata) -> None:
        """Mark a component as completely failed."""
        with self._component_lock:
            if name in self._components:
                component = self._components[name]
                component.status = HealthStatus.UNHEALTHY
                component.error_message = error
                component.last_check = datetime.utcnow()
                component.metadata.update(metadata)
                component.consecutive_failures += 1
                component.total_checks += 1
            else:
                self._components[name] = ComponentHealth(
                    name=name,
                    status=HealthStatus.UNHEALTHY,
                    error_message=error,
                    metadata=metadata,
                    consecutive_failures=1,
                    total_checks=1
                )
        
        logger.error(
            "component_failed",
            component=name,
            error=error,
            metadata=metadata
        )
    
    def get_component_health(self, name: str) -> Optional[ComponentHealth]:
        """Get health info for a specific component."""
        with self._component_lock:
            return self._components.get(name)
    
    def get_all_health(self) -> Dict[str, ComponentHealth]:
        """Get health info for all registered components."""
        with self._component_lock:
            return self._components.copy()
    
    def get_overall_status(self) -> HealthStatus:
        """
        Calculate overall system health based on component statuses.
        
        Logic:
        - UNHEALTHY: Any component is unhealthy
        - DEGRADED: Any component is degraded
        - HEALTHY: All components healthy
        - UNKNOWN: No components or all unknown
        """
        with self._component_lock:
            if not self._components:
                return HealthStatus.UNKNOWN
            
            statuses = [c.status for c in self._components.values()]
            
            if any(s == HealthStatus.UNHEALTHY for s in statuses):
                return HealthStatus.UNHEALTHY
            
            if any(s == HealthStatus.DEGRADED for s in statuses):
                return HealthStatus.DEGRADED
            
            if all(s == HealthStatus.HEALTHY for s in statuses):
                return HealthStatus.HEALTHY
            
            return HealthStatus.UNKNOWN
    
    def get_health_summary(self) -> Dict[str, Any]:
        """
        Get comprehensive health summary for API responses.
        
        Returns:
            Dictionary with overall status, timestamp, component details, and summary stats
        """
        with self._component_lock:
            overall = self.get_overall_status()
            components = {
                name: health.to_dict()
                for name, health in self._components.items()
            }
            
            return {
                "overall_status": overall.value,
                "timestamp": datetime.utcnow().isoformat(),
                "components": components,
                "summary": {
                    "total": len(components),
                    "healthy": sum(1 for c in components.values() if c["status"] == "healthy"),
                    "degraded": sum(1 for c in components.values() if c["status"] == "degraded"),
                    "unhealthy": sum(1 for c in components.values() if c["status"] == "unhealthy"),
                    "unknown": sum(1 for c in components.values() if c["status"] == "unknown"),
                }
            }
    
    def clear(self) -> None:
        """Clear all health data (testing only)."""
        with self._component_lock:
            self._components.clear()
        logger.debug("health_registry_cleared")


# Singleton instance
_health_registry = HealthRegistry()


# Public API
def get_health_registry() -> HealthRegistry:
    """Get the singleton health registry instance."""
    return _health_registry


# Legacy compatibility functions
def register_status(name: str, status: str, **metadata):
    """Legacy compatibility - maps old status strings to HealthStatus enum."""
    status_map = {
        "ready": HealthStatus.HEALTHY,
        "initializing": HealthStatus.UNKNOWN,
        "starting": HealthStatus.UNKNOWN,
        "stopped": HealthStatus.UNKNOWN,
        "failed": HealthStatus.UNHEALTHY,
        "degraded": HealthStatus.DEGRADED,
    }
    health_status = status_map.get(status.lower(), HealthStatus.UNKNOWN)
    _health_registry.register_component(name, health_status, **metadata)


def mark_failed(name: str, error: str, **metadata):
    """Legacy compatibility - mark component as failed."""
    _health_registry.mark_failed(name, error, **metadata)