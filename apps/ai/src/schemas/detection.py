from pydantic import BaseModel, Field
from typing import List, Optional


class BoundingBox(BaseModel):
    x1: float = Field(..., description="Top-left X coordinate")
    y1: float = Field(..., description="Top-left Y coordinate")
    x2: float = Field(..., description="Bottom-right X coordinate")
    y2: float = Field(..., description="Bottom-right Y coordinate")


class DetectedObject(BaseModel):
    class_name: str = Field(..., description="Detected object class")
    confidence: float = Field(..., ge=0.0, le=1.0, description="Detection confidence")
    bbox: BoundingBox = Field(..., description="Bounding box coordinates")


class DetectionResponse(BaseModel):
    success: bool
    detections: List[DetectedObject] = []
    inference_time_ms: float
    error: Optional[str] = None
