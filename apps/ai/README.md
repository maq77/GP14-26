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
apps/ai/
├── src/
│   ├── __init__.py
│   │
│   ├── api/                           # -- API Layer (Thin - Only Inference Endpoints)
│   │   ├── __init__.py
│   │   ├── main.py                    # FastAPI app entry point
│   │   ├── lifespan.py                # Startup/shutdown (load models)
│   │   │
│   │   ├── routes/                    # REST endpoints (optional - for testing)
│   │   │   ├── __init__.py
│   │   │   ├── health.py              # Health check
│   │   │   └── inference.py           # Direct inference endpoint (testing only)
│   │   │
│   │   ├── metrics/                    
│   │   │   ├── __init__.py
│   │   │   └── base.py           
│   │   │
│   │   ├── lifespan/                    # lifespan manager
│   │   │   ├── __init__.py
│   │   │   ├── modules/                #detection.py
│   │   │   ├── base.py            
│   │   │   ├── registry.py            
│   │   │   ├── health_registry.py    
│   │   │   └── manager.py           
│   │   └── grpc/                      # gRPC Server (NO .proto files here!)
│   │       ├── __init__.py
│   │       ├── server.py              # gRPC server setup
│   │       └── servicers/             # gRPC service implementations
│   │           ├── __init__.py
│   │           ├── detection_servicer.py    # Implements DetectionServiceServicer
│   │           ├── face_servicer.py         # Implements FaceServiceServicer
│   │           ├── behavior_servicer.py     # Implements BehaviorServiceServicer
│   │           └── aqi_servicer.py          # Implements AqiServiceServicer
│   │           
│   │           # NOTE: All servicers IMPORT from packages/contracts/
│   │           # Example:
│   │           # from packages.contracts.python import detection_pb2_grpc
│   │
│   ├── core/                          # -- Core Configuration
│   │   ├── __init__.py
│   │   ├── config.py                  # Settings (model paths, device, thresholds)
│   │   ├── logging.py                 # Structured logging
│   │   ├── exceptions.py              # Custom ML exceptions
│   │   └── constants.py               # Model constants (class names, etc.)
│   │
│   ├── schemas/                       # -- DTOs (Input/Output for gRPC & REST)
│   │   ├── __init__.py
│   │   ├── detection.py               # Detection request/response
│   │   ├── face.py                    # Face request/response
│   │   ├── behavior.py                # Behavior request/response
│   │   └── aqi.py                     # AQI request/response
│   │
│   ├── services/                      # -- Inference Services (Business Logic)
│   │   ├── __init__.py
│   │   ├── object_detection_service.py    # Object detection orchestration
│   │   ├── face_recognition_service.py    # Face recognition orchestration
│   │   ├── behavior_analysis_service.py   # Behavior analysis orchestration
│   │   ├── waste_detection_service.py     # Waste detection (specialized)
│   │   ├── vandalism_detection_service.py # Vandalism detection
│   │   └── aqi_service.py                 # AQI calculation & recommendations
│   │
│   └── models/                        # -- ML Models (Core AI Logic)
│   │   ├── __init__.py
│   │   │
│   │   ├── object/                    # Object Detection Models
│   │   │   ├── __init__.py
│   │   │   ├── train.py
│   │   │   ├── benchmark.py
│   │   │   ├── export.py
│   │   │   ├── yolo_detector.py       # YOLO inference wrapper
│   │   │   ├── model_loader.py        # Singleton model loader
│   │   │   ├── preprocessor.py        # Image preprocessing (resize, normalize)
│   │   │   ├── postprocessor.py       # NMS, filtering, bbox conversion
│   │   │   └── tracker.py             # Object tracking (DeepSORT/ByteTrack)
│   │   │
│   │   ├── face/                      # Face Recognition Models
│   │   │   ├── __init__.py
│   │   │   ├── detector.py            # Face detection (RetinaFace/MTCNN)
│   │   │   ├── recognizer.py          # Face recognition (InsightFace)
│   │   │   ├── embedder.py            # Face embedding extraction
│   │   │   ├── matcher.py             # Face matching (cosine similarity)
│   │   │   └── database.py            # Face database (FAISS index)
│   │   │
│   │   ├── behavior/                  # Behavior Analysis Models
│   │   │   ├── __init__.py
│   │   │   ├── pose_estimator.py      # Pose estimation (MediaPipe/YOLO-Pose)
│   │   │   ├── action_classifier.py   # Action classification (fighting, running)
│   │   │   ├── sequence_analyzer.py   # Temporal analysis (multi-frame)
│   │   │   └── anomaly_detector.py    # Abnormal behavior detection
│   │   │
│   │   └── shared/                    # Shared Utilities
│   │       ├── __init__.py
│   │       ├── model_factory.py       # Factory pattern (create models)
│   │       ├── model_registry.py      # Model versioning/management
│   │       ├── base_model.py          # Abstract base model class
│   │       └── optimization.py        # ONNX/TensorRT conversion
│   │
│   │
│   └── pipelines/
│          ├── __init__.py
│          ├── train_pipeline.py       # Factory pattern (create models)
│          ├── eval_pipeline.py      # Model versioning/management
│          ├── eval_pipline.py          # Abstract base model class
│          └── deploy_pipeline.py        # ONNX/TensorRT conversion
│
│
├── tests/                             # -- Tests
│   ├── __init__.py
│   ├── conftest.py                    # Pytest fixtures
│   │
│   ├── unit/                          # Unit tests
│   │   ├── models/
│   │   │   ├── test_yolo_detector.py
│   │   │   ├── test_face_recognizer.py
│   │   │   └── test_behavior_analyzer.py
│   │   └── services/
│   │       └── test_detection_service.py
│   │
│   ├── integration/                   # Integration tests
│   │   ├── test_grpc_detection.py
│   │   └── test_grpc_face.py
│   │
│   └── fixtures/                      # Test data
│       ├── images/
│       └── videos/
│
├── notebooks/                         # --Jupyter Notebooks
│   ├── 01_model_training.ipynb
│   ├── 02_model_evaluation.ipynb
│   ├── 03_inference_benchmarking.ipynb
│   └── 04_hyperparameter_tuning.ipynb
│
├── data/                              # -- Data Directory
│   │
│   ├── raw/                           # --Raw Data (Original, Unprocessed)
│   │   ├── images/                    # Raw images for training
│   │   │   ├── waste/                 # Waste detection images
│   │   │   ├── vehicles/              # Vehicle images
│   │   │   ├── people/                # Person images
│   │   │   └── vandalism/             # Vandalism images
│   │   ├── videos/                    # Raw video footage
│   │   │   ├── cctv_footage/
│   │   │   └── test_streams/
│   │   └── annotations/               # Original annotations (COCO, YOLO format)
│   │       ├── coco_format/
│   │       └── yolo_format/
│   │
│   ├── processed/                     # --Processed Data (Ready for Training)
│   │   ├── train/                     # Training set (70-80%)
│   │   │   ├── images/
│   │   │   └── labels/                # YOLO format (.txt)
│   │   ├── val/                       # Validation set (10-15%)
│   │   │   ├── images/
│   │   │   └── labels/
│   │   ├── test/                      # Test set (10-15%)
│   │   │   ├── images/
│   │   │   └── labels/
│   │   └── dataset.yaml               # YOLO dataset config
│   │
│   ├── augmented/                     # --Augmented Data (Optional)
│   │   ├── train/                     # Augmented training data
│   │   │   ├── images/
│   │   │   └── labels/
│   │   └── augmentation_log.json      # Augmentation metadata
│   │
│   ├── external/                      # -- External Datasets (Downloaded)
│   │   ├── coco/                      # COCO dataset
│   │   ├── taco/                      # Trash Annotations in Context
│   │   ├── wider_face/                # Face detection dataset
│   │   └── README.md                  # Dataset sources & licenses
│   │
│   ├── faces/                         # --Face Recognition Database
│   │   ├── authorized/                # Authorized people faces
│   │   │   ├── employees/             # Employee photos
│   │   │   │   ├── john_doe/
│   │   │   │   │   ├── img1.jpg
│   │   │   │   │   ├── img2.jpg
│   │   │   │   │   └── metadata.json
│   │   │   │   └── jane_smith/
│   │   │   └── vip/                   # VIP/whitelist
│   │   ├── blacklist/                 # Wanted/blacklisted people
│   │   │   └── person_id_123/
│   │   ├── embeddings/                # Generated embeddings
│   │   │   ├── embeddings.pkl         # Serialized embeddings
│   │   │   ├── index.faiss            # FAISS index for fast search
│   │   │   └── metadata.json          # ID → Name mapping
│   │   └── unknown/                   # Unidentified faces (for review)
│   │
│   ├── models/                        # -- Trained Model Weights(brain)
│   │   ├── production/                # Production models
│   │   │   ├── yolo11s_sssp_v1.0.pt  # Deployed YOLO model
│   │   │   ├── face_recognizer_v1.onnx
│   │   │   └── model_metadata.json    # Version, accuracy, etc.
│   │   ├── experiments/               # Experimental models
│   │   │   ├── yolo11m_experiment_1/
│   │   │   └── yolo11l_experiment_2/
│   │   ├── checkpoints/               # Training checkpoints
│   │   │   ├── epoch_10.pt
│   │   │   ├── epoch_50.pt
│   │   │   └── best.pt                # Best performing checkpoint
│   │   └── pretrained/                # Pre-trained base models
│   │       ├── yolo11n.pt
│   │       ├── yolo11s.pt
│   │       └── yolo11m.pt
│   │
│   ├── cache/                         # -- Temporary Cache (gitignored)
│   │   ├── inference/                 # Cached inference results
│   │   ├── preprocessing/             # Preprocessed images cache
│   │   └── .gitkeep
│   │
│   ├── logs/                          # --Logs (gitignored)
│   │   ├── training/                  # Training logs
│   │   │   ├── run_2025_01_10/
│   │   │   │   ├── results.csv
│   │   │   │   ├── confusion_matrix.png
│   │   │   │   └── tensorboard/
│   │   │   └── run_2025_01_15/
│   │   ├── inference/                 # Inference logs
│   │   └── errors/                    # Error logs
│   │
│   ├── splits/                        # -- Dataset Split Configuration
│   │   ├── split_v1.json              # Train/val/test split metadata
│   │   └── stratified_split.json      # Stratified split (balanced classes)
│   │
│   └── README.md                      # Data directory documentation
│
├── .env.example                       # Environment template
├── .env.development
├── .gitignore
├── pyproject.toml                     # Poetry config
├── requirements.txt                   # Dependencies
├── requirements-dev.txt
├── pytest.ini
├── README.md
└── CHANGELOG.md
```

## Common Commands
```bash
python -m apps.ai.src.api.main
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
