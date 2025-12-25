from functools import lru_cache
import inspect

from ..core.config import settings
from ..application.detction_app import DetectionApp

from ..services.ml.object_detection import ObjectDetectionService


def _build_detection_service() -> ObjectDetectionService:
    """
    Create ObjectDetectionService with only the kwargs it actually supports.
    Pulls values from Settings and filters by constructor signature.
    """
    # Prefer a concrete weights path; fall back to model type (Ultralytics auto-download)
    weights_path = settings.get_model_path()

    # Superset of commonly used ctor names across implementations
    superset = {
        # identity / weights
        "model_name": settings.detection_model_name,   # e.g., "yolov8s"
        "model_path": weights_path,
        "weights":    weights_path,

        # device / precision
        "device": settings.DETECTION_DEVICE,           # "cuda" / "cpu" / "mps"
        "half":   settings.DETECTION_HALF_PRECISION,   # many YOLO wrappers accept 'half'

        # thresholds
        "conf":                 settings.DETECTION_CONFIDENCE,
        "confidence_threshold": settings.DETECTION_CONFIDENCE,
        "iou":                  settings.DETECTION_IOU_THRESHOLD,
        "iou_threshold":        settings.DETECTION_IOU_THRESHOLD,

        # limits / size
        "max_det":       settings.DETECTION_MAX_DETECTIONS,
        "max_detections":settings.DETECTION_MAX_DETECTIONS,
        "imgsz":         settings.DETECTION_IMAGE_SIZE,  # ultralytics kw
        "image_size":    settings.DETECTION_IMAGE_SIZE,
        "img_size":      settings.DETECTION_IMAGE_SIZE,

        # class filtering (optional if your runner supports)
        "classes": settings.DETECTION_CLASSES,
    }

    # Filter None (shouldn’t be any) and keep only supported kwargs
    superset = {k: v for k, v in superset.items() if v is not None}
    supported = set(inspect.signature(ObjectDetectionService.__init__).parameters.keys()) - {"self"}
    kwargs = {k: v for k, v in superset.items() if k in supported}

    try:
        return ObjectDetectionService(**kwargs)
    except TypeError:
        # If your service expects no kwargs at all
        return ObjectDetectionService()


@lru_cache(maxsize=1)
def get_object_detection_service() -> ObjectDetectionService:
    return _build_detection_service()


@lru_cache(maxsize=1)
def get_detection_app() -> DetectionApp:
    return DetectionApp(get_object_detection_service())
