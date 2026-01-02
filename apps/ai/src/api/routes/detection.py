from fastapi import APIRouter, UploadFile, File, HTTPException
import numpy as np
import cv2
import time
from typing import List
import structlog

from src.schemas.detection import DetectResponse, BoundingBox, Detection, DetectRequest
from src.core.container import get_detection_app

router = APIRouter(prefix="/api/v1", tags=["detection"])
logger = structlog.get_logger()
detector = get_detection_app()  # Initialize your detection application


@router.post("/detect", response_model=DetectResponse, summary="Detect objects (REST test)")
async def detect_objects(file: UploadFile = File(...)):
    """
    Detect objects in an image using your YOLO pipeline.
    This is a REST testing endpoint (use gRPC in production).
    """
    try:
        start_time = time.perf_counter()

        if file.content_type not in {"image/jpeg", "image/png", "image/jpg"}:
            raise HTTPException(status_code=400, detail="Unsupported content type")

        image_bytes = await file.read()
        if not image_bytes:
            raise HTTPException(status_code=400, detail="Empty file")

        request = DetectRequest(image=image_bytes)
        response = detector.detect(request)

        if not response.success:
            raise HTTPException(status_code=500, detail=response.error_message or "Detection failed")

        logger.info(
            "rest_detection_done",
            total_objects=response.total_objects,
            time_ms=response.inference_time_ms,
        )
        return response

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
