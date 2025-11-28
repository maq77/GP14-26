# camera/frame_utils.py
import cv2
import os

def bgr_to_rgb(frame):
    """Convert BGR image to RGB image"""
    return cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)

def save_frame(frame, path="capture", filename="frame.png"):
    """Save frame to disk"""
    if not os.path.exists(path):
        os.makedirs(path)
    full_path = os.path.join(path, filename)
    cv2.imwrite(full_path, frame)
    return full_path

#def draw_box(frame, box, color=(0, 255, 0), thickness=2):
    #"""Draw a bounding box on the frame"""
    #x, y, w, h = box
    #x, y, w, h = int(x), int(y), int(w), int(h)
    #cv2.rectangle(frame, (x, y), (x + w, y + h), color, thickness)
    #return frame
