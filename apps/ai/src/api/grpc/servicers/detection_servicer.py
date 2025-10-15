"""
apps/ai/src/api/grpc/servicers/detection_servicer.py
gRPC Detection Service Implementation
"""

import sys
from pathlib import Path

# Add packages/contracts/python to path
contracts_path = Path(__file__).resolve().parents[7] / "packages" / "contracts" / "python"
sys.path.insert(0, str(contracts_path))

# Import generated gRPC code from packages/contracts
from detection_pb2 import (
    DetectRequest as ProtoDetectRequest,
    DetectResponse as ProtoDetectResponse,
    DetectBatchRequest as ProtoDetectBatchRequest,
    DetectBatchResponse as ProtoDetectBatchResponse,
    ModelInfoRequest as ProtoModelInfoRequest,
    ModelInfoResponse as ProtoModelInfoResponse,
    Detection as ProtoDetection,
    BoundingBox as ProtoBoundingBox,
    ImageMetadata as ProtoImageMetadata
)
from detection_pb2_grpc import DetectionServiceServicer

from ....core.logging import get_logger
from ....services.ml.object_detection import ObjectDetectionService
from ....schemas.detection import DetectRequest, DetectBatchRequest
from ....models.object.model_loader import get_model_loader
from ....core.config import settings

logger = get_logger("detection_servicer")


class DetectionServicer(DetectionServiceServicer):
    """
    gRPC Detection Service Implementation
    Implements DetectionService defined in detection.proto
    """
    
    def __init__(self):
        """Initialize detection servicer"""
        self.detection_service = ObjectDetectionService()
        logger.info("detection_servicer_initialized")
    
    def DetectObjects(
        self,
        request: ProtoDetectRequest,
        context
    ) -> ProtoDetectResponse:
        """
        Detect objects in image (gRPC endpoint)
        
        Args:
            request: Proto DetectRequest
            context: gRPC context
        
        Returns:
            Proto DetectResponse
        """
        try:
            logger.debug(
                "grpc_detect_objects_called",
                camera_id=request.camera_id,
                request_id=request.request_id
            )
            
            # Convert proto request to internal request
            internal_request = self._proto_to_internal_request(request)
            
            # Call service
            internal_response = self.detection_service.detect_objects(internal_request)
            
            # Convert internal response to proto response
            proto_response = self._internal_to_proto_response(internal_response)
            
            return proto_response
            
        except Exception as e:
            logger.error("grpc_detect_objects_failed", error=str(e), exc_info=True)
            return ProtoDetectResponse(
                success=False,
                error_message=f"Internal error: {str(e)}"
            )
    
    def DetectWaste(
        self,
        request: ProtoDetectRequest,
        context
    ) -> ProtoDetectResponse:
        """
        Detect waste/trash in image (gRPC endpoint)
        """
        try:
            logger.debug("grpc_detect_waste_called", camera_id=request.camera_id)
            
            internal_request = self._proto_to_internal_request(request)
            internal_response = self.detection_service.detect_waste(internal_request)
            proto_response = self._internal_to_proto_response(internal_response)
            
            return proto_response
            
        except Exception as e:
            logger.error("grpc_detect_waste_failed", error=str(e), exc_info=True)
            return ProtoDetectResponse(
                success=False,
                error_message=f"Internal error: {str(e)}"
            )
    
    def DetectVandalism(
        self,
        request: ProtoDetectRequest,
        context
    ) -> ProtoDetectResponse:
        """
        Detect vandalism in image (gRPC endpoint)
        """
        try:
            logger.debug("grpc_detect_vandalism_called", camera_id=request.camera_id)
            
            internal_request = self._proto_to_internal_request(request)
            internal_response = self.detection_service.detect_vandalism(internal_request)
            proto_response = self._internal_to_proto_response(internal_response)
            
            return proto_response
            
        except Exception as e:
            logger.error("grpc_detect_vandalism_failed", error=str(e), exc_info=True)
            return ProtoDetectResponse(
                success=False,
                error_message=f"Internal error: {str(e)}"
            )
    
    def DetectObjectsBatch(
        self,
        request: ProtoDetectBatchRequest,
        context
    ) -> ProtoDetectBatchResponse:
        """
        Detect objects in batch of images (gRPC endpoint)
        """
        try:
            logger.debug("grpc_detect_batch_called", batch_size=len(request.requests))
            
            # Convert proto requests
            internal_requests = [
                self._proto_to_internal_request(req) 
                for req in request.requests
            ]
            
            internal_batch_request = DetectBatchRequest(
                requests=internal_requests,
                parallel_processing=request.parallel_processing
            )
            
            # Call service
            internal_response = self.detection_service.detect_batch(internal_batch_request)
            
            # Convert to proto response
            proto_response = ProtoDetectBatchResponse(
                total_time_ms=internal_response.total_time_ms
            )
            
            for resp in internal_response.responses:
                proto_resp = self._internal_to_proto_response(resp)
                proto_response.responses.append(proto_resp)
            
            return proto_response
            
        except Exception as e:
            logger.error("grpc_detect_batch_failed", error=str(e), exc_info=True)
            return ProtoDetectBatchResponse(
                total_time_ms=0.0
            )
    
    def DetectObjectsStream(self, request_iterator, context):
        """
        Stream detection (not implemented yet - Phase 2)
        """
        context.set_code(grpc.StatusCode.UNIMPLEMENTED)
        context.set_details("Streaming not implemented yet")
        return ProtoDetectResponse()
    
    def GetModelInfo(
        self,
        request: ProtoModelInfoRequest,
        context
    ) -> ProtoModelInfoResponse:
        """
        Get model information (gRPC endpoint)
        """
        try:
            logger.debug("grpc_get_model_info_called")
            
            loader = get_model_loader()
            info = loader.get_model_info()
            
            response = ProtoModelInfoResponse(
                model_name=info.get("model_name", "unknown"),
                model_version=settings.APP_VERSION,
                num_classes=info.get("num_classes", 0),
                device=info.get("device", "unknown"),
                model_size_mb=0.0,  # TODO: Calculate actual size
                input_size=settings.DETECTION_IMAGE_SIZE
            )
            
            # Add class names
            if "class_names" in info:
                response.classes.extend(info["class_names"])
            
            return response
            
        except Exception as e:
            logger.error("grpc_get_model_info_failed", error=str(e), exc_info=True)
            return ProtoModelInfoResponse(
                model_name="error",
                model_version="0.0.0",
                num_classes=0,
                device="unknown"
            )
    
    # ========================================================================
    # Helper Methods - Proto <-> Internal Conversion
    # ========================================================================
    
    def _proto_to_internal_request(
        self,
        proto_request: ProtoDetectRequest
    ) -> DetectRequest:
        """
        Convert protobuf DetectRequest to internal DetectRequest
        """
        return DetectRequest(
            image=proto_request.image,
            confidence_threshold=proto_request.confidence_threshold or settings.DETECTION_CONFIDENCE,
            iou_threshold=proto_request.iou_threshold or settings.DETECTION_IOU_THRESHOLD,
            target_classes=list(proto_request.target_classes) if proto_request.target_classes else [],
            exclude_classes=list(proto_request.exclude_classes) if proto_request.exclude_classes else [],
            camera_id=proto_request.camera_id or None,
            timestamp=proto_request.timestamp or None,
            request_id=proto_request.request_id or None,
            enable_tracking=proto_request.enable_tracking,
            return_cropped_images=proto_request.return_cropped_images,
            max_detections=proto_request.max_detections or settings.DETECTION_MAX_DETECTIONS
        )
    
    def _internal_to_proto_response(
        self,
        internal_response
    ) -> ProtoDetectResponse:
        """
        Convert internal DetectResponse to protobuf DetectResponse
        """
        proto_response = ProtoDetectResponse(
            success=internal_response.success,
            error_message=internal_response.error_message or "",
            total_objects=internal_response.total_objects,
            inference_time_ms=internal_response.inference_time_ms,
            preprocessing_time_ms=internal_response.preprocessing_time_ms,
            postprocessing_time_ms=internal_response.postprocessing_time_ms,
            total_time_ms=internal_response.total_time_ms,
            request_id=internal_response.request_id or "",
            timestamp=internal_response.timestamp or 0
        )
        
        # Add detections
        for detection in internal_response.detections:
            proto_detection = ProtoDetection(
                class_name=detection.class_name,
                class_id=detection.class_id,
                confidence=detection.confidence,
                track_id=detection.track_id or -1,
                area=detection.area or 0.0,
                zone=detection.zone or ""
            )
            
            # Add bounding box
            proto_detection.bbox.CopyFrom(
                ProtoBoundingBox(
                    x1=detection.bbox.x1,
                    y1=detection.bbox.y1,
                    x2=detection.bbox.x2,
                    y2=detection.bbox.y2,
                    x1_norm=detection.bbox.x1_norm or 0.0,
                    y1_norm=detection.bbox.y1_norm or 0.0,
                    x2_norm=detection.bbox.x2_norm or 0.0,
                    y2_norm=detection.bbox.y2_norm or 0.0
                )
            )
            
            # Add cropped image if available
            if detection.cropped_image:
                proto_detection.cropped_image = detection.cropped_image
            
            proto_response.detections.append(proto_detection)
        
        # Add image metadata
        if internal_response.image_metadata:
            proto_response.image_metadata.CopyFrom(
                ProtoImageMetadata(
                    width=internal_response.image_metadata.width,
                    height=internal_response.image_metadata.height,
                    channels=internal_response.image_metadata.channels,
                    format=internal_response.image_metadata.format
                )
            )
        
        return proto_response


# ============================================================================
# Export
# ============================================================================

__all__ = ["DetectionServicer"]