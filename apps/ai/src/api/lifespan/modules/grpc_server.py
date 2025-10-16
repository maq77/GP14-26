"""
gRPC server lifecycle component (THIN WRAPPER).

This is ONLY responsible for lifecycle (start/stop).
Actual gRPC server logic is in api/grpc/server.py
"""
import structlog
from typing import Optional

from ..base import BaseLifecycleComponent, ComponentPriority, ComponentState
from ..registry import register_component
from ...grpc.server import GRPCServer  # Import the actual server
from ....core.config import settings

logger = structlog.get_logger(__name__)


@register_component
class GRPCServerComponent(BaseLifecycleComponent):
    """
    Lifecycle wrapper for gRPC server.
    
    Separation of Concerns:
    - This component: WHEN to start/stop (lifecycle)
    - GRPCServer class: HOW to serve (infrastructure)
    
    This follows Netflix/Google patterns where infrastructure
    is separate from orchestration.
    """
    
    name = "GRPCServer"
    priority = ComponentPriority.NORMAL
    depends_on = ["DetectionModel"]  # Start after models loaded
    startup_timeout = 20
    shutdown_timeout = 10
    
    def __init__(self):
        super().__init__()
        self.server: Optional[GRPCServer] = None
    
    async def startup(self) -> None:
        """Start the gRPC server."""
        self.safe_log("starting_grpc_server")
        
        # Create and start actual gRPC server
        self.server = GRPCServer()
        self.server.start()
        
        # Store metadata
        self.metadata.update({
            "host": self.server.host,
            "port": self.server.port,
            "address": f"{self.server.host}:{self.server.port}",
            "max_workers": self.server.max_workers,
            "services": len(self.server._servicers)
        })
        
        self.safe_log("grpc_server_started", **self.metadata)
    
    async def shutdown(self) -> None:
        """Stop gRPC server gracefully."""
        if self.server:
            self.safe_log("stopping_grpc_server")
            
            try:
                self.server.stop(grace_period=5)
                
                # Brief sleep for cleanup
                import asyncio
                await asyncio.sleep(0.5)
                
                self.safe_log("grpc_server_stopped")
                
            except Exception as e:
                self.log_error("grpc_shutdown_error", e)
    
    async def health_check(self) -> bool:
        """Check if gRPC server is running."""
        return (
            self.server is not None and
            self.server.server is not None and
            self.state == ComponentState.READY
        )