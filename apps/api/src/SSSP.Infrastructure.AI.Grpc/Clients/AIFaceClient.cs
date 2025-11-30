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
        private readonly IAsyncPolicy _policy;

        private const string ServiceName = "FaceService";

        public AIFaceClient(
            GrpcChannelFactory channelFactory,
            ILogger<AIFaceClient> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var channel = channelFactory.CreateAiChannel();
            _client = new FaceService.FaceServiceClient(channel);

            _policy = BuildPolicy();

            _logger.LogInformation("{Service} client initialized.", ServiceName);
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

            return await _policy.ExecuteAsync(async ct =>
            {
                try
                {
                    _logger.LogInformation(
                        "{Service} VerifyFace sent. Camera={CameraId} Size={Size}",
                        ServiceName,
                        cameraId,
                        imageBytes.Length);

                    var response = await _client.VerifyFaceAsync(
                        request,
                        cancellationToken: ct);

                    sw.Stop();

                    _logger.LogInformation(
                        "{Service} VerifyFace response. Camera={CameraId} Success={Success} Match={Match} Authorized={Auth} ElapsedMs={Elapsed}",
                        ServiceName,
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
                        "{Service} VerifyFace failed. Camera={CameraId} ElapsedMs={Elapsed}",
                        ServiceName,
                        cameraId,
                        sw.ElapsedMilliseconds);
                    throw;
                }
            }, CancellationToken.None);
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

            return await _policy.ExecuteAsync(async ct =>
            {
                var effectiveCt = CancellationTokenSource
                    .CreateLinkedTokenSource(ct, cancellationToken)
                    .Token;

                try
                {
                    _logger.LogInformation(
                        "{Service} ExtractEmbedding sent. Camera={CameraId} Size={Size}",
                        ServiceName,
                        cameraId ?? "N/A",
                        image.Length);

                    var response = await _client.ExtractEmbeddingAsync(
                        request,
                        cancellationToken: effectiveCt);

                    sw.Stop();

                    _logger.LogInformation(
                        "{Service} ExtractEmbedding response. Camera={CameraId} Success={Success} Dim={Dim} FaceDetected={Detected} ElapsedMs={Elapsed}",
                        ServiceName,
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
                        "{Service} ExtractEmbedding failed. Camera={CameraId} ElapsedMs={Elapsed}",
                        ServiceName,
                        cameraId ?? "N/A",
                        sw.ElapsedMilliseconds);
                    throw;
                }
            }, cancellationToken);
        }

        private static void ValidateImage(byte[] image)
        {
            if (image == null || image.Length == 0)
                throw new ArgumentException("Image is empty", nameof(image));
        }

        private IAsyncPolicy BuildPolicy()
        {
            // 1) Retry on transient gRPC or timeout, every 3 seconds
            AsyncRetryPolicy retry = Policy
                .Handle<RpcException>()
                .Or<TimeoutRejectedException>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: _ => TimeSpan.FromSeconds(3),
                    onRetry: (ex, ts, attempt, _) =>
                    {
                        _logger.LogWarning(
                            ex,
                            "{Service} retry {Attempt} after {Delay}ms",
                            ServiceName,
                            attempt,
                            ts.TotalMilliseconds);
                    });

            // 2) Per-call timeout (hard cap)
            AsyncTimeoutPolicy timeout = Policy.TimeoutAsync(
                TimeSpan.FromSeconds(5),
                TimeoutStrategy.Optimistic);

            // 3) Circuit breaker on repeated RpcExceptions
            AsyncCircuitBreakerPolicy breaker = Policy
                .Handle<RpcException>()
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(20),
                    onBreak: (ex, ts) =>
                    {
                        _logger.LogCritical(
                            ex,
                            "{Service} circuit opened for {Seconds}s",
                            ServiceName,
                            ts.TotalSeconds);
                    },
                    onReset: () =>
                    {
                        _logger.LogInformation(
                            "{Service} circuit reset",
                            ServiceName);
                    });

            return Policy.WrapAsync(retry, timeout, breaker);
        }
    }
}
