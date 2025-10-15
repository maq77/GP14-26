from fastapi import APIRouter, UploadFile, File, HTTPException
import numpy as np
import cv2
import time
from typing import List
import structlog

# If your PYTHONPATH is repo root and you expose "src", these imports work:
from src.schemas.detection import DetectResponse, BoundingBox, Detection
from apps.ai.src.services.ml.object_detection import ObjectDetectionService

# If you instead import via fully-qualified package, use this pair instead:
from apps.ai.src.schemas.detection import DetectResponse, BoundingBox, Detection
from apps.ai.src.services.ml.object_detection import ObjectDetectionService

router = APIRouter(prefix="/api/v1", tags=["detection"])
logger = structlog.get_logger()
detector = ObjectDetectionService()


@router.post("/detect", response_model=DetectResponse, summary="Detect objects (REST test)")
async def detect_objects(file: UploadFile = File(...)):
    """
    Detect objects in an image using your YOLO pipeline.
    This is a REST testing endpoint (use gRPC in production).
    """
    try:
        start_time = time.time()

        # Basic content-type guard (optional)
        if file.content_type not in {"image/jpeg", "image/png", "image/jpg"}:
            raise HTTPException(status_code=400, detail="Unsupported content type")

        image_bytes = await file.read()
        nparr = np.frombuffer(image_bytes, np.uint8)
        image = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
        if image is None:
            raise HTTPException(status_code=400, detail="Invalid image bytes")

        # Your service should return a list like:
        # [{"class": "person", "confidence": 0.91, "bbox": [x1,y1,x2,y2], ...}, ...]
        detections = detector.detect_objects(image)

        inference_time = (time.time() - start_time) * 1000.0

        detected_objects: List[Detection] = [
            Detection(
                class_name=d.get("class") or d.get("class_name", ""),
                confidence=float(d["confidence"]),
                bbox=BoundingBox(
                    x1=float(d["bbox"][0]),
                    y1=float(d["bbox"][1]),
                    x2=float(d["bbox"][2]),
                    y2=float(d["bbox"][3]),
                ),
                # Optional fields if your service returns them:
                class_id=d.get("class_id"),
                track_id=d.get("track_id"),
            )
            for d in detections
            if "bbox" in d and len(d["bbox"]) == 4
        ]

        return DetectResponse(
            success=True,
            detections=detected_objects,
            total_objects=len(detected_objects),
            inference_time_ms=inference_time,
        )

    except HTTPException:
        raise
    except Exception as e:
        logger.error("object_detection_failed", error=str(e))
        raise HTTPException(status_code=500, detail="Internal error during detection")


@router.post("/face/verify", summary="Verify a face (stub)")
async def verify_face(file: UploadFile = File(...)):
    """
    Stub for face verification (placeholder).
    """
    try:
        _ = await file.read()
        # TODO: Implement face verification when ready.
        return {"success": True, "match_found": False, "confidence": 0.0}
    except Exception as e:
        logger.error("face_verification_failed", error=str(e))
        raise HTTPException(status_code=500, detail="Internal error during face verification")
