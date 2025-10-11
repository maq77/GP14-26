from pydantic_settings import BaseSettings, SettingsConfigDict
from functools import lru_cache


class Settings(BaseSettings):
    # App
    app_name: str = "SSSP AI Service"
    app_version: str = "1.0.0"
    debug: bool = False
    
    # Server
    host: str = "0.0.0.0"
    port: int = 8000
    grpc_port: int = 50051
    
    # ML Models
    model_cache_dir: str = "./models"
    yolo_model: str = "yolov8n.pt"
    face_model: str = "buffalo_l"
    gpu_enabled: bool = False
    
    # Redis
    redis_url: str = "redis://localhost:6379"
    
    # Logging
    log_level: str = "INFO"
    
    # Pydantic v2 config
    model_config = SettingsConfigDict(
        env_file=".env",
        case_sensitive=False,
        protected_namespaces=("settings_",),  # avoid "model_" conflicts
    )


@lru_cache()
def get_settings() -> Settings:
    return Settings()
