using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Timeout;
using SSSP.Infrastructure.AI.Grpc.Config;
using SSSP.Infrastructure.AI.Grpc.Interfaces;
using SSSP.Infrastructure.AI.Grpc.Video;
using Sssp.Ai.Stream;
using Grpc.Core;

namespace SSSP.Infrastructure.AI.Grpc.Clients
{
    public sealed class VideoStreamClient : IVideoStreamClient
    {
        private readonly ILogger<VideoStreamClient> _logger;
        private readonly VideoStreamService.VideoStreamServiceClient _client;
        private readonly AsyncTimeoutPolicy _timeoutPolicy;

        public VideoStreamClient(
            GrpcChannelFactory channelFactory,
            ILogger<VideoStreamClient> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var channel = channelFactory.CreateAiChannel();
            _client = new VideoStreamService.VideoStreamServiceClient(channel);

            _timeoutPolicy = Policy.TimeoutAsync(10);

            _logger.LogInformation("VideoStreamClient initialized.");
        }

        public async Task StreamCameraAsync(
            string cameraId,
            string rtspUrl,
            Func<VideoFrameResponse, Task> onFrameResponse,
            CancellationToken cancellationToken = default)
        {
            using var stream = new RtspCamera(cameraId, rtspUrl);

            using var call = _client.StreamFrames(cancellationToken: cancellationToken);

            var readTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var response in call.ResponseStream.ReadAllAsync(cancellationToken))
                    {
                        await SafeHandleFrame(onFrameResponse, response);
                    }
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogError(
                        ex,
                        "AI streaming read failed. Camera={CameraId}",
                        cameraId);
                }
            }, cancellationToken);

            long frameId = 0;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var jpeg = stream.ReadJpeg();
                    if (jpeg == null)
                    {
                        await Task.Delay(50, cancellationToken);
                        continue;
                    }

                    var request = new VideoFrameRequest
                    {
                        CameraId = cameraId,
                        FrameId = frameId++,
                        TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        ImageJpeg = ByteString.CopyFrom(jpeg)
                    };

                    await _timeoutPolicy.ExecuteAsync(() =>
                        call.RequestStream.WriteAsync(request, cancellationToken));

                    // 10 FPS
                    await Task.Delay(100, cancellationToken);
                }

                await call.RequestStream.CompleteAsync();
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(
                    ex,
                    "AI camera streaming failed. Camera={CameraId}",
                    cameraId);
            }

            await readTask;
        }

        private async Task SafeHandleFrame(
            Func<VideoFrameResponse, Task> handler,
            VideoFrameResponse response)
        {
            try
            {
                await handler(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Frame handler failed. Camera={CameraId} Frame={FrameId}",
                    response.CameraId,
                    response.FrameId);
            }
        }
    }
}
