using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Timeout;
using SSSP.Infrastructure.AI.Grpc.Clients;
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

        private static readonly TimeSpan ConnectRetryDelay = TimeSpan.FromSeconds(3);

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
            RtspCamera? stream = null;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    stream = TryCreateRtspCameraWithRetry(cameraId, rtspUrl, cancellationToken);

                    if (stream == null)
                    {
                        _logger.LogWarning(
                            "Camera={CameraId} could not be opened. Exiting stream loop.",
                            cameraId);
                        return;
                    }

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
                        _logger.LogInformation(
                            "AI streaming started. Camera={CameraId} RtspUrl={RtspUrl}",
                            cameraId,
                            rtspUrl);

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

                    _logger.LogWarning(
                        "AI streaming finished for Camera={CameraId}. Will retry connect.",
                        cameraId);
                }
                finally
                {
                    stream?.Dispose();
                    stream = null;
                }

                if (cancellationToken.IsCancellationRequested)
                    break;

                _logger.LogInformation(
                    "Camera={CameraId} reconnecting in {DelaySeconds} sec",
                    cameraId,
                    ConnectRetryDelay.TotalSeconds);

                await Task.Delay(ConnectRetryDelay, cancellationToken);
            }
        }

        private RtspCamera? TryCreateRtspCameraWithRetry(
            string cameraId,
            string rtspUrl,
            CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation(
                        "Opening RTSP stream. Camera={CameraId} RtspUrl={RtspUrl}",
                        cameraId,
                        rtspUrl);

                    return new RtspCamera(cameraId, rtspUrl);
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning(
                        ex,
                        "RTSP stream not accessible. Camera={CameraId} RtspUrl={RtspUrl}. Retrying in {DelaySeconds} sec",
                        cameraId,
                        rtspUrl,
                        ConnectRetryDelay.TotalSeconds);

                    try
                    {
                        Task.Delay(ConnectRetryDelay, cancellationToken).Wait(cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            return null;
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
