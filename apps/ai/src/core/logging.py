"""
apps/ai/src/core/logging.py
Structured logging setup using structlog
"""

import logging
import sys
from pathlib import Path
from typing import Optional
import structlog
from structlog.stdlib import BoundLogger
from .config import settings


def setup_logging() -> BoundLogger:
    """
    Configure structured logging for the application
    Returns configured logger instance
    """
    
    # Configure stdlib logging
    logging.basicConfig(
        format="%(message)s",
        stream=sys.stdout,
        level=getattr(logging, settings.LOG_LEVEL),
    )
    
    # Shared processors for all logs
    shared_processors = [
        structlog.contextvars.merge_contextvars,
        structlog.stdlib.add_log_level,
        structlog.stdlib.add_logger_name,
        structlog.processors.TimeStamper(fmt="iso"),
        structlog.processors.StackInfoRenderer(),
        structlog.processors.format_exc_info,
        structlog.processors.UnicodeDecoder(),
    ]
    
    # Output format based on environment
    if settings.LOG_FORMAT == "json":
        # JSON format for production (easy to parse)
        processors = shared_processors + [
            structlog.processors.dict_tracebacks,
            structlog.processors.JSONRenderer()
        ]
    else:
        # Console format for development (human-readable)
        processors = shared_processors + [
            structlog.dev.ConsoleRenderer(colors=True)
        ]
    
    # Configure structlog
    structlog.configure(
        processors=processors,
        wrapper_class=structlog.stdlib.BoundLogger,
        context_class=dict,
        logger_factory=structlog.stdlib.LoggerFactory(),
        cache_logger_on_first_use=True,
    )
    
    # Create logger
    logger = structlog.get_logger("sssp.ai")
    
    # Log startup info
    logger.info(
        "logging_configured",
        level=settings.LOG_LEVEL,
        format=settings.LOG_FORMAT,
        environment=settings.ENVIRONMENT
    )
    
    return logger


def get_logger(name: Optional[str] = None) -> BoundLogger:
    """
    Get a logger instance
    
    Args:
        name: Logger name (e.g., "detection", "face")
    
    Returns:
        Configured structlog logger
    """
    if name:
        return structlog.get_logger(f"sssp.ai.{name}")
    return structlog.get_logger("sssp.ai")


# ============================================================================
# Context Manager for Request Logging
# ============================================================================

class LogContext:
    """
    Context manager for adding request-specific context to logs
    
    Usage:
        with LogContext(request_id="123", camera_id="cam_1"):
            logger.info("processing_frame")
    """
    
    def __init__(self, **kwargs):
        self.context = kwargs
    
    def __enter__(self):
        structlog.contextvars.clear_contextvars()
        structlog.contextvars.bind_contextvars(**self.context)
        return self
    
    def __exit__(self, exc_type, exc_val, exc_tb):
        structlog.contextvars.clear_contextvars()
        return False


# ============================================================================
# Export
# ============================================================================

__all__ = ["setup_logging", "get_logger", "LogContext"]