from __future__ import annotations
import structlog
from typing import Any, Dict, Optional
import numpy as np
import cv2
from packages.contracts.python.face_pb2 import (
    FaceDetectResponse,
    FaceEmbeddingResponse,
    FaceVerifyResponse,
    FaceEnrollResponse,
    FaceModelInfoResponse,
    FrameProcessResponse,
    Face,
    DetectedFace,
    BoundingBox,
    ErrorCode,
    PerformanceMetrics,
)
from packages.contracts.python.face_pb2_grpc import FaceServiceServicer

<<<<<<< HEAD
from src.services.ml.Face_Recognition_Service import FaceRecognitionService
from src.core.exceptions import InvalidImageException
=======
from ....services.ml.Face_Recognition_Service import FaceRecognitionService
from ....core.exceptions import InvalidImageException
>>>>>>> main

logger = structlog.get_logger("grpc.face_servicer")


class FaceServicer(FaceServiceServicer):
    """
    gRPC FaceService implementation that wraps FaceRecognitionService.
    This layer:
      - Maps protobuf requests to Python dicts / numpy
      - Calls FaceRecognitionService
      - Maps results back into protobuf responses
    """

    def __init__(self, face_service: FaceRecognitionService) -> None:
        self.face_service = face_service
        logger.info("face_servicer_initialized")

    # -------------------------------------------------------------------------
    # DetectFaces -> FaceDetectResponse { repeated DetectedFace }
    # -------------------------------------------------------------------------
    def DetectFaces(self, request, context):
        try:
            result = self.face_service.detect_faces(
                image_bytes=request.image,
                confidence_threshold=request.confidence_threshold or 0.7,
                max_faces=request.max_faces or 0,
                include_crops=request.include_crops,
                max_image_dimension=request.max_image_dimension or 0,
            )

            faces_result = result.get("faces", [])
            resp = FaceDetectResponse(
                success=bool(result.get("success", False)),
                total_faces=len(faces_result),
                total_time_ms=float(result.get("time_ms", 0.0)),
            )
            
            # Map metrics if present
            metrics = self._map_metrics_to_proto(result.get("metrics"))
            if metrics:
                resp.metrics.CopyFrom(metrics)

            # OPTIMIZED: Use helper methods
            for f in faces_result:
                det = DetectedFace()
                
                # Bbox mapping
                bbox_proto = self._map_bbox_to_proto(f.get("bbox"))
                if bbox_proto:
                    det.bbox.CopyFrom(bbox_proto)
                
                det.confidence = float(f.get("confidence", 1.0))
                
                # Crop image
                crop = f.get("crop_jpeg") or f.get("cropped_image")
                if crop is not None:
                    det.cropped_image = crop
                
                # Face ID
                face_id = f.get("face_id")
                if face_id is not None:
                    det.face_id = int(face_id)
                
                # Quality mapping
                self._map_quality_to_proto(f.get("quality"), det)
                
                resp.faces.append(det)

            logger.info(
                "detect_faces_rpc_completed",
                total_faces=len(resp.faces),
                time_ms=resp.total_time_ms,
            )
            return resp

        except InvalidImageException as e:
            logger.error("detect_faces_rpc_invalid_image", error=str(e), exc_info=True)
            return FaceDetectResponse(success=False, error_message=str(e))

        except Exception as e:
            logger.error("detect_faces_rpc_error", error=str(e), exc_info=True)
            return FaceDetectResponse(success=False, error_message=str(e))
    # -------------------------------------------------------------------------
    # ExtractEmbedding / ExtractEmbeddings -> FaceEmbeddingResponse
    #   - Multi-face
    #   - Legacy & new RPC names share same implementation
    # -------------------------------------------------------------------------

    def ExtractEmbedding(self, request, context):
        return self._extract_embeddings_impl(request, context)

    def ExtractEmbeddings(self, request, context):
        return self._extract_embeddings_impl(request, context)

    def _extract_embeddings_impl(self, request, context):
        try:
            result = self.face_service.extract_embeddings(
                image_bytes=request.image,
                camera_id=request.camera_id or "unknown",
                confidence_threshold=request.confidence_threshold or 0.7,
                max_faces=request.max_faces or 0,
                include_crops=request.include_crops,
                max_image_dimension=request.max_image_dimension or 0,
            )

            faces_result = result.get("faces") or []

            resp = FaceEmbeddingResponse(
                success=bool(result.get("success", False)),
                face_detected=bool(result.get("face_detected", False)),
                total_time_ms=float(result.get("time_ms", 0.0)),
                camera_id=result.get("camera_id", "") or (request.camera_id or ""),
                error_code=ErrorCode.ERROR_CODE_UNSPECIFIED,
            )
            
            # Map metrics
            metrics = self._map_metrics_to_proto(result.get("metrics"))
            if metrics:
                resp.metrics.CopyFrom(metrics)

            # Check for failures
            if not resp.success:
                resp.error_message = result.get("error_message", "embedding_failed")
                logger.error("extract_embeddings_rpc_failed_flag", error_message=resp.error_message)
                return resp

            # No faces found
            if not faces_result:
                resp.success = False
                resp.face_detected = False
                resp.error_code = ErrorCode.NO_FACE_FOUND
                logger.info("extract_embeddings_rpc_no_faces", time_ms=resp.total_time_ms)
                return resp

            # OPTIMIZED: Map faces with helper methods
            for f in faces_result:
                face_msg = Face()
                
                # Bbox
                bbox_proto = self._map_bbox_to_proto(f.get("bbox"))
                if bbox_proto:
                    face_msg.bbox.CopyFrom(bbox_proto)
                
                # Embedding
                embedding = f.get("embedding") or []
                if embedding:
                    face_msg.embedding_vector.extend(float(v) for v in embedding)
                
                face_msg.confidence = float(f.get("confidence", 1.0))
                
                # Crop
                crop = f.get("crop_jpeg") or f.get("cropped_image")
                if crop is not None:
                    face_msg.cropped_image = crop
                
                # Face ID
                face_id = f.get("face_id")
                if face_id is not None:
                    face_msg.face_id = int(face_id)
                
                # Quality
                self._map_quality_to_proto(f.get("quality"), face_msg)
                
                resp.faces.append(face_msg)

            logger.info(
                "extract_embedding_rpc_completed",
                face_detected=resp.face_detected,
                faces=len(resp.faces),
                time_ms=resp.total_time_ms,
            )
            return resp

        except InvalidImageException as e:
            logger.error("extract_embedding_rpc_invalid_image", error=str(e), exc_info=True)
            return FaceEmbeddingResponse(
                success=False,
                error_message=str(e),
                error_code=ErrorCode.INVALID_IMAGE,
            )

        except Exception as e:
            logger.error("extract_embedding_rpc_error", error=str(e), exc_info=True)
            return FaceEmbeddingResponse(
                success=False,
                error_message=str(e),
                error_code=ErrorCode.INTERNAL_ERROR,
            )
   
    # -------------------------------------------------------------------------
    # GetModelInfo
    # -------------------------------------------------------------------------
    def GetModelInfo(self, request, context):
        try:
            info = self.face_service.get_model_info()

            resp = FaceModelInfoResponse(
                model_name=str(info.get("model_name", "")),
                model_version=str(info.get("model_version", "")),
                device=str(info.get("device", "")),
                model_size_mb=float(info.get("model_size_mb", 0.0)),
                input_size=int(info.get("input_size", 0)),
                embedding_dim=int(info.get("embedding_dim", 0)),
                total_faces_enrolled=int(info.get("total_faces_enrolled", 0)),
                is_ready=bool(info.get("is_ready", True)),
                detector_type=str(info.get("detector_type", "")),
            )

            # If you want to map detector_config into the proto DetectorConfig,
            # you can do it here (if the field exists and you imported it).

            logger.info(
                "get_model_info_rpc_completed",
                model_name=resp.model_name,
                device=resp.device,
            )
            return resp

        except Exception as e:
            logger.error(
                "get_model_info_rpc_error",
                error=str(e),
                exc_info=True,
            )
            return FaceModelInfoResponse(
                model_name="error",
                model_version="0.0.0",
                device="unknown",
                is_ready=False,
            )

    # -------------------------------------------------------------------------
    # VerifyFace / EnrollFace are BL-only (not implemented in AI)
    # -------------------------------------------------------------------------

    def VerifyFace(self, request, context):
        logger.warning("verify_face_not_implemented_in_ai_service")
        return FaceVerifyResponse(
            success=False,
            error_message=(
                "VerifyFace is implemented in .NET business logic, not AI service."
            ),
            face_detected=False,
            total_time_ms=0.0,
        )

    def EnrollFace(self, request, context):
        logger.warning("enroll_face_not_implemented_in_ai_service")
        return FaceEnrollResponse(
            success=False,
            error_message=(
                "EnrollFace is implemented in .NET business logic, not AI service."
            ),
            person_id=request.person_id,
            images_processed=0,
            valid_embeddings=0,
            avg_quality_score=0.0,
            total_time_ms=0.0,
        )

    # -------------------------------------------------------------------------
    # ProcessFrame – NOW IMPLEMENTED
    # -------------------------------------------------------------------------
    def ProcessFrame(self, request, context):
        """
        Process a single video frame directly via FaceClient.
        Enables bypassing VideoStreamService for face-only cameras.
        """
        try:
            # Validate frame data
            if not request.frame or len(request.frame) == 0:
                logger.warning("process_frame_empty_image")
                return FrameProcessResponse(
                    success=False,
                    error_message="Empty frame data",
                )

            # Decode JPEG
            np_arr = np.frombuffer(request.frame, np.uint8)
            frame = cv2.imdecode(np_arr, cv2.IMREAD_COLOR)

            if frame is None:
                logger.error("process_frame_decode_failed")
                return FrameProcessResponse(
                    success=False,
                    error_message="Failed to decode frame",
                )

            # Call service layer
            result = self.face_service.process_frame(
                frame=frame,
                camera_id=request.camera_id or "unknown",
                confidence_threshold=request.confidence_threshold or 0.7,
                max_faces=request.max_faces or 10,
                skip_embedding=request.skip_embedding,
            )

            faces_result = result.get("faces", [])
            resp = FrameProcessResponse(
                success=bool(result.get("success", False)),
                frame_id=int(result.get("frame_id", 0)),
                camera_id=result.get("camera_id", "") or request.camera_id,
                total_time_ms=float(result.get("time_ms", 0.0)),
            )

            # Map metrics
            metrics = self._map_metrics_to_proto(result.get("metrics"))
            if metrics:
                resp.metrics.CopyFrom(metrics)

            # OPTIMIZED: Map faces with helper methods
            for f in faces_result:
                face_msg = Face()

                # Bbox
                bbox_proto = self._map_bbox_to_proto(f.get("bbox"))
                if bbox_proto:
                    face_msg.bbox.CopyFrom(bbox_proto)

                # Embedding
                embedding = f.get("embedding") or []
                if embedding:
                    face_msg.embedding_vector.extend(float(v) for v in embedding)

                face_msg.confidence = float(f.get("confidence", 1.0))

                # Face ID
                face_id = f.get("face_id")
                if face_id is not None:
                    face_msg.face_id = int(face_id)

                # Quality
                self._map_quality_to_proto(f.get("quality"), face_msg)

                resp.faces.append(face_msg)

            logger.info(
                "process_frame_rpc_completed",
                camera_id=resp.camera_id,
                frame_id=resp.frame_id,
                faces=len(resp.faces),
                time_ms=resp.total_time_ms,
            )
            return resp

        except InvalidImageException as e:
            logger.error("process_frame_rpc_invalid_image", error=str(e), exc_info=True)
            return FrameProcessResponse(success=False, error_message=str(e))

        except Exception as e:
            logger.error("process_frame_rpc_error", error=str(e), exc_info=True)
            return FrameProcessResponse(success=False, error_message=str(e))
# ==================================== Helpers ++ ========================================
    @staticmethod
    def _map_bbox_to_proto(bbox_data: Any) -> Optional[BoundingBox]:
        """
        Extract bbox from service layer response and map to protobuf.
        Handles both tuple (x,y,w,h) and dict {x,y,w,h} formats.
        
        Returns None if bbox is invalid.
        """
        if bbox_data is None:
            return None
        
        try:
            # Tuple/list format: (x, y, w, h)
            if isinstance(bbox_data, (list, tuple)) and len(bbox_data) == 4:
                x, y, w, h = bbox_data
                return BoundingBox(
                    x=float(x),
                    y=float(y),
                    w=float(w),
                    h=float(h),
                )
            
            # Dict format: {x, y, w, h}
            if isinstance(bbox_data, dict):
                x = bbox_data.get("x", 0.0)
                y = bbox_data.get("y", 0.0)
                w = bbox_data.get("w", 0.0)
                h = bbox_data.get("h", 0.0)
                return BoundingBox(
                    x=float(x),
                    y=float(y),
                    w=float(w),
                    h=float(h),
                )
        except (TypeError, ValueError) as e:
            logger.warning("bbox_mapping_failed", error=str(e))
            return None
        
        return None
    
    @staticmethod
    def _map_quality_to_proto(quality_data: Optional[Dict[str, Any]], target_msg) -> None:
        """
        Map quality dict from service layer to protobuf quality message.
        Modifies target_msg.quality in place.
        """
        if quality_data is None:
            return
        
        q = target_msg.quality
        q.overall_score = float(quality_data.get("overall_score", 0.0))
        q.sharpness = float(quality_data.get("sharpness", 0.0))
        q.brightness = float(quality_data.get("brightness", 0.0))
        q.face_size_pixels = int(quality_data.get("face_size_pixels", 0))
    
    @staticmethod
    def _map_metrics_to_proto(metrics_data: Optional[Dict[str, Any]]) -> Optional[PerformanceMetrics]:
        """
        Map metrics dict from service layer to protobuf PerformanceMetrics.
        Returns None if no metrics data.
        """
        if not metrics_data:
            return None
        
        return PerformanceMetrics(
            detection_ms=float(metrics_data.get("detection_ms", 0.0)),
            embedding_ms=float(metrics_data.get("embedding_ms", 0.0)),
            preprocessing_ms=float(metrics_data.get("preprocessing_ms", 0.0)),
            total_ms=float(metrics_data.get("total_ms", 0.0)),
            image_width=int(metrics_data.get("image_width", 0)),
            image_height=int(metrics_data.get("image_height", 0)),
            faces_detected=int(metrics_data.get("faces_detected", 0)),
        )