# apps/ai/validate_model.py
from ultralytics import YOLO
import cv2

def validate_model(model_path='runs/detect/sssp_v1/weights/best.pt'):
    """
    Validate trained model on test set
    """
    model = YOLO(model_path)
    
    # Validate
    metrics = model.val(
        data='data/dataset.yaml',
        imgsz=640,
        batch=16,
        conf=0.25,  # Confidence threshold
        iou=0.45,   # IoU threshold for NMS
        device='cuda' if torch.cuda.is_available() else 'cpu'
    )
    
    # Print metrics
    print(f"\n{'='*50}")
    print(f"Validation Results:")
    print(f"  mAP@0.5: {metrics.box.map50:.3f}")
    print(f"  mAP@0.5:0.95: {metrics.box.map:.3f}")
    print(f"  Precision: {metrics.box.mp:.3f}")
    print(f"  Recall: {metrics.box.mr:.3f}")
    print(f"{'='*50}\n")
    
    return metrics

def test_single_image(model_path, image_path):
    """
    Test model on a single image
    """
    model = YOLO(model_path)
    
    # Run inference
    results = model(image_path, conf=0.25)
    
    # Visualize results
    for r in results:
        im_array = r.plot()  # Plot with bounding boxes
        cv2.imshow('Detection Result', im_array)
        cv2.waitKey(0)
        cv2.destroyAllWindows()
        
        # Print detections
        for box in r.boxes:
            cls = int(box.cls[0])
            conf = float(box.conf[0])
            print(f"Detected: {model.names[cls]} (confidence: {conf:.2f})")

if __name__ == "__main__":
    # Validate model
    validate_model()
    
    # Test on single image
    # test_single_image('runs/detect/sssp_v1/weights/best.pt', 'test_image.jpg')