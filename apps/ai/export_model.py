# apps/ai/export_model.py
from ultralytics import YOLO

def export_models(model_path='runs/detect/sssp_v1/weights/best.pt'):
    """
    Export model to different formats for deployment
    """
    model = YOLO(model_path)
    
    print("Exporting models...")
    
    # 1. ONNX (recommended for production)
    model.export(format='onnx', simplify=True)
    print("✓ ONNX export complete")
    
    # 2. TensorRT (for NVIDIA GPUs - fastest)
    # model.export(format='engine', device=0)
    # print("✓ TensorRT export complete")
    
    # 3. CoreML (for iOS/macOS)
    # model.export(format='coreml')
    
    # 4. TFLite (for mobile/edge devices)
    # model.export(format='tflite')
    
    print(f"\nExported models saved in: runs/detect/sssp_v1/weights/")

if __name__ == "__main__":
    export_models()