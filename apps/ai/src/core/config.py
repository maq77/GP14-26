"""
apps/ai/src/core/config.py
Configuration management using Pydantic Settings
"""

from pydantic_settings import BaseSettings, SettingsConfigDict
from pydantic import Field, field_validator
from typing import List, Optional, Literal
from pathlib import Path
import torch


class Settings(BaseSettings):
    """
    AI Service Configuration
    Environment variables can override these defaults
    """
    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        case_sensitive=False,
        extra="ignore"
    )
    
    # ========================================================================
    # Application Settings
    # ========================================================================
    APP_NAME: str = "SSSP-AI-Service"
    APP_VERSION: str = "1.0.0"
    ENVIRONMENT: Literal["development", "staging", "production"] = "development"
    DEBUG: bool = False
    
    # ========================================================================
    # API Settings (FastAPI - for testing only)
    # ========================================================================
    API_HOST: str = "0.0.0.0"
    API_PORT: int = 8001
    API_WORKERS: int = 1
    
    # ========================================================================
    # gRPC Settings (Primary communication with .NET)
    # ========================================================================
    GRPC_HOST: str = "0.0.0.0"
    GRPC_PORT: int = 50051
    GRPC_MAX_WORKERS: int = 10
    GRPC_MAX_MESSAGE_LENGTH: int = 100 * 1024 * 1024  # 100MB
    
    # ========================================================================
    # Object Detection Model Settings
    # ========================================================================
    
    # Model selection
    DETECTION_MODEL_TYPE: Literal["yolo11n", "yolo11s", "yolo11m", "yolo11l", "yolov8s", "yolo11x", "yolo8n", "yolo8s"] = "yolov8s"
    DETECTION_MODEL_PATH: Path = Path("apps/ai/data/models/production/yolov8s.pt")  # Optional: custom model path
    #DETECTION_MODEL_PATH: Optional[Path] = None  # If None, download pretrained
    
    # Inference parameters
    DETECTION_CONFIDENCE: float = Field(default=0.25, ge=0.0, le=1.0)
    DETECTION_IOU_THRESHOLD: float = Field(default=0.45, ge=0.0, le=1.0)
    DETECTION_MAX_DETECTIONS: int = Field(default=300, ge=1, le=1000)
    DETECTION_IMAGE_SIZE: int = Field(default=640, ge=320, le=1280)
    
    # Device settings
    DETECTION_DEVICE: Literal["auto", "cuda", "cpu", "mps"] = "auto"
    DETECTION_HALF_PRECISION: bool = True  # FP16 for faster inference on GPU
    
    # Class filtering
    DETECTION_CLASSES: List[str] = [
        "person", "car", "truck", "bus", "motorcycle", "bicycle",
        "trash_bag", "trash_bin", "bottle", "backpack", "handbag",
        "suitcase", "fire", "smoke"
    ]
    
    # ========================================================================
    # Object Tracking Settings (DeepSORT)
    # ========================================================================
    TRACKING_ENABLED: bool = False  # Enable in Phase 2
    TRACKING_MAX_AGE: int = 30  # Frames to keep lost tracks
    TRACKING_MIN_HITS: int = 3   # Min detections before tracking
    TRACKING_IOU_THRESHOLD: float = 0.3
    
    # ========================================================================
    # Waste Detection Settings (Specialized)
    # ========================================================================
    WASTE_CLASSES: List[str] = [
        "trash_bag", "trash_bin", "bottle", "can", "paper", "cardboard",
        "plastic", "metal", "glass"
    ]
    WASTE_CONFIDENCE: float = 0.3  # Lower threshold for waste
    
    # ========================================================================
    # Performance Settings
    # ========================================================================
    BATCH_SIZE: int = 8
    MAX_CONCURRENT_REQUESTS: int = 10
    REQUEST_TIMEOUT: int = 30  # seconds
    WARMUP_ITERATIONS: int = 5  # Model warmup on startup
    
    # ========================================================================
    # Logging Settings
    # ========================================================================
    LOG_LEVEL: Literal["DEBUG", "INFO", "WARNING", "ERROR", "CRITICAL"] = "INFO"
    LOG_FORMAT: str = "json"  # "json" or "console"
    LOG_FILE: Optional[Path] = Path("data/logs/ai-service.log")
    
    # ========================================================================
    # Monitoring Settings
    # ========================================================================
    METRICS_ENABLED: bool = True
    METRICS_PORT: int = 9090
    TRACING_ENABLED: bool = False
    
    # ========================================================================
    # Data Paths
    # ========================================================================
    BASE_DIR: Path = Path(__file__).resolve().parents[2]  # apps/ai/
    DATA_DIR: Path = Path("data")
    MODELS_DIR: Path = DATA_DIR / "models" / "production"
    CACHE_DIR: Path = DATA_DIR / "cache"
    LOGS_DIR: Path = DATA_DIR / "logs"
    redis_url: str = "redis://localhost:6379" # Redis connection URL #still not applied yet
    
    # ========================================================================
    # Validators
    # ========================================================================
    
    @field_validator("DETECTION_DEVICE")
    def validate_device(cls, v):
        """Auto-detect best available device"""
        if v == "auto":
            if torch.cuda.is_available():
                return "cuda"
            elif torch.backends.mps.is_available():  # Apple Silicon
                return "mps"
            else:
                return "cpu"
        return v
    
    @field_validator("MODELS_DIR", "CACHE_DIR", "LOGS_DIR")
    def create_directories(cls, v):
        """Ensure directories exist"""
        v.mkdir(parents=True, exist_ok=True)
        return v
    
    @field_validator("LOG_FILE")
    def create_log_file_dir(cls, v):
        """Ensure log directory exists"""
        if v:
            v.parent.mkdir(parents=True, exist_ok=True)
        return v
    
    # ========================================================================
    # Properties
    # ========================================================================
    
    @property
    def is_production(self) -> bool:
        return self.ENVIRONMENT == "production"
    
    @property
    def is_development(self) -> bool:
        return self.ENVIRONMENT == "development"
    
    @property
    def detection_model_name(self) -> str:
        """Get full model name"""
        if self.DETECTION_MODEL_PATH:
            return self.DETECTION_MODEL_PATH.stem
        return self.DETECTION_MODEL_TYPE
    
    @property
    def use_gpu(self) -> bool:
        return self.DETECTION_DEVICE in ["cuda", "mps"]
    
    def get_model_path(self) -> str:
        """Get model path or download name"""
        if self.DETECTION_MODEL_PATH and self.DETECTION_MODEL_PATH.exists():
            return str(self.DETECTION_MODEL_PATH)
        # Return model name for Ultralytics to auto-download
        return f"{self.DETECTION_MODEL_TYPE}.pt"
    
    def model_dump_safe(self) -> dict:
        """Export config without sensitive data"""
        data = self.model_dump()
        # No sensitive data in AI service config
        return data


# ============================================================================
# Singleton Pattern - Global Settings Instance
# ============================================================================

_settings: Optional[Settings] = None


def get_settings() -> Settings:
    """
    Get or create settings instance (Singleton)
    Thread-safe singleton for configuration
    """
    global _settings
    if _settings is None:
        _settings = Settings()
    return _settings


# Convenience export
settings = get_settings()


# ============================================================================
# Export
# ============================================================================

__all__ = ["Settings", "get_settings", "settings"]