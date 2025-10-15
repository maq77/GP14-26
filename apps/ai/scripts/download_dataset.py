# apps/ai/scripts/download_dataset.py
from roboflow import Roboflow
import os

# Initialize Roboflow (free API key from roboflow.com)
rf = Roboflow(api_key="YOUR_API_KEY")

# Download relevant datasets
# 1. COCO subset (people, vehicles)
# 2. Waste detection dataset
# 3. Weapon detection dataset

# Example: Download waste detection dataset
project = rf.workspace("waste-detection").project("trash-detection")
dataset = project.version(1).download("yolov8")

print(f"Dataset downloaded to: {dataset.location}")