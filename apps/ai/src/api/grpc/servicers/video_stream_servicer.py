import cv2
import numpy as np
from typing import Iterator

from packages.contracts.python import video_stream_pb2, video_stream_pb2_grpc
from ....services.ml.Face_Recognition_Service import FaceRecognitionService
import structlog

logger = structlog.get_logger("grpc.video_stream_servicer")


class VideoStreamService(video_stream_pb2_grpc.VideoStreamServiceServicer):
    def __init__(self, face_service: FaceRecognitionService):
        self.face_service = face_service
        logger.info("video_stream_servicer_initialized")

    def StreamFrames(
        self,
        request_iterator: Iterator[video_stream_pb2.VideoFrameRequest],
        context,
    ):
        for req in request_iterator:
            frame = self._decode_jpeg(req.image_jpeg)
            results = self.face_service.process_frame(frame)
            resp = video_stream_pb2.VideoFrameResponse(
                camera_id=req.camera_id,
                frame_id=req.frame_id,
            )
            for r in results:
                x, y, w, h = r["bbox"]
                emb = r["embedding"]
                emb_list = [float(v) for v in emb.tolist()]
                box_msg = video_stream_pb2.FaceBox(
                    x=float(x),
                    y=float(y),
                    w=float(w),
                    h=float(h),
                )
                emb_msg = video_stream_pb2.FaceEmbedding(vector=emb_list)
                resp.faces.add(box=box_msg, embedding=emb_msg)
            logger.debug(
                "stream_frame_processed",
                camera_id=req.camera_id,
                frame_id=req.frame_id,
                faces=len(resp.faces),
            )
            yield resp

    def _decode_jpeg(self, jpeg_bytes: bytes):
        arr = np.frombuffer(jpeg_bytes, np.uint8)
        frame = cv2.imdecode(arr, cv2.IMREAD_COLOR)
        return frame
