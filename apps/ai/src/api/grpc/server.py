import sys
from pathlib import Path
from concurrent import futures
from typing import Optional

import grpc
import structlog

from ...core.config import settings
from ...services.ml.Face_Recognition_Service import FaceRecognitionService
from .servicers.detection_servicer import DetectionServicer
from .servicers.face_servicer import FaceServicer
from .servicers.video_stream_servicer import VideoStreamService

contracts_path = Path(__file__).resolve().parents[5] / "packages" / "contracts" / "python"
sys.path.insert(0, str(contracts_path))
sys.path.insert(0, str(contracts_path.parent))

from detection_pb2_grpc import add_DetectionServiceServicer_to_server
from face_pb2_grpc import add_FaceServiceServicer_to_server
from video_stream_pb2_grpc import add_VideoStreamServiceServicer_to_server

logger = structlog.get_logger("grpc_server")


class GRPCServer:
    def __init__(self):
        self.host = settings.GRPC_HOST
        self.port = settings.GRPC_PORT
        self.max_workers = settings.GRPC_MAX_WORKERS
        self.server: Optional[grpc.Server] = None
        self.face_service: Optional[FaceRecognitionService] = None
        logger.info(
            "grpc_server_initialized",
            host=self.host,
            port=self.port,
            max_workers=self.max_workers,
        )

    def start(self) -> None:
        if self.server is not None:
            logger.warning("grpc_server_already_started")
            return
        try:
            self.server = grpc.server(
                futures.ThreadPoolExecutor(max_workers=self.max_workers),
                options=[
                    ("grpc.max_send_message_length", 100 * 1024 * 1024),
                    ("grpc.max_receive_message_length", 100 * 1024 * 1024),
                ],
            )
            detection_servicer = DetectionServicer()
            add_DetectionServiceServicer_to_server(detection_servicer, self.server)
            logger.info("servicer_registered", servicer="DetectionService")
            self.face_service = FaceRecognitionService()
            add_FaceServiceServicer_to_server(
                FaceServicer(self.face_service),
                self.server,
            )
            logger.info("servicer_registered", servicer="FaceService")
            add_VideoStreamServiceServicer_to_server(
                VideoStreamService(self.face_service),
                self.server,
            )
            logger.info("servicer_registered", servicer="VideoStreamService")
            bind_address = f"{self.host}:{self.port}"
            self.server.add_insecure_port(bind_address)
            self.server.start()
            logger.info("grpc_server_started", address=bind_address)
        except Exception as e:
            logger.error("failed_to_start_grpc_server", error=str(e), exc_info=True)
            raise

    def stop(self, grace_period: int = 5) -> None:
        if self.server is None:
            logger.warning("grpc_server_not_running")
            return
        try:
            logger.info("stopping_grpc_server", grace_period=grace_period)
            self.server.stop(grace_period)
            logger.info("grpc_server_stopped")
        except Exception as e:
            logger.error("error_stopping_grpc_server", error=str(e))
        finally:
            self.server = None
            self.face_service = None

    def wait_for_termination(self) -> None:
        if self.server:
            self.server.wait_for_termination()


__all__ = ["GRPCServer"]
