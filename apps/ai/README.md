# SSSP AI Service (FastAPI)
Computer Vision & ML service for object detection, face recognition, and AQI calculation

## Quick Start
```bash
cd apps/ai
python -m venv venv
source venv/bin/activate  # Windows: venv\Scripts\activate
pip install -r requirements.txt

cp .env.example .env
python scripts/download_models.py
uvicorn src.api.main:app --reload --host 0.0.0.0 --port 8000
```

## Configuration
Edit `.env`:
```bash
GPU_ENABLED=false
YOLO_MODEL=yolov8n.pt
REDIS_URL=redis://localhost:6379
```

## Project Structure
```bash
src/
├── api/routes/
├── models/
├── services/
└── schemas/
```

## Common Commands
```bash
uvicorn src.api.main:app --reload
pytest
gunicorn src.api.main:app --workers 4 --worker-class uvicorn.workers.UvicornWorker
```

## Test Detection
```bash
curl -X POST http://localhost:8000/api/v1/detect -F "file=@test_image.jpg"
```

## Models
- **YOLOv8n**: Fast, CPU-friendly
- **YOLOv8m**: Balanced
- **YOLOv8l**: Most accurate, needs GPU

## Troubleshooting
- **Out of memory**: Use smaller model (yolov8n)
- **Slow on CPU**: Use yolov8n
- **Model not found**: Run `python scripts/download_models.py`

## Contact
ML Team - #ml-channel
