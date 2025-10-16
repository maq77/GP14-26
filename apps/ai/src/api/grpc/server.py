"""
gRPC Server Implementation (Infrastructure Layer)
This handles the actual gRPC server setup and service registration.
Lifecycle management is handled separately by the lifespan component.
"""
import grpc
from concurrent import futures
import structlog
from typing import Optional, List

from .servicers.detection_servicer import DetectionServicer
#from .servicers.face_servicer import FaceServicer
# from .servicers.behavior_servicer import BehaviorServicer
# from .servicers.aqi_servicer import AqiServicer

from ...core.config import settings

logger = structlog.get_logger("grpc.server")


class GRPCServer:
    """
    gRPC Server for AI inference services.
    
    Follows Netflix/Google patterns:
    - Separate infrastructure from lifecycle
    - Service registration pattern
    - Graceful shutdown support
    - Interceptor support for cross-cutting concerns
    """
    
    def __init__(
        self,
        host: Optional[str] = None,
        port: Optional[int] = None,
        max_workers: Optional[int] = None,
        max_concurrent_rpcs: Optional[int] = None
    ):
        """
        Initialize gRPC server.
        
        Args:
            host: Server host (default from settings)
            port: Server port (default from settings)
            max_workers: Thread pool size (default: 10)
            max_concurrent_rpcs: Max concurrent RPCs (default: 100)
        """
        self.host = host or getattr(settings, 'GRPC_HOST', '0.0.0.0')
        self.port = port or getattr(settings, 'GRPC_PORT', 50051)
        self.max_workers = max_workers or getattr(settings, 'GRPC_MAX_WORKERS', 10)
        self.max_concurrent_rpcs = max_concurrent_rpcs or getattr(
            settings, 'GRPC_MAX_CONCURRENT_RPCS', 100
        )
        
        self.server: Optional[grpc.Server] = None
        self._servicers: List = []
    
    def _create_server(self) -> grpc.Server:
        """
        Create gRPC server with configuration.
        
        Returns:
            Configured gRPC server
        """
        # Server options for production
        options = [
            ('grpc.max_send_message_length', 100 * 1024 * 1024),  # 100MB
            ('grpc.max_receive_message_length', 100 * 1024 * 1024),  # 100MB
            ('grpc.max_concurrent_streams', self.max_concurrent_rpcs),
            ('grpc.so_reuseport', 1),
            ('grpc.keepalive_time_ms', 10000),
            ('grpc.keepalive_timeout_ms', 5000),
            ('grpc.keepalive_permit_without_calls', 1),
            ('grpc.http2.max_pings_without_data', 0),
        ]
        
        # Create thread pool executor
        thread_pool = futures.ThreadPoolExecutor(max_workers=self.max_workers)
        
        # Create server with interceptors (add auth, logging, etc.)
        interceptors = self._get_interceptors()
        
        if interceptors:
            server = grpc.server(
                thread_pool,
                interceptors=interceptors,
                options=options
            )
        else:
            server = grpc.server(thread_pool, options=options)
        
        return server
    
    def _get_interceptors(self) -> List:
        """
        Get gRPC interceptors for cross-cutting concerns.
        
        Add interceptors for:
        - Authentication
        - Logging
        - Metrics
        - Error handling
        
        Returns:
            List of interceptor instances
        """
        interceptors = []
        
        # Add custom interceptors here
        # Example:
        # from .interceptors import LoggingInterceptor, AuthInterceptor
        # interceptors.append(LoggingInterceptor())
        # interceptors.append(AuthInterceptor())
        
        return interceptors
    
    def _register_services(self):
        """
        Register all gRPC service implementations.
        
        This is where you add all your servicers (Netflix pattern).
        """
        logger.info("registering_grpc_services")
        
        # Import proto definitions
        try:
            # Update these imports based on your actual proto package
            from packages.contracts.python import (
                detection_pb2_grpc,
                # face_pb2_grpc,
                # behavior_pb2_grpc,
                # aqi_pb2_grpc,
            )
            
            # Register Detection Service
            detection_servicer = DetectionServicer()
            detection_pb2_grpc.add_DetectionServiceServicer_to_server(
                detection_servicer,
                self.server
            )
            self._servicers.append(detection_servicer)
            logger.info("service_registered", service="DetectionService")
            
            # Register Face Service
            # face_servicer = FaceServicer()
            # face_pb2_grpc.add_FaceServiceServicer_to_server(
            #     face_servicer,
            #     self.server
            # )
            # self._servicers.append(face_servicer)
            # logger.info("service_registered", service="FaceService")
            
            # Add more services as needed...
            
            logger.info(
                "all_services_registered",
                total_services=len(self._servicers)
            )
            
        except ImportError as e:
            logger.error(
                "failed_to_import_proto_definitions",
                error=str(e),
                hint="Ensure protobuf contracts are generated"
            )
            raise
    
    def start(self) -> None:
        """
        Start the gRPC server.
        
        This is non-blocking - the server runs in background threads.
        """
        if self.server is not None:
            logger.warning("grpc_server_already_started")
            return
        
        logger.info(
            "starting_grpc_server",
            host=self.host,
            port=self.port,
            max_workers=self.max_workers
        )
        
        try:
            # Create server
            self.server = self._create_server()
            
            # Register all services
            self._register_services()
            
            # Add insecure port (use add_secure_port for TLS)
            bind_address = f'{self.host}:{self.port}'
            self.server.add_insecure_port(bind_address)
            
            # For TLS/SSL in production:
            # with open('server.key', 'rb') as f:
            #     private_key = f.read()
            # with open('server.pem', 'rb') as f:
            #     certificate_chain = f.read()
            # credentials = grpc.ssl_server_credentials(
            #     [(private_key, certificate_chain)]
            # )
            # self.server.add_secure_port(bind_address, credentials)
            
            # Start server (non-blocking)
            self.server.start()
            
            logger.info(
                "grpc_server_started",
                address=bind_address,
                services=len(self._servicers)
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
        Stop the gRPC server gracefully.
        
        Args:
            grace_period: Seconds to wait for in-flight RPCs to complete
        """
        if self.server is None:
            logger.warning("grpc_server_not_running")
            return
        
        logger.info(
            "stopping_grpc_server",
            grace_period=grace_period
        )
        
        try:
            # Graceful shutdown
            self.server.stop(grace_period)
            
            logger.info("grpc_server_stopped")
            
        except Exception as e:
            logger.error(
                "error_stopping_grpc_server",
                error=str(e),
                exc_info=True
            )
        finally:
            self.server = None
            self._servicers.clear()
    
    def wait_for_termination(self, timeout: Optional[float] = None):
        """
        Block until the server terminates.
        
        Useful for standalone gRPC server deployment.
        
        Args:
            timeout: Max time to wait in seconds
        """
        if self.server:
            self.server.wait_for_termination(timeout=timeout)
