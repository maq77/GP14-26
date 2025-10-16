"""Face recognition service gRPC implementation."""
import structlog

from packages.contracts.python import face_pb2, face_pb2_grpc
#from ....services.ml.face_recognition_service import FaceRecognitionService

logger = structlog.get_logger("grpc.face_servicer")


class FaceServicer(face_pb2_grpc.FaceServiceServicer):
    """
    gRPC service for face recognition operations.
    
    Thin adapter layer - business logic in FaceRecognitionService.
    """
    
    def __init__(self):
        #self.face_service = FaceRecognitionService()
        logger.info("face_servicer_initialized")
    
    def Verify(self, request: face_pb2.VerifyRequest, context):
        """
        Verify if a face matches a known identity.
        
        Args:
            request: VerifyRequest proto
            context: gRPC context
            
        Returns:
            VerifyResponse proto
        """
        try:
            logger.debug("face_verification_request", person_id=request.person_id)
            
            # Call business logic
            result = self.face_service.verify(
                image=request.image,
                person_id=request.person_id
            )
            
            response = face_pb2.VerifyResponse(
                success=True,
                match=result.match,
                confidence=result.confidence,
                person_id=request.person_id
            )
            
            logger.info(
                "face_verification_completed",
                match=result.match,
                confidence=result.confidence
            )
            
            return response
            
        except Exception as e:
            logger.error("face_verification_error", error=str(e), exc_info=True)
            context.set_code(grpc.StatusCode.INTERNAL)
            context.set_details(str(e))
            return face_pb2.VerifyResponse(
                success=False,
                error_message=str(e)
            )
    
    def Identify(self, request: face_pb2.IdentifyRequest, context):
        """
        Identify a face from the database.
        
        Args:
            request: IdentifyRequest proto
            context: gRPC context
            
        Returns:
            IdentifyResponse proto
        """
        try:
            logger.debug("face_identification_request")
            
            result = self.face_service.identify(request.image)
            
            response = face_pb2.IdentifyResponse(
                success=True,
                person_id=result.person_id if result.match else None,
                confidence=result.confidence,
                match_found=result.match
            )
            
            return response
            
        except Exception as e:
            logger.error("face_identification_error", error=str(e), exc_info=True)
            context.set_code(grpc.StatusCode.INTERNAL)
            context.set_details(str(e))
            return face_pb2.IdentifyResponse(
                success=False,
                error_message=str(e)
            )
