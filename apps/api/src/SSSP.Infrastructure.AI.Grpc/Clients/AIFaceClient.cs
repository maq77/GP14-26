using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using SSSP.Infrastructure.AI.Grpc.Interfaces;
using SSSP.Infrastructure.AI.Grpc.Clients;
using Sssp.Ai.Face;

namespace SSSP.Infrastructure.AI.Grpc.Clients
{
    public sealed class AIFaceClient : IAIFaceClient
    {
        private readonly ILogger<AIFaceClient> _logger;
        private readonly FaceService.FaceServiceClient _client;
        private readonly AsyncPolicy _policy;

        public AIFaceClient(
            GrpcChannelFactory channelFactory,
            ILogger<AIFaceClient> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var channel = channelFactory.CreateAiChannel();
            _client = new FaceService.FaceServiceClient(channel);

            _policy = BuildPolicy();

            _logger.LogInformation("AIFaceClient initialized.");
        }

        public async Task<FaceVerifyResponse> VerifyFaceAsync(
            byte[] imageBytes,
            string cameraId)
        {
            ValidateImage(imageBytes);

            var request = new FaceVerifyRequest
            {
                Image = ByteString.CopyFrom(imageBytes),
                CameraId = cameraId ?? string.Empty,
                CheckBlacklist = true
            };

            var sw = Stopwatch.StartNew();

            return await _policy.ExecuteAsync(async () =>
            {
                try
                {
                    _logger.LogInformation(
                        "VerifyFace sent. Camera={CameraId} Size={Size}",
                        cameraId,
                        imageBytes.Length);

                    var response = await _client.VerifyFaceAsync(request);

                    sw.Stop();

                    _logger.LogInformation(
                        "VerifyFace response. Camera={CameraId} Success={Success} Match={Match} Authorized={Auth} ElapsedMs={Elapsed}",
                        cameraId,
                        response.Success,
                        response.MatchFound,
                        response.IsAuthorized,
                        sw.ElapsedMilliseconds);

                    return response;
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    _logger.LogError(
                        ex,
                        "VerifyFace failed. Camera={CameraId} ElapsedMs={Elapsed}",
                        cameraId,
                        sw.ElapsedMilliseconds);
                    throw;
                }
            });
        }

        public async Task<FaceEmbeddingResponse> ExtractEmbeddingAsync(
            byte[] image,
            string? cameraId = null,
            CancellationToken cancellationToken = default)
        {
            ValidateImage(image);

            var request = new FaceEmbeddingRequest
            {
                Image = ByteString.CopyFrom(image)
            };

            if (!string.IsNullOrWhiteSpace(cameraId))
                request.CameraId = cameraId;

            var sw = Stopwatch.StartNew();

            return await _policy.ExecuteAsync(async () =>
            {
                try
                {
                    _logger.LogInformation(
                        "ExtractEmbedding sent. Camera={CameraId} Size={Size}",
                        cameraId ?? "N/A",
                        image.Length);

                    var response = await _client.ExtractEmbeddingAsync(
                        request,
                        cancellationToken: cancellationToken);

                    sw.Stop();

                    _logger.LogInformation(
                        "ExtractEmbedding response. Camera={CameraId} Success={Success} Dim={Dim} FaceDetected={Detected} ElapsedMs={Elapsed}",
                        cameraId ?? "N/A",
                        response.Success,
                        response.Embedding.Count,
                        response.FaceDetected,
                        sw.ElapsedMilliseconds);

                    return response;
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    _logger.LogError(
                        ex,
                        "ExtractEmbedding failed. Camera={CameraId} ElapsedMs={Elapsed}",
                        cameraId ?? "N/A",
                        sw.ElapsedMilliseconds);
                    throw;
                }
            });
        }

        private static void ValidateImage(byte[] image)
        {
            if (image == null || image.Length == 0)
                throw new ArgumentException("Image is empty", nameof(image));
        }

        private AsyncPolicy BuildPolicy()
        {
            var retry = Policy
                .Handle<RpcException>()
                .Or<TimeoutRejectedException>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(300 * attempt),
                    onRetry: (ex, ts, count, _) =>
                    {
                        _logger.LogWarning(
                            ex,
                            "AIFaceClient retry {Retry} after {Delay}ms",
                            count,
                            ts.TotalMilliseconds);
                    });

            var timeout = Policy.TimeoutAsync(5);

            var breaker = Policy
                .Handle<RpcException>()
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(20),
                    onBreak: (ex, ts) =>
                    {
                        _logger.LogCritical(
                            ex,
                            "AIFaceClient circuit opened for {Seconds}s",
                            ts.TotalSeconds);
                    },
                    onReset: () =>
                    {
                        _logger.LogInformation("AIFaceClient circuit reset");
                    });

            return Policy.WrapAsync(retry, timeout, breaker);
        }
    }
}
