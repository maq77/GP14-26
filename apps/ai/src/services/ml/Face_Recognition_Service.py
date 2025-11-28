import time
from typing import List, Dict, Any, Optional

import cv2
import numpy as np

from ...core.logging import get_logger
from ...core.exceptions import InvalidImageException
from ...models.face.detector import FaceDetector
from ...models.face.embedder import FaceEmbedder, EmbedderConfig

logger = get_logger("face_recognition_service")


class FaceRecognitionService:
    def __init__(self):
        self.detector: Optional[FaceDetector] = None
        self.embedder: Optional[FaceEmbedder] = None
        logger.info("face_recognition_service_initialized")

    @staticmethod
    def _now_ms() -> float:
        return time.time() * 1000.0

    def _ensure_models_loaded(self):
        if self.detector is None or self.embedder is None:
            self.detector = FaceDetector()
            cfg = EmbedderConfig(input_color_space="rgb")
            self.embedder = FaceEmbedder(cfg)
            logger.info(
                "face_models_loaded",
                detector="MTCNN",
                embedder="FaceNet",
                device=self.embedder.config.device,
            )

    def _decode_image(self, image_bytes: bytes) -> np.ndarray:
        nparr = np.frombuffer(image_bytes, np.uint8)
        image = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
        if image is None:
            raise InvalidImageException("Could not decode image")
        return image

    def detect_faces(
        self,
        image_bytes: bytes,
        confidence_threshold: float = 0.5,
    ) -> Dict[str, Any]:
        self._ensure_models_loaded()
        start = self._now_ms()
        image = self._decode_image(image_bytes)
        boxes_xywh = self.detector.detect(image)
        faces: List[Dict[str, Any]] = []
        for (x, y, w, h) in boxes_xywh:
            faces.append(
                {
                    "bbox": [float(x), float(y), float(w), float(h)],
                    "confidence": 1.0,
                }
            )
        time_ms = self._now_ms() - start
        logger.info("detect_faces_completed", total_faces=len(faces), time_ms=time_ms)
        return {
            "faces": faces,
            "time_ms": time_ms,
        }

    def extract_embedding(
        self,
        image_bytes: bytes,
        confidence_threshold: float = 0.5,
    ) -> Dict[str, Any]:
        self._ensure_models_loaded()
        start = self._now_ms()
        image = self._decode_image(image_bytes)
        boxes_xywh, crops = self.detector.detect_with_crops(image)
        if not boxes_xywh or not crops:
            time_ms = self._now_ms() - start
            logger.info("extract_embedding_no_face", time_ms=time_ms)
            return {
                "success": True,
                "face_detected": False,
                "embedding": [],
                "bbox": None,
                "time_ms": time_ms,
            }
        areas = [w * h for (_, _, w, h) in boxes_xywh]
        best_idx = int(np.argmax(areas))
        best_box = boxes_xywh[best_idx]
        best_crop = crops[best_idx]
        emb = self.embedder.embed(best_crop)
        emb_list = [float(x) for x in emb.tolist()]
        time_ms = self._now_ms() - start
        logger.info("extract_embedding_completed", time_ms=time_ms)
        return {
            "success": True,
            "face_detected": True,
            "embedding": emb_list,
            "bbox": [float(v) for v in best_box],
            "time_ms": time_ms,
        }

    def process_frame(
        self,
        frame: np.ndarray,
        confidence_threshold: float = 0.5,
    ) -> List[Dict[str, Any]]:
        self._ensure_models_loaded()
        boxes_xywh, crops = self.detector.detect_with_crops(frame)
        if not boxes_xywh or not crops:
            logger.debug("process_frame_no_faces")
            return []
        embeddings = self.embedder.embed_batch(crops)
        results: List[Dict[str, Any]] = []
        for box, emb in zip(boxes_xywh, embeddings):
            results.append(
                {
                    "bbox": [float(v) for v in box],
                    "embedding": emb,
                }
            )
        logger.debug("process_frame_completed", faces=len(results))
        return results

    def get_model_info(self) -> Dict[str, Any]:
        self._ensure_models_loaded()
        info = {
            "model_name": "FaceNet (InceptionResnetV1)",
            "model_version": "vggface2",
            "device": self.embedder.config.device,
            "model_size_mb": 90.0,
            "input_size": self.embedder.config.image_size,
            "embedding_dim": 512,
        }
        logger.info("get_model_info", **info)
        return info
