# config.py

# Camera settings
CAMERA_INDEX = 0
FRAME_WIDTH = 640
FRAME_HEIGHT = 480

# Motion detector
MOTION_MIN_AREA = 500
GAUSSIAN_BLUR_KERNEL = (21, 21)
THRESHOLD = 25

# FaceNet settings
FACENET_IMAGE_SIZE = (160, 160)
EMBEDDING_THRESHOLD = 0.6

# Database settings
DB_HOST = "localhost"
DB_PORT = 5432
DB_NAME ="smart_gate"
DB_USER ="smart_user"
DB_PASSWORD = "REDA"

# Logging
LOG_FILE = "logs/app.log"

