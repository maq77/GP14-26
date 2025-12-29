from __future__ import annotations
from typing import Optional, List
import numpy as np
import cv2

from src.schemas.detection import (
    DetectRequest,
    DetectResponse,
)
from src.services.ml.object_detection import ObjectDetectionService


class DetectionApp:
    """
    Application/use-case layer.
    Keeps transports (HTTP/gRPC/RabbitMQ) thin and stateless.
    """
    def __init__(self, runner: ObjectDetectionService):
        self.runner = runner

    # --- helpers -------------------------------------------------------------
    @staticmethod
    def _bytes_to_bgr(image_bytes: bytes) -> np.ndarray:
        """Decode bytes→OpenCV BGR; raise ValueError on failure."""
        nparr = np.frombuffer(image_bytes, np.uint8)
        img = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
        if img is None:
            raise ValueError("Invalid image bytes")
        return img

    # --- use-cases -----------------------------------------------------------
    def detect(self, req: DetectRequest) -> DetectResponse:
        """
        Accepts DetectRequest (bytes or ndarray in req.image),
        performs any pre/post orchestration, and delegates to the runner.
        """
        # Allow either bytes or already-decoded ndarray
        image_input = req.image
        if isinstance(image_input, (bytes, bytearray)):
            img = self._bytes_to_bgr(image_input)
            req.image = img  # mutate to ndarray for the runner
        return self.runner.detect_objects(req)

    def detect_waste(self, req: DetectRequest) -> DetectResponse:
        if isinstance(req.image, (bytes, bytearray)):
            req.image = self._bytes_to_bgr(req.image)
        return self.runner.detect_waste(req)

    def detect_vandalism(self, req: DetectRequest) -> DetectResponse:
        if isinstance(req.image, (bytes, bytearray)):
            req.image = self._bytes_to_bgr(req.image)
        return self.runner.detect_vandalism(req)

    def get_model_info(self) -> dict:
        return self.runner.get_model_info()
