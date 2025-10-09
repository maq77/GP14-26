import grpc, inference_pb2, inference_pb2_grpc
from concurrent import futures

class InferenceServicer(inference_pb2_grpc.InferenceServicer):
    def DetectObjects(self, request, context):
        # TODO: run YOLO on request.image_url
        return inference_pb2.DetectResponse(detections=[])
    def VerifyFace(self, request, context):
        # TODO: run face verify
        return inference_pb2.FaceVerifyResponse(match=False, score=0.0)

def serve():
    server = grpc.server(futures.ThreadPoolExecutor(max_workers=4))
    inference_pb2_grpc.add_InferenceServicer_to_server(InferenceServicer(), server)
    server.add_insecure_port("[::]:50051")
    server.start(); server.wait_for_termination()

if __name__ == "__main__":
    serve()
