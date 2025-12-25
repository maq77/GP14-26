"""
apps/ai/src/core/exceptions.py
Custom exceptions for AI service
"""

from typing import Optional, Any


class AIServiceException(Exception):
    """Base exception for all AI service errors"""
    
    def __init__(
        self,
        message: str,
        error_code: str = "UNKNOWN_ERROR",
        details: Optional[dict] = None
    ):
        self.message = message
        self.error_code = error_code
        self.details = details or {}
        super().__init__(self.message)
    
    def to_dict(self) -> dict:
        """Convert exception to dictionary for logging/response"""
        return {
            "error_code": self.error_code,
            "message": self.message,
            "details": self.details
        }


# ============================================================================
# Model-related Exceptions
# ============================================================================

class ModelNotLoadedException(AIServiceException):
    """Model is not loaded or failed to load"""
    
    def __init__(self, model_name: str, details: Optional[dict] = None):
        super().__init__(
            message=f"Model '{model_name}' is not loaded",
            error_code="MODEL_NOT_LOADED",
            details=details
        )


class ModelLoadException(AIServiceException):
    """Failed to load model"""
    
    def __init__(self, model_name: str, reason: str):
        super().__init__(
            message=f"Failed to load model '{model_name}': {reason}",
            error_code="MODEL_LOAD_FAILED",
            details={"model_name": model_name, "reason": reason}
        )


class InferenceException(AIServiceException):
    """Inference failed"""
    
    def __init__(self, reason: str, details: Optional[dict] = None):
        super().__init__(
            message=f"Inference failed: {reason}",
            error_code="INFERENCE_FAILED",
            details=details
        )


# ============================================================================
# Input/Output Exceptions
# ============================================================================

class InvalidImageException(AIServiceException):
    """Invalid or corrupted image data"""
    
    def __init__(self, reason: str = "Could not decode image"):
        super().__init__(
            message=f"Invalid image: {reason}",
            error_code="INVALID_IMAGE",
            details={"reason": reason}
        )


class InvalidParametersException(AIServiceException):
    """Invalid request parameters"""
    
    def __init__(self, parameter: str, reason: str):
        super().__init__(
            message=f"Invalid parameter '{parameter}': {reason}",
            error_code="INVALID_PARAMETERS",
            details={"parameter": parameter, "reason": reason}
        )


# ============================================================================
# Processing Exceptions
# ============================================================================

class PreprocessingException(AIServiceException):
    """Image preprocessing failed"""
    
    def __init__(self, reason: str):
        super().__init__(
            message=f"Preprocessing failed: {reason}",
            error_code="PREPROCESSING_FAILED",
            details={"reason": reason}
        )


class PostprocessingException(AIServiceException):
    """Postprocessing failed"""
    
    def __init__(self, reason: str):
        super().__init__(
            message=f"Postprocessing failed: {reason}",
            error_code="POSTPROCESSING_FAILED",
            details={"reason": reason}
        )


# ============================================================================
# Resource Exceptions
# ============================================================================

class ResourceException(AIServiceException):
    """Resource-related errors (memory, disk, etc.)"""
    
    def __init__(self, resource: str, reason: str):
        super().__init__(
            message=f"Resource error ({resource}): {reason}",
            error_code="RESOURCE_ERROR",
            details={"resource": resource, "reason": reason}
        )


class TimeoutException(AIServiceException):
    """Request timeout"""
    
    def __init__(self, timeout_seconds: int):
        super().__init__(
            message=f"Request timeout after {timeout_seconds} seconds",
            error_code="TIMEOUT",
            details={"timeout_seconds": timeout_seconds}
        )


# ============================================================================
# Export
# ============================================================================

__all__ = [
    "AIServiceException",
    "ModelNotLoadedException",
    "ModelLoadException",
    "InferenceException",
    "InvalidImageException",
    "InvalidParametersException",
    "PreprocessingException",
    "PostprocessingException",
    "ResourceException",
    "TimeoutException",
]