"""
Optimized Face Recognition Service
- Eliminated redundant validation checks
- Improved error handling with specific exception types
- Better memory management for batch operations
- Optimized image decoding pipeline
- Comprehensive metrics tracking
"""
import time
from typing import List, Dict, Any, Optional
from dataclasses import dataclass

import cv2
import numpy as np

from ...core.logging import get_logger
from ...core.exceptions import InvalidImageException
from ...models.face.detector import FaceDetector, DetectionConfig, DetectedFace
from ...models.face.embedder import FaceEmbedder, EmbedderConfig


logger = get_logger("sssp.ai.face_recognition_service")


@dataclass
class ImageValidationLimits:
    """Image validation constraints."""
    MAX_DIMENSION: int = 4096
    MIN_DIMENSION: int = 40
    MAX_PIXELS: int = 4096 * 4096  # 16MP
    DEFAULT_RESIZE_DIMENSION: int = 1280


class FaceRecognitionService:
    """
    Production-grade face recognition service.
    
    Features:
    - Lazy model loading with warmup
    - Multi-face detection & embedding
    - Optimized batch processing
    - Quality assessment per face
    - Comprehensive metrics
    """

    def __init__(self) -> None:
        self.detector: Optional[FaceDetector] = None
        self.embedder: Optional[FaceEmbedder] = None
        self._frame_counter: int = 0
        self._validation = ImageValidationLimits()
        
        logger.info("face_recognition_service_initialized")

    # =========================================================================
    # LIFECYCLE MANAGEMENT
    # =========================================================================

    def warmup(self) -> None:
        """Warmup models with dummy data to eliminate cold start latency."""
        self._ensure_models_loaded()
        
        logger.info("warming_up_models")
        start = time.time()
        
        try:
            # Warmup detector
            dummy_image = np.random.randint(0, 255, (480, 640, 3), dtype=np.uint8)
            _ = self.detector.detect_with_quality(
                dummy_image,
                confidence_threshold=0.7,
                return_crops=True,
            )
            
            # Warmup embedder
            dummy_crop = np.random.randint(0, 255, (160, 160, 3), dtype=np.uint8)
            _ = self.embedder.embed_batch([dummy_crop])
            
            elapsed = time.time() - start
            logger.info(
                "model_warmup_completed",
                elapsed_ms=round(elapsed * 1000, 2),
            )
        except Exception as e:
            logger.error("model_warmup_failed", error=str(e), exc_info=True)
            raise

    def cleanup(self) -> None:
        """Clean up resources on shutdown."""
        try:
            if self.detector is not None and hasattr(self.detector, 'close'):
                self.detector.close()
            
            if self.embedder is not None:
                # Clear CUDA cache if using GPU
                if hasattr(self.embedder, 'device') and 'cuda' in str(self.embedder.device):
                    try:
                        import torch
                        torch.cuda.empty_cache()
                        logger.info("cuda_cache_cleared")
                    except ImportError:
                        pass
            
            logger.info("face_recognition_service_cleanup_completed")
        except Exception as e:
            logger.error("cleanup_error", error=str(e), exc_info=True)

    # =========================================================================
    # PUBLIC API
    # =========================================================================

    def detect_faces(
        self,
        image_bytes: bytes,
        confidence_threshold: float = 0.7,
        max_faces: int = 10,
        include_crops: bool = False,
        max_image_dimension: Optional[int] = None,
    ) -> Dict[str, Any]:
        """
        Detect faces only (no embeddings).
        
        Args:
            image_bytes: JPEG/PNG encoded image
            confidence_threshold: Minimum detection confidence (0.0-1.0)
            max_faces: Maximum number of faces to return
            include_crops: Whether to include cropped face images
            max_image_dimension: Auto-resize if image exceeds this
            
        Returns:
            Dict with keys: success, faces, total_faces, time_ms, metrics
        """
        self._ensure_models_loaded()
        timer = Timer()

        # Decode + optional resize
        with timer.measure("decode"):
            image = self._decode_and_resize_image(
                image_bytes=image_bytes,
                max_dimension=max_image_dimension or self._validation.DEFAULT_RESIZE_DIMENSION,
            )
        
        self._validate_image(image)

        # Detection
        with timer.measure("detect"):
            detected_faces = self.detector.detect_with_quality(
                image,
                confidence_threshold=confidence_threshold,
                return_crops=include_crops,
            )

        # Limit results
        if max_faces and len(detected_faces) > max_faces:
            detected_faces = detected_faces[:max_faces]

        # Map to response format
        faces = [
            self._map_detected_face(face, include_crop=include_crops)
            for face in detected_faces
        ]

        logger.info(
            "detect_faces_completed",
            total_faces=len(faces),
            time_ms=timer.total_ms(),
        )

        return {
            "success": True,
            "faces": faces,
            "total_faces": len(faces),
            "time_ms": timer.total_ms(),
            "metrics": self._build_metrics(
                timer=timer,
                image_shape=image.shape,
                faces_detected=len(faces),
            ),
        }

    def extract_embeddings(
        self,
        image_bytes: bytes,
        camera_id: str = "unknown",
        confidence_threshold: float = 0.7,
        max_faces: int = 10,
        include_crops: bool = False,
        max_image_dimension: Optional[int] = None,
    ) -> Dict[str, Any]:
        """
        Detect faces and compute embeddings for each.
        
        Args:
            image_bytes: JPEG/PNG encoded image
            camera_id: Camera identifier for logging
            confidence_threshold: Minimum detection confidence
            max_faces: Maximum number of faces to process
            include_crops: Whether to include cropped face images
            max_image_dimension: Auto-resize if image exceeds this
            
        Returns:
            Dict with keys: success, face_detected, faces, camera_id, time_ms, metrics
        """
        self._ensure_models_loaded()
        timer = Timer()

        # Decode + resize
        with timer.measure("decode"):
            image = self._decode_and_resize_image(
                image_bytes=image_bytes,
                max_dimension=max_image_dimension or self._validation.DEFAULT_RESIZE_DIMENSION,
            )
        
        self._validate_image(image)

        # Detection
        with timer.measure("detect"):
            detected_faces = self.detector.detect_with_quality(
                image,
                confidence_threshold=confidence_threshold,
                return_crops=True,  # Always need crops for embeddings
            )

        # No faces found
        if not detected_faces:
            logger.info("extract_embeddings_no_face", camera_id=camera_id)
            return self._build_no_faces_response(
                camera_id=camera_id,
                timer=timer,
                image_shape=image.shape,
            )

        # Limit faces
        if max_faces and len(detected_faces) > max_faces:
            detected_faces = detected_faces[:max_faces]

        # Extract embeddings
        with timer.measure("embed"):
            embeddings = self._extract_embeddings_batch(detected_faces)

        # Map results
        faces = [
            self._map_face_with_embedding(face, emb, include_crop=include_crops)
            for face, emb in zip(detected_faces, embeddings)
        ]

        logger.info(
            "extract_embeddings_completed",
            camera_id=camera_id,
            faces=len(faces),
            time_ms=timer.total_ms(),
        )

        return {
            "success": True,
            "face_detected": True,
            "faces": faces,
            "camera_id": camera_id,
            "time_ms": timer.total_ms(),
            "metrics": self._build_metrics(
                timer=timer,
                image_shape=image.shape,
                faces_detected=len(faces),
            ),
        }

    def process_frame(
        self,
        frame: np.ndarray,
        camera_id: str = "unknown",
        confidence_threshold: float = 0.7,
        max_faces: int = 10,
        skip_embedding: bool = False,
    ) -> Dict[str, Any]:
        """
        Process a single video frame (already decoded np.ndarray).
        
        Optimized for streaming pipelines - no image decoding overhead.
        
        Args:
            frame: BGR OpenCV image (np.ndarray)
            camera_id: Camera identifier
            confidence_threshold: Minimum detection confidence
            max_faces: Maximum faces to process
            skip_embedding: If True, only detect faces (faster)
            
        Returns:
            Dict with keys: success, faces, frame_id, camera_id, time_ms, metrics
        """
        self._ensure_models_loaded()
        timer = Timer()
        self._frame_counter += 1

        h, w = frame.shape[:2]

        # Detection
        with timer.measure("detect"):
            detected_faces = self.detector.detect_with_quality(
                frame,
                confidence_threshold=confidence_threshold,
                return_crops=not skip_embedding,
            )

        # No faces
        if not detected_faces:
            logger.debug(
                "process_frame_no_faces",
                camera_id=camera_id,
                frame_id=self._frame_counter,
            )
            return self._build_frame_response(
                faces=[],
                frame_id=self._frame_counter,
                camera_id=camera_id,
                timer=timer,
                image_width=w,
                image_height=h,
            )

        # Limit faces
        if max_faces and len(detected_faces) > max_faces:
            detected_faces = detected_faces[:max_faces]

        # Process faces
        if skip_embedding:
            # Detection only (fast path)
            faces = [
                self._map_detected_face(face, include_crop=False)
                for face in detected_faces
            ]
        else:
            # Detection + embeddings
            with timer.measure("embed"):
                embeddings = self._extract_embeddings_batch(detected_faces)
            
            faces = [
                self._map_face_with_embedding(face, emb, include_crop=False)
                for face, emb in zip(detected_faces, embeddings)
            ]

        logger.debug(
            "process_frame_completed",
            camera_id=camera_id,
            frame_id=self._frame_counter,
            faces=len(faces),
            time_ms=timer.total_ms(),
        )

        return self._build_frame_response(
            faces=faces,
            frame_id=self._frame_counter,
            camera_id=camera_id,
            timer=timer,
            image_width=w,
            image_height=h,
        )

    def get_model_info(self) -> Dict[str, Any]:
        """Get model configuration and status."""
        self._ensure_models_loaded()

        info = {
            "model_name": "FaceNet (InceptionResnetV1)",
            "model_version": "vggface2",
            "device": self.embedder.config.device,
            "model_size_mb": 90.0,
            "input_size": self.embedder.config.image_size,
            "embedding_dim": 512,
            "total_faces_enrolled": 0,
            "is_ready": True,
            "detector_type": "MTCNN",
            "detector_config": {
                "min_face_size": self.detector.config.min_face_size,
                "confidence_threshold": float(self.detector.config.thresholds[2]),
                "max_faces": self.detector.config.max_faces,
                "keep_all": self.detector.config.keep_all,
            },
        }

        logger.info("get_model_info", **info)
        return info

    # =========================================================================
    # INTERNAL HELPERS
    # =========================================================================

    def _ensure_models_loaded(self) -> None:
        """Lazily initialize detector and embedder once."""
        if self.detector is not None and self.embedder is not None:
            return

        # Detector config
        detection_cfg = DetectionConfig(
            min_face_size=40,
            max_faces=10,
            quality_threshold=0.3,
            keep_all=True,
        )
        self.detector = FaceDetector(detection_cfg)

        # Embedder config
        embedder_cfg = EmbedderConfig(
            input_color_space="rgb",
            pretrained="vggface2",
            normalize_l2=True,
            batch_size=32,  # GPU: 32, CPU: 16
        )
        self.embedder = FaceEmbedder(embedder_cfg)

        logger.info(
            "face_models_loaded",
            detector="MTCNN",
            embedder="FaceNet",
            device=self.embedder.config.device,
        )

    def _decode_and_resize_image(
        self,
        image_bytes: bytes,
        max_dimension: int,
    ) -> np.ndarray:
        """
        Decode image bytes and optionally resize.
        
        Raises:
            InvalidImageException: If decoding fails or image is invalid
        """
        # Decode
        np_arr = np.frombuffer(image_bytes, np.uint8)
        image = cv2.imdecode(np_arr, cv2.IMREAD_COLOR)

        if image is None:
            raise InvalidImageException("Could not decode image bytes")

        h, w = image.shape[:2]

        # Skip resize if not needed
        if max_dimension <= 0 or max(h, w) <= max_dimension:
            return image

        # Resize
        scale = float(max_dimension) / float(max(h, w))
        new_w = max(1, int(round(w * scale)))
        new_h = max(1, int(round(h * scale)))

        logger.debug(
            "image_resized",
            orig_w=w,
            orig_h=h,
            new_w=new_w,
            new_h=new_h,
            scale=round(scale, 3),
        )

        return cv2.resize(image, (new_w, new_h), interpolation=cv2.INTER_AREA)

    def _validate_image(self, image: np.ndarray) -> None:
        """
        Validate image dimensions.
        
        Raises:
            InvalidImageException: If image is too large or too small
        """
        h, w = image.shape[:2]

        # Too large
        if h * w > self._validation.MAX_PIXELS:
            raise InvalidImageException(
                f"Image too large: {w}x{h} (max {self._validation.MAX_PIXELS} pixels)"
            )

        # Too small
        if h < self._validation.MIN_DIMENSION or w < self._validation.MIN_DIMENSION:
            raise InvalidImageException(
                f"Image too small: {w}x{h} (min {self._validation.MIN_DIMENSION}px)"
            )

    def _extract_embeddings_batch(
        self,
        detected_faces: List[DetectedFace],
    ) -> List[np.ndarray]:
        """
        Extract embeddings for all faces in a single batch.
        
        Returns:
            List of embeddings (same length as input)
        """
        crops = [f.crop for f in detected_faces if f.crop is not None]
        
        if not crops:
            logger.warning("no_valid_crops_for_embedding")
            return [np.zeros(512) for _ in detected_faces]  # Return zero vectors
        
        embeddings = self.embedder.embed_batch(crops)
        
        # Validate count
        if len(embeddings) != len(crops):
            logger.error(
                "embedding_count_mismatch",
                expected=len(crops),
                actual=len(embeddings),
            )
            # Pad with zeros if mismatch
            while len(embeddings) < len(crops):
                embeddings.append(np.zeros(512))
        
        return embeddings

    # =========================================================================
    # RESPONSE BUILDERS
    # =========================================================================

    @staticmethod
    def _map_detected_face(
        face: DetectedFace,
        include_crop: bool = False,
    ) -> Dict[str, Any]:
        """Map DetectedFace to dict (no embedding)."""
        x, y, w, h = face.bbox
        
        face_dict = {
            "face_id": 0,  # Will be set by caller
            "bbox": (float(x), float(y), float(w), float(h)),
            "confidence": float(face.confidence),
            "quality": {
                "overall_score": float(face.quality.overall_score),
                "sharpness": float(face.quality.sharpness),
                "brightness": float(face.quality.brightness),
                "face_size_pixels": int(face.quality.face_size_pixels),
            },
        }
        
        if include_crop and face.crop is not None:
            _, buffer = cv2.imencode(
                ".jpg",
                cv2.cvtColor(face.crop, cv2.COLOR_RGB2BGR),
            )
            face_dict["crop_jpeg"] = buffer.tobytes()
        
        return face_dict

    @staticmethod
    def _map_face_with_embedding(
        face: DetectedFace,
        embedding: np.ndarray,
        include_crop: bool = False,
    ) -> Dict[str, Any]:
        """Map DetectedFace + embedding to dict."""
        x, y, w, h = face.bbox
        
        face_dict = {
            # "face_id": 0,  # Will be set by caller
            "bbox": (float(x), float(y), float(w), float(h)),
            "confidence": float(face.confidence),
            "embedding": [float(v) for v in embedding.tolist()],
            "quality": {
                "overall_score": float(face.quality.overall_score),
                "sharpness": float(face.quality.sharpness),
                "brightness": float(face.quality.brightness),
                "face_size_pixels": int(face.quality.face_size_pixels),
            },
        }
        
        if include_crop and face.crop is not None:
            _, buffer = cv2.imencode(
                ".jpg",
                cv2.cvtColor(face.crop, cv2.COLOR_RGB2BGR),
            )
            face_dict["crop_jpeg"] = buffer.tobytes()
        
        return face_dict

    @staticmethod
    def _build_metrics(
        timer: 'Timer',
        image_shape: tuple,
        faces_detected: int,
    ) -> Dict[str, Any]:
        """Build metrics dict."""
        h, w = image_shape[:2]
        
        return {
            "preprocessing_ms": timer.get("decode", 0.0),
            "detection_ms": timer.get("detect", 0.0),
            "embedding_ms": timer.get("embed", 0.0),
            "total_ms": timer.total_ms(),
            "image_width": int(w),
            "image_height": int(h),
            "faces_detected": faces_detected,
        }

    def _build_no_faces_response(
        self,
        camera_id: str,
        timer: 'Timer',
        image_shape: tuple,
    ) -> Dict[str, Any]:
        """Build response when no faces detected."""
        return {
            "success": True,
            "face_detected": False,
            "faces": [],
            "camera_id": camera_id,
            "time_ms": timer.total_ms(),
            "metrics": self._build_metrics(
                timer=timer,
                image_shape=image_shape,
                faces_detected=0,
            ),
        }

    def _build_frame_response(
        self,
        faces: List[Dict[str, Any]],
        frame_id: int,
        camera_id: str,
        timer: 'Timer',
        image_width: int,
        image_height: int,
    ) -> Dict[str, Any]:
        """Build process_frame response."""
        # Set face IDs
        for i, face in enumerate(faces):
            face["face_id"] = i
        
        return {
            "success": True,
            "faces": faces,
            "frame_id": frame_id,
            "camera_id": camera_id,
            "time_ms": timer.total_ms(),
            "metrics": {
                "preprocessing_ms": 0.0,
                "detection_ms": timer.get("detect", 0.0),
                "embedding_ms": timer.get("embed", 0.0),
                "total_ms": timer.total_ms(),
                "image_width": image_width,
                "image_height": image_height,
                "faces_detected": len(faces),
            },
        }


# =============================================================================
# UTILITY CLASS
# =============================================================================

class Timer:
    """Simple timer for tracking operation durations."""
    
    def __init__(self):
        self._start = time.time()
        self._measurements: Dict[str, float] = {}
    
    def measure(self, name: str):
        """Context manager for timing operations."""
        return TimerContext(self, name)
    
    def record(self, name: str, duration_ms: float) -> None:
        """Record a measurement."""
        self._measurements[name] = duration_ms
    
    def get(self, name: str, default: float = 0.0) -> float:
        """Get a measurement by name."""
        return self._measurements.get(name, default)
    
    def total_ms(self) -> float:
        """Total elapsed time in milliseconds."""
        return (time.time() - self._start) * 1000.0


class TimerContext:
    """Context manager for Timer.measure()."""
    
    def __init__(self, timer: Timer, name: str):
        self.timer = timer
        self.name = name
        self.start = 0.0
    
    def __enter__(self):
        self.start = time.time()
        return self
    
    def __exit__(self, *args):
        duration_ms = (time.time() - self.start) * 1000.0
        self.timer.record(self.name, duration_ms)