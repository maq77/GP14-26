using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SSSP.Infrastructure.AI.Grpc.Interfaces;

namespace SSSP.BL.Services
{
    public class CameraMonitoringService
    {
        private readonly IVideoStreamClient _stream;
        private readonly FaceRecognitionService _faceService;
        private readonly ILogger<CameraMonitoringService> _logger;

        public CameraMonitoringService(
            IVideoStreamClient stream,
            FaceRecognitionService faceService,
            ILogger<CameraMonitoringService> logger)
        {
            _stream = stream;
            _faceService = faceService;
            _logger = logger;
        }

        public async Task StartCameraAsync(string cameraId, string rtspUrl, CancellationToken ct)
        {
            _logger.LogInformation(
                "Starting camera monitoring for camera {CameraId} on RTSP {RtspUrl}",
                cameraId,
                rtspUrl);

            await _stream.StreamCameraAsync(
                cameraId,
                rtspUrl,
                async response =>
                {
                    _logger.LogDebug(
                        "AI streaming frame received. Camera {CameraId} Frame {FrameId} Faces {FaceCount}",
                        response.CameraId,
                        response.FrameId,
                        response.Faces.Count);

                    foreach (var face in response.Faces)
                    {
                        var match = await _faceService.VerifyEmbeddingAsync(
                            face.Embedding.Vector,
                            response.CameraId,
                            ct);

                        if (match.IsMatch)
                        {
                            _logger.LogInformation(
                                "Camera {CameraId} frame {FrameId} recognized user {UserId} with similarity {Similarity}",
                                response.CameraId,
                                response.FrameId,
                                match.UserId,
                                match.Similarity);
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Camera {CameraId} frame {FrameId} unknown face. BestSimilarity {Similarity}",
                                response.CameraId,
                                response.FrameId,
                                match.Similarity);
                        }
                    }
                },
                ct);

            _logger.LogInformation(
                "Camera monitoring finished for camera {CameraId}",
                cameraId);
        }
    }
}
