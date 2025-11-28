import numpy as np
from camera.stream import CameraStream
from face_processing.facenet_encoder import FaceNetModel
from face_processing.mtcnn_detector import MTCNNDetector
from database.db_manager import DBManager
from utills.similarity import cosine_similarity
import time

class FaceRecognitionSystem:
    def __init__(self, device = 'cpu', threshold = 0.6):
        self.camera = CameraStream()
        self.detector = MTCNNDetector()
        self.facenet = FaceNetModel()
        self.db = DBManager()
        self.device = device
        self.threshold = threshold

    def register_face(self, name):
        print("Preparing camera...")

        # warm up camera
        for _ in range(30):  # capture 30 frames to stabilize
            self.camera.read_frame()

        print("Camera ready! Position yourself... (3 seconds)")
        time.sleep(3)  # give user time to position

        frame = self.camera.read_frame()
        boxes, _ = self.detector.detect(frame)


        if boxes is None or len(boxes) == 0:
            raise ValueError("No face detected")

        if len(boxes) > 1:
            raise ValueError("Multiple faces detected. Please ensure only one face is visible")

        box = boxes[0]
        x1, y1, x2, y2 = map(int, box)
        face = frame[y1:y2, x1:x2]

        embedding = self.facenet.get_embedding(face)

        if self.db.face_exists(name):
            raise ValueError(f"Face with name '{name}' already exists")

        self.db.insert_face(name, embedding.cpu().numpy())
        return f"Face registered successfully: {name}"

    def recognize_face(self, frame):
        boxes, _ = self.detector.detect(frame)

        if boxes is None:
            return []

        database = self.db.fetch_all_embeddings()
        if not database:
            return []

        results = []
        for box in boxes:
            x1, y1, x2, y2 = map(int, box)
            face = frame[y1:y2, x1:x2]

            embedding = self.facenet.get_embedding(face).cpu().numpy()

            best_match = None
            best_score = -1

            for name, db_embedding in database.items():
                score = cosine_similarity(embedding, db_embedding)
                if score > best_score:
                    best_score = score
                    best_match = name

            if best_score >= self.threshold:
                results.append({
                    'name': best_match,
                    'confidence': float(best_score),
                    'box': box.tolist()
                })
            else:
                results.append({
                    'name': 'Unknown',
                    'confidence': float(best_score),
                    'box': box.tolist()
                })

        return results

    def update_face(self, name):
        if not self.db.face_exists(name):
            raise ValueError(f"No face found with name '{name}'")

        frame = self.camera.read_frame()
        boxes, _ = self.detector.detect(frame)

        if boxes is None or len(boxes) == 0:
            raise ValueError("No face detected")

        if len(boxes) > 1:
            raise ValueError("Multiple faces detected")

        box = boxes[0]
        x1, y1, x2, y2 = map(int, box)
        face = frame[y1:y2, x1:x2]

        embedding = self.facenet.get_embedding(face)
        self.db.update_face(name, embedding.cpu().numpy())
        return f"Face updated successfully: {name}"

    def delete_face(self, name):
        self.db.delete_face(name)
        return f"Face deleted successfully: {name}"

    def get_all_faces(self):
        return list(self.db.fetch_all_embeddings().keys())

    def stream_recognition(self):
        for frame in self.camera.stream():
            results = self.recognize_face(frame)
            yield frame, results

    def close(self):
        self.camera.release()
        self.db.close()

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        self.close()


#"""Register face from image file"""
#import os
#
#if not os.path.exists(image_path):
    #raise FileNotFoundError(f"Image not found: {image_path}")
#
#frame = cv2.imread(image_path)
#if frame is None:
    #raise ValueError("Failed to load image")
#
#boxes, _ = self.detector.detect(frame)