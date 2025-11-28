# utils/preprocessing.py
import cv2

def resize_image(image, size=(160, 160)):
    """Resize image to target size"""
    return cv2.resize(image, size)

def normalize_image(image):
    """Normalize pixel values to [0, 1]"""
    return image / 255.0

#def align_face(image, landmarks=None):
    #"""
    #Placeholder for face alignment using landmarks.
    #Currently returns the image as-is.
    #"""
    #return image
