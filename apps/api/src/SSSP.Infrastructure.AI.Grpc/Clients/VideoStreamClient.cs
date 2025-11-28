using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SSSP.Infrastructure.AI.Grpc.Config;
using SSSP.Infrastructure.AI.Grpc.Interfaces;
using SSSP.Infrastructure.AI.Grpc.Video;
using Sssp.Ai.Stream;

namespace SSSP.Infrastructure.AI.Grpc.Clients
{
    public class VideoStreamClient : IVideoStreamClient
    {
        private readonly VideoStreamService.VideoStreamServiceClient _client;
        private readonly ILogger<VideoStreamClient> _logger;
        private readonly AIOptions _options;

        public VideoStreamClient(
            IOptions<AIOptions> options,
            ILogger<VideoStreamClient> logger)
        {
            _options = options.Value;
            _logger = logger;

            var channel = GrpcChannel.ForAddress(_options.GrpcUrl);
            _client = new VideoStreamService.VideoStreamServiceClient(channel);
        }

        public async Task StreamCameraAsync(
            string cameraId,
            string rtspUrl,
            Func<VideoFrameResponse, Task> onFrameResponse,
            CancellationToken cancellationToken = default)
        {
            using var rtsp = new RtspCamera(cameraId, rtspUrl);

            using var call = _client.StreamFrames(
                cancellationToken: cancellationToken);

            var readTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var response in call.ResponseStream.ReadAllAsync(cancellationToken))
                    {
                        try
                        {
                            await onFrameResponse(response);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex,
                                "Error while processing AI frame response for camera {CameraId}",
                                cameraId);
                        }
                    }
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogError(ex,
                        "Error reading AI streaming response for camera {CameraId}",
                        cameraId);
                }
            }, cancellationToken);

            long frameId = 0;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var jpeg = rtsp.ReadJpeg();
                    if (jpeg == null)
                    {
                        await Task.Delay(50, cancellationToken);
                        continue;
                    }

                    var req = new VideoFrameRequest
                    {
                        CameraId = cameraId,
                        FrameId = frameId++,
                        TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        ImageJpeg = ByteString.CopyFrom(jpeg)
                    };

                    await call.RequestStream.WriteAsync(req, cancellationToken);

                    // 10 FPS
                    await Task.Delay(100, cancellationToken);
                }

                await call.RequestStream.CompleteAsync();
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex,
                    "Error streaming camera {CameraId} to AI",
                    cameraId);
            }

            await readTask;
        }
    }
}
