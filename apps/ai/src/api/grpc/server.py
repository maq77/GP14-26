"""
apps/ai/src/api/grpc/server.py
Simple gRPC server for AI inference.

Responsibilities:
- Create and configure gRPC server
- Register servicers (Detection, Face, etc.)
- Start/stop server
"""

import sys
from pathlib import Path
import grpc
from concurrent import futures
import structlog
from typing import Optional

print("CURRENT FILE:", Path(__file__).resolve())
print("PARENTS:")
for i in range(0, 8):
    print(i, "→", Path(__file__).resolve().parents[i])

# Add packages/contracts/python to path
contracts_path = Path(__file__).resolve().parents[5] / "packages" / "contracts" / "python"
sys.path.insert(0, str(contracts_path))
sys.path.insert(0, str(contracts_path.parent))

print("Added to sys.path:", contracts_path)

# Import generated gRPC code WITHOUT package prefix
from sssp.ai.detection.detection_pb2_grpc import add_DetectionServiceServicer_to_server
# from face_pb2_grpc import add_FaceServiceServicer_to_server
# from behavior_pb2_grpc import add_BehaviorServiceServicer_to_server
# from aqi_pb2_grpc import add_AqiServiceServicer_to_server

from .servicers.detection_servicer import DetectionServicer
# from .servicers.face_servicer import FaceServicer
# from .servicers.behavior_servicer import BehaviorServicer
# from .servicers.aqi_servicer import AqiServicer

from ...core.config import settings

logger = structlog.get_logger("grpc_server")


class GRPCServer:
    """
    Simple gRPC server for AI services.
    
    Clean and straightforward - no over-engineering.
    """
    
    def __init__(self):
        """Initialize gRPC server"""
        self.host = settings.GRPC_HOST
        self.port = settings.GRPC_PORT
        self.max_workers = settings.GRPC_MAX_WORKERS
        self.server: Optional[grpc.Server] = None
        
        logger.info(
            "grpc_server_initialized",
            host=self.host,
            port=self.port,
            max_workers=self.max_workers
        )
    
    def start(self) -> None:
        """
        Start gRPC server.
        Non-blocking - runs in background threads.
        """
        if self.server is not None:
            logger.warning("grpc_server_already_started")
            return
        
        logger.info("creating_grpc_server")
        
        try:
            # Create server with thread pool
            self.server = grpc.server(
                futures.ThreadPoolExecutor(max_workers=self.max_workers),
                options=[
                    ('grpc.max_send_message_length', 100 * 1024 * 1024),  # 100MB
                    ('grpc.max_receive_message_length', 100 * 1024 * 1024),  # 100MB
                ]
            )
            
            # Register servicers
            logger.info("registering_grpc_servicers")
            
            # Detection Service
            detection_servicer = DetectionServicer()
            add_DetectionServiceServicer_to_server(detection_servicer, self.server)
            logger.info("servicer_registered", servicer="DetectionService")
            
            # Face Service (TODO: Implement in Part 2)
            # face_servicer = FaceServicer()
            # add_FaceServiceServicer_to_server(face_servicer, self.server)
            # logger.info("servicer_registered", servicer="FaceService")
            
            # Add more servicers here...
            
            # Bind address and start
            bind_address = f"{self.host}:{self.port}"
            self.server.add_insecure_port(bind_address)
            
            # Start server (non-blocking)
            self.server.start()
            
            logger.info(
                "grpc_server_started",
                address=bind_address
            )
            
        except Exception as e:
            logger.error(
                "failed_to_start_grpc_server",
                error=str(e),
                exc_info=True
            )
            raise
    
    def stop(self, grace_period: int = 5) -> None:
        """
        Stop gRPC server gracefully.
        
        Args:
            grace_period: Seconds to wait for in-flight requests
        """
        if self.server is None:
            logger.warning("grpc_server_not_running")
            return
        
        logger.info("stopping_grpc_server", grace_period=grace_period)
        
        try:
            self.server.stop(grace_period)
            logger.info("grpc_server_stopped")
        except Exception as e:
            logger.error("error_stopping_grpc_server", error=str(e))
        finally:
            self.server = None
    
    def wait_for_termination(self):
        """Wait for server termination (blocking)"""
        if self.server:
            self.server.wait_for_termination()


# ============================================================================
# Export
# ============================================================================

__all__ = ["GRPCServer"]