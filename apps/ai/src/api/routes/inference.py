from fastapi import APIRouter, UploadFile, File, HTTPException
import numpy as np
import cv2
import time
from typing import List
import structlog

from src.schemas.detection import DetectionResponse, BoundingBox, DetectedObject
from apps.ai.src.services.ml.object_detection import ObjectDetectionService 

router = APIRouter()
logger = structlog.get_logger()
detector = ObjectDetectionService()

@router.post("/detect", response_model=DetectionResponse)
async def detect_objects(file: UploadFile = File(...)):
    """
    Detect objects in an image using YOLO
    """
    try:
        start_time = time.time()
        image_bytes = await file.read()

        nparr = np.frombuffer(image_bytes, np.uint8)
        image = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
        if image is None:
            raise HTTPException(status_code=400, detail="Invalid image format")

        detections = detector.detect_objects(image)
        inference_time = (time.time() - start_time) * 1000

        detected_objects = [
            DetectedObject(
                class_name=d["class"],
                confidence=d["confidence"],
                bbox=BoundingBox(
                    x1=d["bbox"][0],
                    y1=d["bbox"][1],
                    x2=d["bbox"][2],
                    y2=d["bbox"][3],
                ),
            )
            for d in detections
        ]

        return DetectionResponse(
            success=True,
            detections=detected_objects,
            inference_time_ms=inference_time,
        )

    except Exception as e:
        logger.error("Object detection failed", error=str(e))
        raise HTTPException(status_code=500, detail=str(e))


@router.post("/face/verify")
async def verify_face(file: UploadFile = File(...)):
    """
    Verify a face against known faces
    """
    try:
        contents = await file.read()
        
        # TODO: Implement face verification
        
        return {
            "success": True,
            "match_found": False,
            "confidence": 0.0
        }
    except Exception as e:
        logger.error("Face verification failed", error=str(e))
        raise HTTPException(status_code=500, detail=str(e))
