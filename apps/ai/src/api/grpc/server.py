"""
apps/ai/src/api/grpc/server.py
gRPC Server Setup
"""

import sys
from pathlib import Path
from concurrent import futures
import grpc

# Add packages/contracts/python to path
contracts_path = Path(__file__).resolve().parents[6] / "packages" / "contracts" / "python"
sys.path.insert(0, str(contracts_path))

from packages.contracts.python import detection_pb2_grpc
from packages.contracts.python.detection_pb2_grpc import add_DetectionServiceServicer_to_server

from ...core.config import settings
from ...core.logging import get_logger
from ...models.object.model_loader import get_model_loader
from .servicers.detection_servicer import DetectionServicer

logger = get_logger("grpc_server")


class GRPCServer:
    """
    gRPC Server Manager
    Handles server lifecycle and servicer registration
    """
    
    def __init__(self):
        """Initialize gRPC server"""
        self.server = None
        self.port = settings.GRPC_PORT
        self.host = settings.GRPC_HOST
        logger.info("grpc_server_initialized", port=self.port)
    
    def start(self):
        """
        Start gRPC server
        Loads models and registers servicers
        """
        try:
            logger.info("starting_grpc_server", host=self.host, port=self.port)
            
            # Load YOLO model on startup
            logger.info("loading_detection_model")
            model_loader = get_model_loader()
            model_loader.load_detector()
            logger.info("detection_model_loaded")
            
            # Create gRPC server
            self.server = grpc.server(
                futures.ThreadPoolExecutor(max_workers=settings.GRPC_MAX_WORKERS),
                options=[
                    ('grpc.max_send_message_length', settings.GRPC_MAX_MESSAGE_LENGTH),
                    ('grpc.max_receive_message_length', settings.GRPC_MAX_MESSAGE_LENGTH),
                ]
            )
            
            # Register servicers
            logger.info("registering_servicers")
            detection_servicer = DetectionServicer()
            add_DetectionServiceServicer_to_server(detection_servicer, self.server)
            
            # TODO: Register other servicers (Face, Behavior, AQI) in future parts
            
            # Add insecure port (use TLS in production!)
            server_address = f"{self.host}:{self.port}"
            self.server.add_insecure_port(server_address)
            
            # Start server
            self.server.start()
            
            logger.info(
                "grpc_server_started",
                address=server_address,
                max_workers=settings.GRPC_MAX_WORKERS
            )
            
            return self.server
            
        except Exception as e:
            logger.error("grpc_server_start_failed", error=str(e), exc_info=True)
            raise
    
    def stop(self, grace_period: int = 5):
        """
        Stop gRPC server gracefully
        
        Args:
            grace_period: Seconds to wait for ongoing requests
        """
        if self.server:
            logger.info("stopping_grpc_server", grace_period=grace_period)
            self.server.stop(grace_period)
            logger.info("grpc_server_stopped")
    
    def wait_for_termination(self):
        """Wait for server termination (blocking)"""
        if self.server:
            logger.info("grpc_server_waiting_for_termination")
            self.server.wait_for_termination()


# ============================================================================
# Convenience Functions
# ============================================================================

def serve():
    """
    Start and run gRPC server (blocking)
    """
    server = GRPCServer()
    server.start()
    
    try:
        server.wait_for_termination()
    except KeyboardInterrupt:
        logger.info("received_keyboard_interrupt")
        server.stop()


# ============================================================================
# Export
# ============================================================================

__all__ = ["GRPCServer", "serve"]