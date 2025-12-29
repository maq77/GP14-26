"""
apps/ai/src/services/ml/object_detection.py
Object Detection Service - Business Logic Layer
"""

import time
from typing import List, Optional
import numpy as np
import cv2

from src.core.logging import get_logger, LogContext
from src.core.exceptions import InvalidImageException, InferenceException
from src.schemas.detection import (
    DetectRequest,
    DetectResponse,
    DetectBatchRequest,
    DetectBatchResponse,
    Detection,
    ImageMetadata,
)
from src.models.object.model_loader import get_detector


logger = get_logger("object_detection_service")


class ObjectDetectionService:
    """
    Object Detection Service
    Handles detection requests and coordinates with YOLO detector
    """
    
    def __init__(self):
        """Initialize detection service"""
        self.detector = None
        logger.info("object_detection_service_initialized")
    
    def _ensure_detector_loaded(self):
        """Ensure YOLO detector is loaded"""
        if self.detector is None:
            self.detector = get_detector()
    
    def _decode_image(self, image_bytes: bytes) -> np.ndarray:
        """
        Decode image bytes to numpy array
        
        Args:
            image_bytes: Image bytes (JPEG/PNG)
        
        Returns:
            Numpy array (BGR format)
        
        Raises:
            InvalidImageException: If decoding fails
        """
        try:
            # Convert bytes to numpy array
            nparr = np.frombuffer(image_bytes, np.uint8)
            
            # Decode image
            image = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
            
            if image is None:
                raise InvalidImageException("Could not decode image bytes")
            
            return image
            
        except Exception as e:
            logger.error("image_decode_failed", error=str(e))
            raise InvalidImageException(f"Failed to decode image: {str(e)}")
    
    def detect_objects(self, request: DetectRequest) -> DetectResponse:
        """
        Detect objects in image
        
        Args:
            request: Detection request
        
        Returns:
            Detection response
        """
        # Ensure detector is loaded
        self._ensure_detector_loaded()
        
        # Add request context to logs
        with LogContext(
            request_id=request.request_id,
            camera_id=request.camera_id
        ):
            try:
                logger.info(
                    "detection_request_received",
                    image_size_bytes=len(request.image),
                    conf_threshold=request.confidence_threshold
                )
                
                # Decode image
                image = self._decode_image(request.image)
                
                # Run detection
                detections, image_metadata, metrics = self.detector.predict(
                    image=image,
                    conf_threshold=request.confidence_threshold,
                    iou_threshold=request.iou_threshold,
                    target_classes=request.target_classes or None,
                    max_detections=request.max_detections
                )
                
                # Filter by exclude_classes
                if request.exclude_classes:
                    detections = [
                        d for d in detections 
                        if d.class_name not in request.exclude_classes
                    ]
                
                # Create response
                response = DetectResponse(
                    success=True,
                    detections=detections,
                    total_objects=len(detections),
                    inference_time_ms=metrics["inference_time_ms"],
                    preprocessing_time_ms=metrics["preprocessing_time_ms"],
                    postprocessing_time_ms=metrics["postprocessing_time_ms"],
                    total_time_ms=metrics["total_time_ms"],
                    request_id=request.request_id,
                    timestamp=request.timestamp or int(time.time() * 1000),
                    image_metadata=image_metadata
                )
                
                logger.info(
                    "detection_completed",
                    num_detections=len(detections),
                    total_time_ms=metrics["total_time_ms"]
                )
                
                return response
                
            except InvalidImageException as e:
                logger.error("invalid_image", error=str(e))
                return DetectResponse(
                    success=False,
                    error_message=f"Invalid image: {str(e)}",
                    request_id=request.request_id,
                    timestamp=int(time.time() * 1000)
                )
            
            except InferenceException as e:
                logger.error("inference_failed", error=str(e))
                return DetectResponse(
                    success=False,
                    error_message=f"Inference failed: {str(e)}",
                    request_id=request.request_id,
                    timestamp=int(time.time() * 1000)
                )
            
            except Exception as e:
                logger.error("unexpected_error", error=str(e), exc_info=True)
                return DetectResponse(
                    success=False,
                    error_message=f"Unexpected error: {str(e)}",
                    request_id=request.request_id,
                    timestamp=int(time.time() * 1000)
                )
    
    def detect_waste(self, request: DetectRequest) -> DetectResponse:
        """
        Detect waste/trash specifically
        
        Args:
            request: Detection request
        
        Returns:
            Detection response with waste objects only
        """
        # Override target_classes with waste classes
        from .core.config import settings
        request.target_classes = settings.WASTE_CLASSES
        
        # Lower confidence threshold for waste
        if request.confidence_threshold > settings.WASTE_CONFIDENCE:
            request.confidence_threshold = settings.WASTE_CONFIDENCE
        
        logger.info("waste_detection_request", camera_id=request.camera_id)
        return self.detect_objects(request)
    
    def detect_vandalism(self, request: DetectRequest) -> DetectResponse:
        """
        Detect vandalism (property damage, graffiti)
        Note: This requires a custom trained model
        Currently uses general object detection
        
        Args:
            request: Detection request
        
        Returns:
            Detection response
        """
        # TODO: Implement custom vandalism detection model
        # For now, use general detection
        logger.info("vandalism_detection_request", camera_id=request.camera_id)
        return self.detect_objects(request)
    
    def detect_batch(self, request: DetectBatchRequest) -> DetectBatchResponse:
        """
        Detect objects in batch of images
        
        Args:
            request: Batch detection request
        
        Returns:
            Batch detection response
        """
        logger.info(
            "batch_detection_request",
            batch_size=len(request.requests),
            parallel=request.parallel_processing
        )
        
        start_time = time.time()
        responses = []
        
        # Process each request
        # TODO: Implement parallel processing if request.parallel_processing=True
        for req in request.requests:
            response = self.detect_objects(req)
            responses.append(response)
        
        total_time = (time.time() - start_time) * 1000
        
        batch_response = DetectBatchResponse(
            responses=responses,
            total_time_ms=round(total_time, 2)
        )
        
        logger.info(
            "batch_detection_completed",
            batch_size=len(responses),
            total_detections=batch_response.total_detections,
            success_rate=batch_response.success_rate,
            total_time_ms=total_time
        )
        
        return batch_response


# ============================================================================
# Export
# ============================================================================

__all__ = ["ObjectDetectionService"]