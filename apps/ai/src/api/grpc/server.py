"""
apps/ai/src/api/grpc/server.py
Simple gRPC server for AI inference.

Responsibilities:
- Create and configure gRPC server
- Register servicers (Detection, Face, VideoStream)
- Start/stop server

AI responsibilities:
- Face detection
- Embedding extraction
- Model info / low-level ML ops

All higher-level business logic (recognize, verify, enroll, etc.)
is implemented in .NET and NOT in this AI service.
"""

import sys
from pathlib import Path
from concurrent import futures
from typing import Optional

import grpc
import structlog

# ---------------------------------------------------------------------------
# Sys.path setup so generated protobufs & packages are importable
# ---------------------------------------------------------------------------

CURRENT_FILE = Path(__file__).resolve()
PROJECT_ROOT = CURRENT_FILE.parents[5]  # .../GP14-26
CONTRACTS_PY_PATH = PROJECT_ROOT / "packages" / "contracts" / "python"

# Make sure these are at the front of sys.path
sys.path.insert(0, str(PROJECT_ROOT))          # so `packages.*` works
sys.path.insert(0, str(CONTRACTS_PY_PATH))     # so `face_pb2` / `video_stream_pb2` etc. work
sys.path.insert(0, str(CONTRACTS_PY_PATH.parent))  # .../packages/contracts

# ---------------------------------------------------------------------------
# gRPC generated code imports
# ---------------------------------------------------------------------------

# Detection service (compiled with python_package = "sssp.ai.detection" most likely)
from sssp.ai.detection.detection_pb2_grpc import (
    add_DetectionServiceServicer_to_server,
)

# Face service (no BL here – only low-level ML endpoints are implemented)
from packages.contracts.python.face_pb2_grpc import (
    add_FaceServiceServicer_to_server,
)

# Video stream service for live frames + embeddings
from packages.contracts.python.video_stream_pb2_grpc import (
    add_VideoStreamServiceServicer_to_server,
)

# ---------------------------------------------------------------------------
# Servicers & ML service
# ---------------------------------------------------------------------------

from .servicers.detection_servicer import DetectionServicer
from .servicers.face_servicer import FaceServicer
from .servicers.video_stream_servicer import VideoStreamService
from ...services.ml.Face_Recognition_Service import FaceRecognitionService
from ...core.config import settings

logger = structlog.get_logger("grpc_server")


class GRPCServer:
    """
    Simple gRPC server for AI services.

    - Single shared FaceRecognitionService instance
    - Registers:
        * DetectionServicer          → object detection (if you have it)
        * FaceServicer              → DetectFaces, ExtractEmbedding, GetModelInfo
        * VideoStreamService        → streaming frames + embeddings

    NOTE:
        FaceServicer intentionally does NOT implement business logic RPCs
        like RecognizeFace / VerifyFace / EnrollFace. Those RPCs return
        a "not implemented in AI" message because .NET is responsible
        for the business logic side.
    """

    def __init__(self, face_service: Optional[FaceRecognitionService] = None) -> None:
        # Server basic config from settings
        self.host = settings.GRPC_HOST
        self.port = settings.GRPC_PORT
        self.max_workers = settings.GRPC_MAX_WORKERS

        # Shared ML service (YOLO/ArcFace/etc.)
        self.face_service: FaceRecognitionService = (
            face_service or FaceRecognitionService()
        )

        self.server: Optional[grpc.Server] = None
        logger.info(
            "grpc_server_initialized",
            host=self.host,
            port=self.port,
            max_workers=self.max_workers,
        )

    # ------------------------------------------------------------------ #
    # Lifecycle
    # ------------------------------------------------------------------ #

    def start(self) -> None:
        """
        Start gRPC server (non-blocking).
        """
        if self.server is not None:
            logger.warning("grpc_server_already_started")
            return

        logger.info("creating_grpc_server")

        try:
            self.server = grpc.server(
                futures.ThreadPoolExecutor(max_workers=self.max_workers),
                options=[
                    ("grpc.max_send_message_length", 100 * 1024 * 1024),   # 100 MB
                    ("grpc.max_receive_message_length", 100 * 1024 * 1024),  # 100 MB
                ],
            )

            # ----------------------------------------------------------
            # Register servicers
            # ----------------------------------------------------------
            logger.info("registering_grpc_servicers")

            # 1) Detection service (if you have non-face detection, e.g. objects)
            detection_servicer = DetectionServicer()
            add_DetectionServiceServicer_to_server(detection_servicer, self.server)
            logger.info("servicer_registered", servicer="DetectionService")

            # 2) Face service (AI-only: detect, embeddings, model info)
            face_servicer = FaceServicer(face_service=self.face_service)
            add_FaceServiceServicer_to_server(face_servicer, self.server)
            logger.info("servicer_registered", servicer="FaceService")

            # 3) Video stream service (stream frames & return boxes + embeddings)
            video_stream_servicer = VideoStreamService(face_service=self.face_service)
            add_VideoStreamServiceServicer_to_server(
                video_stream_servicer, self.server
            )
            logger.info("servicer_registered", servicer="VideoStreamService")

            # ----------------------------------------------------------
            # Bind & start
            # ----------------------------------------------------------
            bind_address = f"{self.host}:{self.port}"
            self.server.add_insecure_port(bind_address)
            self.server.start()

            logger.info("grpc_server_started", address=bind_address)

        except Exception as e:
            logger.error(
                "failed_to_start_grpc_server",
                error=str(e),
                exc_info=True,
            )
            # Surface the error – better to crash fast and let orchestrator restart
            raise

    def stop(self, grace_period: int = 5) -> None:
        """
        Stop gRPC server gracefully.

        Args:
            grace_period: seconds to wait for in-flight requests to finish
        """
        if self.server is None:
            logger.warning("grpc_server_not_running")
            return

        logger.info("stopping_grpc_server", grace_period=grace_period)

        try:
            logger.info("stopping_grpc_server", grace_period=grace_period)
            self.server.stop(grace_period)
            logger.info("grpc_server_stopped")
        except Exception as e:
            logger.error("error_stopping_grpc_server", error=str(e))
        finally:
            self.server = None

    def wait_for_termination(self) -> None:
        """
        Block the current thread until the server is terminated.
        """
        if self.server:
            self.server.wait_for_termination()


# ========================================================================
# Export
# ========================================================================

__all__ = ["GRPCServer"]
