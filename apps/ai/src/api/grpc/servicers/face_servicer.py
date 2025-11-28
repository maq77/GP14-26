import structlog

from packages.contracts.python.face_pb2 import (
    FaceDetectResponse,
    FaceRecognizeResponse,
    FaceVerifyResponse,
    FaceEnrollResponse,
    FaceModelInfoResponse,
    FaceEmbeddingResponse,
    Face,
    FaceMatch,
    BoundingBox,
)
from packages.contracts.python.face_pb2_grpc import FaceServiceServicer

from ....services.ml.Face_Recognition_Service import FaceRecognitionService

logger = structlog.get_logger("grpc.face_servicer")


class FaceServicer(FaceServiceServicer):
    def __init__(self, face_service: FaceRecognitionService):
        self.face_service = face_service
        logger.info("face_servicer_initialized")

    def DetectFaces(self, request, context):
        try:
            result = self.face_service.detect_faces(
                image_bytes=request.image,
                confidence_threshold=request.confidence_threshold,
            )
            resp = FaceDetectResponse(
                success=True,
                total_faces=len(result["faces"]),
                total_time_ms=result["time_ms"],
            )
            for f in result["faces"]:
                x, y, w, h = f["bbox"]
                resp.faces.append(
                    Face(
                        bbox=BoundingBox(
                            x1=float(x),
                            y1=float(y),
                            x2=float(x + w),
                            y2=float(y + h),
                        ),
                        confidence=float(f.get("confidence", 1.0)),
                    )
                )
            logger.info("detect_faces_rpc_completed", total_faces=len(resp.faces))
            return resp
        except Exception as e:
            logger.error("detect_faces_rpc_error", error=str(e), exc_info=True)
            return FaceDetectResponse(success=False, error_message=str(e))

    def ExtractEmbedding(self, request, context):
        try:
            result = self.face_service.extract_embedding(
                image_bytes=request.image,
                # confidence_threshold=request.confidence_threshold,
            )
            resp = FaceEmbeddingResponse(
                success=result["success"],
                face_detected=result["face_detected"],
                total_time_ms=result["time_ms"],
            )
            if not result["success"]:
                resp.error_message = "embedding_failed"
                logger.error("extract_embedding_rpc_failed_flag")
                return resp
            if result["face_detected"] and result["bbox"] is not None:
                x, y, w, h = result["bbox"]
                resp.bbox.CopyFrom(
                    BoundingBox(
                        x1=float(x),
                        y1=float(y),
                        x2=float(x + w),
                        y2=float(y + h),
                    )
                )
            resp.embedding.extend(result["embedding"])
            logger.info(
                "extract_embedding_rpc_completed",
                face_detected=result["face_detected"],
            )
            return resp
        except Exception as e:
            logger.error("extract_embedding_rpc_error", error=str(e), exc_info=True)
            return FaceEmbeddingResponse(success=False, error_message=str(e))

    def GetModelInfo(self, request, context):
        try:
            info = self.face_service.get_model_info()
            resp = FaceModelInfoResponse(
                model_name=info["model_name"],
                model_version=info["model_version"],
                device=info["device"],
                model_size_mb=info["model_size_mb"],
                input_size=info["input_size"],
                embedding_dim=info["embedding_dim"],
                total_faces_enrolled=0,
            )
            logger.info("get_model_info_rpc_completed")
            return resp
        except Exception as e:
            logger.error("get_model_info_rpc_error", error=str(e), exc_info=True)
            return FaceModelInfoResponse(
                model_name="error",
                model_version="0.0.0",
            )

    def RecognizeFace(self, request, context):
        logger.warning("recognize_face_not_implemented_in_ai_service")
        return FaceRecognizeResponse(
            success=False,
            error_message="RecognizeFace is implemented in .NET business logic, not AI service.",
            total_candidates_checked=0,
            total_time_ms=0.0,
        )

    def VerifyFace(self, request, context):
        logger.warning("verify_face_not_implemented_in_ai_service")
        return FaceVerifyResponse(
            success=False,
            error_message="VerifyFace is implemented in .NET business logic, not AI service.",
            face_detected=False,
            match_found=False,
            is_authorized=False,
            person_id="",
            person_name="",
            confidence=0.0,
            total_time_ms=0.0,
        )

    def EnrollFace(self, request, context):
        logger.warning("enroll_face_not_implemented_in_ai_service")
        return FaceEnrollResponse(
            success=False,
            error_message="EnrollFace is implemented in .NET business logic, not AI service.",
            person_id=request.person_id,
            images_enrolled=0,
        )
