# apps/ai/benchmark_model.py
from ultralytics import YOLO
import time
import cv2
import numpy as np

def benchmark_inference(model_path, num_iterations=100):
    """
    Benchmark model inference speed
    """
    model = YOLO(model_path)
    
    # Create dummy image
    dummy_image = np.random.randint(0, 255, (640, 640, 3), dtype=np.uint8)
    
    # Warmup
    for _ in range(10):
        model(dummy_image, verbose=False)
    
    # Benchmark
    start_time = time.time()
    for _ in range(num_iterations):
        results = model(dummy_image, verbose=False)
    end_time = time.time()
    
    avg_time = (end_time - start_time) / num_iterations
    fps = 1 / avg_time
    
    print(f"\n{'='*50}")
    print(f"Benchmark Results ({num_iterations} iterations):")
    print(f"  Average inference time: {avg_time*1000:.2f} ms")
    print(f"  FPS: {fps:.2f}")
    print(f"{'='*50}\n")
    
    return avg_time, fps

if __name__ == "__main__":
    # Benchmark PyTorch model
    print("Benchmarking PyTorch model...")
    benchmark_inference('runs/detect/sssp_v1/weights/best.pt')
    
    # Benchmark ONNX model
    print("\nBenchmarking ONNX model...")
    benchmark_inference('runs/detect/sssp_v1/weights/best.onnx')