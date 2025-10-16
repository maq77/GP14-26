"""Detection service gRPC implementation."""
import grpc
import structlog
from typing import Iterator
from pathlib import Path
import sys

# Import proto definitions
contracts_path = Path(__file__).resolve().parents[6] / "packages" / "contracts" / "python"
if str(contracts_path) not in sys.path:
    sys.path.insert(0, str(contracts_path))

import detection_pb2
import detection_pb2_grpc

# Import business logic
from ....services.ml.object_detection import ObjectDetectionService
from ....core.exceptions import ModelNotLoadedException, InferenceException

logger = structlog.get_logger("grpc.detection_servicer")


class DetectionServicer(detection_pb2_grpc.DetectionServiceServicer):
    """
    gRPC service implementation for object detection.
    
    This is a thin layer that:
    1. Receives gRPC requests
    2. Converts proto to internal DTOs
    3. Calls business logic services
    4. Converts results back to proto
    5. Handles errors appropriately
    
    Business logic stays in services/ directory.
    """
    
    def __init__(self):
        self.detection_service = ObjectDetectionService()
        logger.info("detection_servicer_initialized")
    
    def Detect(self, request: detection_pb2.DetectRequest, context):
        """
        Single image detection.
        
        Args:
            request: DetectRequest proto
            context: gRPC context
            
        Returns:
            DetectResponse proto
        """
        try:
            logger.debug(
                "detection_request_received",
                image_size=len(request.image),
                client=context.peer()
            )
            
            # Call business logic
            result = self.detection_service.detect(request.image)
            
            # Convert to proto response
            response = detection_pb2.DetectResponse(
                success=True,
                detections=[
                    detection_pb2.Detection(
                        class_id=det.class_id,
                        class_name=det.class_name,
                        confidence=det.confidence,
                        bbox=detection_pb2.BoundingBox(
                            x1=det.bbox.x1,
                            y1=det.bbox.y1,
                            x2=det.bbox.x2,
                            y2=det.bbox.y2
                        )
                    )
                    for det in result.detections
                ],
                inference_time_ms=result.inference_time_ms,
                model_version=result.model_version
            )
            
            logger.info(
                "detection_completed",
                detections=len(response.detections),
                inference_ms=response.inference_time_ms
            )
            
            return response
            
        except ModelNotLoadedException as e:
            logger.error("model_not_loaded", error=str(e))
            context.set_code(grpc.StatusCode.UNAVAILABLE)
            context.set_details("Detection model not loaded")
            return detection_pb2.DetectResponse(
                success=False,
                error_message="Model not available"
            )
            
        except InferenceException as e:
            logger.error("inference_error", error=str(e), exc_info=True)
            context.set_code(grpc.StatusCode.INTERNAL)
            context.set_details("Inference failed")
            return detection_pb2.DetectResponse(
                success=False,
                error_message=str(e)
            )
            
        except Exception as e:
            logger.error("unexpected_error", error=str(e), exc_info=True)
            context.set_code(grpc.StatusCode.INTERNAL)
            context.set_details("Internal server error")
            return detection_pb2.DetectResponse(
                success=False,
                error_message="Internal error"
            )
    
    def DetectStream(
        self,
        request_iterator: Iterator[detection_pb2.DetectRequest],
        context
    ):
        """
        Streaming detection for video processing.
        
        Args:
            request_iterator: Stream of DetectRequest protos
            context: gRPC context
            
        Yields:
            DetectResponse protos
        """
        try:
            logger.info("stream_detection_started", client=context.peer())
            
            frame_count = 0
            for request in request_iterator:
                frame_count += 1
                
                # Process each frame
                result = self.detection_service.detect(request.image)
                
                # Convert and yield response
                response = detection_pb2.DetectResponse(
                    success=True,
                    detections=[
                        detection_pb2.Detection(
                            class_id=det.class_id,
                            class_name=det.class_name,
                            confidence=det.confidence,
                            bbox=detection_pb2.BoundingBox(
                                x1=det.bbox.x1,
                                y1=det.bbox.y1,
                                x2=det.bbox.x2,
                                y2=det.bbox.y2
                            )
                        )
                        for det in result.detections
                    ],
                    inference_time_ms=result.inference_time_ms,
                    frame_number=frame_count
                )
                
                yield response
            
            logger.info(
                "stream_detection_completed",
                total_frames=frame_count
            )
            
        except Exception as e:
            logger.error("stream_detection_error", error=str(e), exc_info=True)
            context.set_code(grpc.StatusCode.INTERNAL)
            context.set_details(str(e))
