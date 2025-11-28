# face_processing/mtcnn_detector.py
from facenet_pytorch import MTCNN
from camera.frame_utils import bgr_to_rgb

class MTCNNDetector:
    def __init__(self, device="cpu"):
        self.device = device # ('cuda' if torch.cude.is_available() else 'cpu')
        self.mtcnn = MTCNN(keep_all=True, device=self.device)

    def detect(self, frame):
        """
        Input: frame numpy array BGR
        Output: List of detected faces
        """
        rgb_frame = bgr_to_rgb(frame)
        boxes, probs = self.mtcnn.detect(rgb_frame)

        faces = []
        if boxes is not None:
            for box in boxes:
                x1, y1, x2, y2 = [int(b) for b in box]  # ---> map(int , box)
                face = rgb_frame[y1:y2, x1:x2].copy()
                faces.append(face)

        return boxes, faces
