"""
apps/ai/src/schemas/detection.py
Data Transfer Objects for object detection
"""

from pydantic import Field, field_validator, ConfigDict
from typing import List, Optional
from datetime import datetime
from .base import BaseModel

# ============================================================================
# Bounding Box
# ============================================================================

class BoundingBox(BaseModel):
    """Bounding box coordinates"""
    x1: float = Field(..., description="Top-left X coordinate")
    y1: float = Field(..., description="Top-left Y coordinate")
    x2: float = Field(..., description="Bottom-right X coordinate")
    y2: float = Field(..., description="Bottom-right Y coordinate")
    
    # Normalized coordinates (0.0-1.0)
    x1_norm: Optional[float] = Field(None, ge=0.0, le=1.0)
    y1_norm: Optional[float] = Field(None, ge=0.0, le=1.0)
    x2_norm: Optional[float] = Field(None, ge=0.0, le=1.0)
    y2_norm: Optional[float] = Field(None, ge=0.0, le=1.0)
    
    @property
    def width(self) -> float:
        """Bounding box width"""
        return self.x2 - self.x1
    
    @property
    def height(self) -> float:
        """Bounding box height"""
        return self.y2 - self.y1
    
    @property
    def area(self) -> float:
        """Bounding box area"""
        return self.width * self.height
    
    @property
    def center(self) -> tuple[float, float]:
        """Bounding box center point"""
        return ((self.x1 + self.x2) / 2, (self.y1 + self.y2) / 2)
    
    def to_xyxy(self) -> List[float]:
        """Convert to [x1, y1, x2, y2] format"""
        return [self.x1, self.y1, self.x2, self.y2]
    
    def to_xywh(self) -> List[float]:
        """Convert to [x, y, width, height] format"""
        return [self.x1, self.y1, self.width, self.height]


# ============================================================================
# Detection
# ============================================================================

class Detection(BaseModel):
    """Single object detection"""
    class_name: str = Field(..., description="Detected class name")
    class_id: int = Field(..., ge=0, description="Numeric class ID")
    confidence: float = Field(..., ge=0.0, le=1.0, description="Confidence score")
    bbox: BoundingBox = Field(..., description="Bounding box")
    
    # Optional fields
    track_id: Optional[int] = Field(None, description="Object tracking ID")
    cropped_image: Optional[bytes] = Field(None, description="Cropped detection image")
    area: Optional[float] = Field(None, description="Bounding box area")
    zone: Optional[str] = Field(None, description="Geofence zone")
    
    class Config:
        json_schema_extra = {
            "example": {
                "class_name": "person",
                "class_id": 0,
                "confidence": 0.95,
                "bbox": {
                    "x1": 100.0,
                    "y1": 150.0,
                    "x2": 300.0,
                    "y2": 450.0
                },
                "track_id": 5
            }
        }


# ============================================================================
# Image Metadata
# ============================================================================

class ImageMetadata(BaseModel):
    """Image metadata"""
    width: int = Field(..., gt=0)
    height: int = Field(..., gt=0)
    channels: int = Field(default=3, ge=1, le=4)
    format: str = Field(default="jpeg", description="Image format")
    
    @property
    def shape(self) -> tuple[int, int, int]:
        """Image shape (H, W, C)"""
        return (self.height, self.width, self.channels)
    
    @property
    def aspect_ratio(self) -> float:
        """Image aspect ratio"""
        return self.width / self.height


# ============================================================================
# Request Models
# ============================================================================

class DetectRequest(BaseModel):
    """Object detection request"""
    image: bytes = Field(..., description="Image bytes (JPEG/PNG)")
    
    # Detection parameters
    confidence_threshold: float = Field(default=0.25, ge=0.0, le=1.0)
    iou_threshold: float = Field(default=0.45, ge=0.0, le=1.0)
    
    # Optional filters
    target_classes: List[str] = Field(default_factory=list)
    exclude_classes: List[str] = Field(default_factory=list)
    
    # Metadata
    camera_id: Optional[str] = None
    timestamp: Optional[int] = None
    request_id: Optional[str] = None
    
    # Advanced options
    enable_tracking: bool = False
    return_cropped_images: bool = False
    max_detections: int = Field(default=300, ge=1, le=1000)
    
    @field_validator("image")
    def validate_image_size(cls, v):
        """Ensure image is not too large"""
        max_size = 10 * 1024 * 1024  # 10MB
        if len(v) > max_size:
            raise ValueError(f"Image too large (max {max_size} bytes)")
        return v


class DetectBatchRequest(BaseModel):
    """Batch detection request"""
    requests: List[DetectRequest]
    parallel_processing: bool = Field(default=True)
    
    @field_validator("requests")
    def validate_batch_size(cls, v):
        """Ensure batch is not too large"""
        max_batch = 100
        if len(v) > max_batch:
            raise ValueError(f"Batch too large (max {max_batch} requests)")
        return v


# ============================================================================
# Response Models
# ============================================================================

class DetectResponse(BaseModel):
    """Object detection response"""
    success: bool
    error_message: Optional[str] = None
    
    # Detections
    detections: List[Detection] = Field(default_factory=list)
    total_objects: int = 0
    
    # Performance metrics
    inference_time_ms: float = 0.0
    preprocessing_time_ms: float = 0.0
    postprocessing_time_ms: float = 0.0
    total_time_ms: float = 0.0
    
    # Metadata
    request_id: Optional[str] = None
    timestamp: Optional[int] = None
    image_metadata: Optional[ImageMetadata] = None
    
    class Config:
        json_schema_extra = {
            "example": {
                "success": True,
                "detections": [
                    {
                        "class_name": "person",
                        "class_id": 0,
                        "confidence": 0.95,
                        "bbox": {
                            "x1": 100.0,
                            "y1": 150.0,
                            "x2": 300.0,
                            "y2": 450.0
                        }
                    }
                ],
                "total_objects": 1,
                "inference_time_ms": 45.2,
                "total_time_ms": 52.8
            }
        }


class DetectBatchResponse(BaseModel):
    """Batch detection response"""
    responses: List[DetectResponse]
    total_time_ms: float = 0.0
    
    @property
    def total_detections(self) -> int:
        """Total detections across all responses"""
        return sum(r.total_objects for r in self.responses)
    
    @property
    def success_rate(self) -> float:
        """Success rate (0.0-1.0)"""
        if not self.responses:
            return 0.0
        successful = sum(1 for r in self.responses if r.success)
        return successful / len(self.responses)


# ============================================================================
# Model Info
# ============================================================================

class ModelInfoRequest(BaseModel):
    """Model information request (empty)"""
    pass


class ModelInfoResponse(BaseModel):
    """Model information response"""
    # allow fields starting with "model_"
    model_config = ConfigDict(
        protected_namespaces=(),
        json_schema_extra={
            "example": {
                "model_name": "yolo11s",
                "model_version": "1.0.0",
                "classes": ["person", "car", "truck"],
                "num_classes": 80,
                "device": "cuda",
                "model_size_mb": 22.5,
                "input_size": 640,
            }
        },
    )

    model_name: str
    model_version: str
    classes: List[str]
    num_classes: int
    device: str
    model_size_mb: float
    input_size: int



# ============================================================================
# Export
# ============================================================================

__all__ = [
    "BoundingBox",
    "Detection",
    "ImageMetadata",
    "DetectRequest",
    "DetectBatchRequest",
    "DetectResponse",
    "DetectBatchResponse",
    "ModelInfoRequest",
    "ModelInfoResponse",
]