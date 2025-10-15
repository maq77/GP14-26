# apps/ai/train_model.py
from ultralytics import YOLO
import torch

def train_object_detection():
    """
    Train custom YOLO model for SSSP
    """
    # Check if GPU available
    device = 'cuda' if torch.cuda.is_available() else 'cpu'
    print(f"Training on: {device}")
    
    # Load pre-trained model (transfer learning)
    model = YOLO('yolov8n.pt')  # nano - fastest
    # model = YOLO('yolov8s.pt')  # small - balanced
    # model = YOLO('yolov8m.pt')  # medium - more accurate
    
    # Training parameters
    results = model.train(
        data='data/dataset.yaml',
        epochs=100,              # Adjust based on dataset size
        imgsz=640,               # Image size
        batch=16,                # Batch size (adjust for GPU memory)
        device=device,
        patience=20,             # Early stopping
        save=True,
        project='runs/detect',
        name='sssp_v1',
        
        # Optimization
        optimizer='AdamW',
        lr0=0.001,              # Initial learning rate
        weight_decay=0.0005,
        
        # Augmentation
        hsv_h=0.015,            # HSV-Hue augmentation
        hsv_s=0.7,              # HSV-Saturation
        hsv_v=0.4,              # HSV-Value
        degrees=0.0,            # Rotation
        translate=0.1,          # Translation
        scale=0.5,              # Scale
        flipud=0.0,             # Flip up-down
        fliplr=0.5,             # Flip left-right
        mosaic=1.0,             # Mosaic augmentation
    )
    
    # Print results
    print(f"\n{'='*50}")
    print(f"Training completed!")
    print(f"Best model saved at: {results.save_dir}/weights/best.pt")
    print(f"{'='*50}\n")
    
    return results

if __name__ == "__main__":
    train_object_detection()