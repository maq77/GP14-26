using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SSSP.Infrastructure.AI.Grpc.Interfaces;

namespace SSSP.BL.Services
{
    public sealed class CameraMonitoringService
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

        public async Task StartCameraAsync(
            string cameraId,
            string rtspUrl,
            CancellationToken ct)
        {
            _logger.LogInformation(
                "Camera streaming started. CameraId={CameraId} Rtsp={RtspUrl}",
                cameraId,
                rtspUrl);

            try
            {
                await _stream.StreamCameraAsync(
                    cameraId,
                    rtspUrl,
                    async response =>
                    {
                        try
                        {
                            foreach (var face in response.Faces)
                            {
                                var match =
                                    await _faceService.VerifyEmbeddingAsync(
                                        face.Embedding.Vector,
                                        response.CameraId,
                                        ct);

                                if (match.IsMatch)
                                {
                                    _logger.LogInformation(
                                        "Streaming match. Camera={Camera} User={UserId} Similarity={Similarity}",
                                        response.CameraId,
                                        match.UserId,
                                        match.Similarity);
                                }
                                else
                                {
                                    _logger.LogDebug(
                                        "Streaming unknown face. Camera={Camera} BestSimilarity={Similarity}",
                                        response.CameraId,
                                        match.Similarity);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(
                                ex,
                                "Streaming processing error. Camera={CameraId}",
                                cameraId);
                        }
                    },
                    ct);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation(
                    "Camera streaming cancelled. CameraId={CameraId}",
                    cameraId);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(
                    ex,
                    "Camera streaming crashed. CameraId={CameraId}",
                    cameraId);
                throw;
            }
        }
    }
}
