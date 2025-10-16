"""Detection model lifecycle component."""
import structlog
import numpy as np

from ..base import BaseLifecycleComponent, ComponentPriority, ComponentState
from ..registry import register_component
from ....models.object.model_loader import get_model_loader

logger = structlog.get_logger(__name__)


@register_component
class DetectionModelComponent(BaseLifecycleComponent):
    """
    Manages YOLO object detection model lifecycle.
    
    Responsibilities:
    - Load detection model on startup
    - Warm up model with dummy inference
    - Monitor GPU/CPU device status
    - Unload model on shutdown
    """
    
    name = "DetectionModel"
    priority = ComponentPriority.HIGH
    startup_timeout = 90  # Model loading + warmup can take time
    shutdown_timeout = 15
    
    def __init__(self):
        super().__init__()
        self.model_loader = None
        self.warmup_iterations = 3
        self.warmup_image_size = (640, 640)
    
    async def startup(self) -> None:
        """Load and warm up the detection model."""
        self.safe_log("loading_detection_model")
        
        # Get singleton model loader
        self.model_loader = get_model_loader()
        
        # Load the model
        self.model_loader.load_detector()
        
        # Collect metadata
        self.metadata.update({
            "device": str(self.model_loader.device),
            "model_type": getattr(self.model_loader, 'model_name', 'YOLO'),
            "model_path": getattr(self.model_loader, 'model_path', 'unknown'),
            "warmup_iterations": self.warmup_iterations,
            "input_size": self.warmup_image_size,
        })
        
        self.safe_log(
            "model_loaded",
            device=self.metadata["device"],
            model=self.metadata["model_type"]
        )
        
        # Warm up the model
        self.safe_log("warming_up_model", iterations=self.warmup_iterations)
        await self._warmup_model()
        
        self.safe_log("detection_model_ready")
    
    async def _warmup_model(self) -> None:
        """
        Warm up model with dummy inference.
        
        This:
        - Allocates GPU memory
        - Compiles CUDA kernels
        - Initializes inference pipeline
        - Reduces first real inference latency
        """
        try:
            # Create dummy image
            h, w = self.warmup_image_size
            dummy_image = np.random.randint(0, 255, (h, w, 3), dtype=np.uint8)
            
            for i in range(self.warmup_iterations):
                # Run inference (result is discarded)
                _ = self.model_loader.detect(dummy_image)
                
                self.safe_log(
                    "warmup_iteration_completed",
                    iteration=i + 1,
                    total=self.warmup_iterations
                )
            
            self.metadata["warmup_completed"] = True
            self.safe_log("model_warmup_completed")
            
        except Exception as e:
            self.log_error("model_warmup_failed", e)
            self.metadata["warmup_completed"] = False
            # Don't fail startup - model is loaded even if warmup fails
            logger.warning(
                "continuing_without_warmup",
                component=self.name,
                reason=str(e)
            )
    
    async def shutdown(self) -> None:
        """Unload model and free resources."""
        if self.model_loader:
            self.safe_log("unloading_detection_model")
            
            try:
                self.model_loader.unload_detector()
                self.safe_log("detection_model_unloaded")
            except Exception as e:
                # Log but don't raise - best effort shutdown
                self.log_error("model_unload_error", e)
    
    async def health_check(self) -> bool:
        """Check if model is loaded and functional."""
        try:
            if not self.model_loader:
                return False
            
            # Check if model attribute exists and is not None
            model_loaded = (
                hasattr(self.model_loader, 'model') and 
                self.model_loader.model is not None
            )
            
            return model_loaded and self.state == ComponentState.READY
            
        except Exception as e:
            self.log_error("health_check_failed", e)
            return False
    
    def get_metrics(self) -> dict:
        """Return model-specific metrics."""
        base_metrics = super().get_metrics()
        
        if self.model_loader:
            model_loaded = (
                hasattr(self.model_loader, 'model') and 
                self.model_loader.model is not None
            )
            
            base_metrics["model_info"] = {
                "loaded": model_loaded,
                "device": self.metadata.get("device"),
                "type": self.metadata.get("model_type"),
                "warmup_completed": self.metadata.get("warmup_completed", False),
            }
        
        return base_metrics
