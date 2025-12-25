# face_processing/facenet_model.py
from facenet_pytorch import InceptionResnetV1
import torch
from utills.preprocessing import resize_image, normalize_image
from camera.frame_utils import bgr_to_rgb


class FaceNetModel:
    def __init__(self, device='cpu', pretrained=True):
        self.device = device  #('cuda' if torch.cuda.is_available() else 'cpu')
        self.model = InceptionResnetV1(pretrained='vggface2' if pretrained else None).eval().to(self.device)

    def get_embedding(self, face):
        """
        Input: face (np array BGR)
        Output: embedding (torch tensor)
        """
        if face is None or face.size == 0:
            return None

        rgb_face = bgr_to_rgb(face)
        resized_face = resize_image(rgb_face, (160, 160))
        normalized_face = normalize_image(resized_face)

        #tensor_face = torch.tensor(normalized_face, dtype=torch.float33, device=self.device)
        tensor_face = torch.from_numpy(normalized_face).float()  #faster.
        tensor_face = tensor_face.permute(2, 0, 1).unsqueeze(0)

        with torch.no_grad():
            embedding = self.model(tensor_face)

        return embedding.squeeze()
