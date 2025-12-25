# camera/camera_stream.py
import cv2

class CameraStream:
    def __init__(self, camera_index = 0):
        self.camera_index = camera_index
        if camera_index < 0:
            raise ValueError(f"Invalid camera index {camera_index}")

        try:
            self.cap = cv2.VideoCapture(camera_index)
            if not self.cap.isOpened():
                raise OSError(f"Camera device not accessible {camera_index}")

        except Exception as e:
            raise RuntimeError(f"Failed to open camera: {e}")

    def read_frame(self):
        ret, frame = self.cap.read()

        if not ret:
            raise RuntimeError(f"Failed to read camera frame")
        return frame

    def release(self):
        self.cap.release()
        cv2.destroyAllWindows()

    def stream(self):
        """Generator that yields frames continuously"""
        while True:
            # frame = self.read_frame()  can do this but if ret is false raise the exception.
            ret, frame = self.cap.read()
            if not ret:
                break
            yield frame


#if __name__ == '__main__':
#
    #obj = CameraStream()
    ##while True:
        ##frame = obj.read_frame()
        ##cv2.imshow('Camera', frame)
        ##if cv2.waitKey(1) & 0xFF == ord('q'): break
    #for frame in obj.stream():
        ##print("got frame")
        #cv2.imshow('Camera', frame)
        #if cv2.waitKey(1) & 0xFF == ord('q'): break
    #obj.release()

