"""Base classes and enums for lifecycle components."""
from abc import ABC, abstractmethod
from enum import Enum
from typing import Optional, Dict, Any
import structlog
from datetime import datetime

logger = structlog.get_logger(__name__)


class ComponentState(Enum):
    """Lifecycle states for managed components."""
    UNINITIALIZED = "uninitialized"
    INITIALIZING = "initializing"
    READY = "ready"
    DEGRADED = "degraded"
    FAILED = "failed"
    STOPPING = "stopping"
    STOPPED = "stopped"


class ComponentPriority(Enum):
    """
    Component startup priority levels.
    Lower values start first, higher values start last.
    Shutdown occurs in reverse order (LIFO).
    """
    CRITICAL = 0      # Core infrastructure (DB connections)
    HIGH = 10         # Essential services (ML models)
    NORMAL = 20       # Standard services (gRPC, RabbitMQ)
    LOW = 30          # Optional features (monitoring, metrics)


class BaseLifecycleComponent(ABC):
    """
    Abstract base class for all lifecycle-managed components.
    
    Each component represents a discrete subsystem requiring
    initialization and cleanup (models, servers, connections, etc.).
    
    Features:
    - Automatic state management
    - Structured logging
    - Health monitoring
    - Metrics collection
    - Dependency declaration
    """
    
    # Override these in subclasses
    name: str = "UnnamedComponent"
    priority: ComponentPriority = ComponentPriority.NORMAL
    startup_timeout: int = 30  # seconds
    shutdown_timeout: int = 10  # seconds
    depends_on: list = []  # List of component names (dependencies)
    
    def __init__(self):
        self.state = ComponentState.UNINITIALIZED
        self.started_at: Optional[datetime] = None
        self.stopped_at: Optional[datetime] = None
        self.error: Optional[str] = None
        self.metadata: Dict[str, Any] = {}
        self._logger = structlog.get_logger(f"component.{self.name}")
    
    @abstractmethod
    async def startup(self) -> None:
        """
        Initialize the component.
        
        This method should:
        - Load resources
        - Establish connections
        - Validate configuration
        - Raise exceptions on failure
        
        Exceptions will be caught by the manager and logged appropriately.
        """
        pass
    
    @abstractmethod
    async def shutdown(self) -> None:
        """
        Clean up the component.
        
        This method should:
        - Release resources
        - Close connections
        - Save state if needed
        - Be idempotent (safe to call multiple times)
        - Never raise exceptions (log errors instead)
        """
        pass
    
    async def health_check(self) -> bool:
        """
        Perform a health check on this component.
        
        Override to implement custom health checks.
        Default implementation checks if state is READY.
        
        Returns:
            True if component is healthy, False otherwise
        """
        return self.state == ComponentState.READY
    
    def get_metrics(self) -> Dict[str, Any]:
        """
        Return component-specific metrics.
        
        Override to provide custom metrics (request counts, latencies, etc.).
        
        Returns:
            Dictionary of metric name -> value
        """
        uptime = None
        if self.started_at:
            uptime = (datetime.utcnow() - self.started_at).total_seconds()
        
        return {
            "state": self.state.value,
            "uptime_seconds": uptime,
            "started_at": self.started_at.isoformat() if self.started_at else None,
            "error": self.error,
            "metadata": self.metadata,
        }
    
    def safe_log(self, event: str, **kwargs):
        """Helper for structured logging."""
        self._logger.info(event, component=self.name, **kwargs)
    
    def log_error(self, event: str, error: Exception, **kwargs):
        """Helper for error logging with full context."""
        self._logger.error(
            event,
            component=self.name,
            error=str(error),
            error_type=type(error).__name__,
            **kwargs,
            exc_info=True
        )