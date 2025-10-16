from fastapi import APIRouter, UploadFile, File, HTTPException
import time
import structlog

from ...core.container import get_detection_app
from ...schemas.detection import DetectRequest
from ..metrics.registry import track_inference  


router = APIRouter(prefix="/api/v1", tags=["detection"])
logger = structlog.get_logger()


@router.post("/detect", summary="Detect objects (REST test)")
async def detect_objects(file: UploadFile = File(...)):
    """
    Thin HTTP adapter around the application layer.
    """
    try:
        t0 = time.perf_counter()

        if file.content_type not in {"image/jpeg", "image/png", "image/jpg"}:
            raise HTTPException(status_code=400, detail="Unsupported content type")

        image_bytes = await file.read()
        if not image_bytes:
            raise HTTPException(status_code=400, detail="Empty file")

        app = get_detection_app()

        req = DetectRequest(
            image=image_bytes,                  # bytes; app will decode to ndarray
            confidence_threshold=None,          # None -> use runner defaults
            iou_threshold=None,
            target_classes=[],
            exclude_classes=[],
            camera_id=None,
            timestamp=None,
            request_id=None,
            enable_tracking=False,
            return_cropped_images=False,
            max_detections=None
        )
        resp = app.detect(req)

        if track_inference:
            # keep units consistent; record seconds, not ms
            track_inference("yolov8s", time.perf_counter() - t0, success=resp.success)

        if not resp.success:
            raise HTTPException(status_code=500, detail=resp.error_message or "Detection failed")

        return resp

    except HTTPException:
        raise
    except Exception as e:
        logger.error("rest_detect_failed", error=str(e))
        raise HTTPException(status_code=500, detail="Internal error during detection")
