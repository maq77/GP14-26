using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using SSSP.Infrastructure.AI.Grpc.Interfaces;
using Sssp.Ai.Face;
using SSSP.Telemetry.Abstractions.Faces;

namespace SSSP.Infrastructure.AI.Grpc.Clients
{
    /// <summary>
    /// Production-grade gRPC client wrapper:
    /// - Retries only transient gRPC failures (no retry on Cancelled/BadRequest/etc.)
    /// - Uses gRPC deadlines per call (server-aware timeout)
    /// - Uses Polly timeout as a client-side guard
    /// - Circuit breaker only on transient
    /// - Correct cancellation propagation
    /// - Structured logs + metrics
    /// </summary>
    public sealed class AIFaceClient : IAIFaceClient
    {
        private const string ServiceName = "FaceService";

        // Call deadlines (tune)
        private static readonly TimeSpan VerifyDeadline = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan EmbedDeadline = TimeSpan.FromSeconds(5);

        // Polly timeout should be slightly ABOVE the gRPC deadline (guard, not primary timeout)
        private static readonly TimeSpan PollyTimeout = TimeSpan.FromSeconds(6);

        private readonly ILogger<AIFaceClient> _logger;
        private readonly FaceService.FaceServiceClient _client;
        private readonly IAsyncPolicy _policy;
        private readonly IFaceMetrics _metrics;

        public AIFaceClient(
            GrpcChannelFactory channelFactory,
            IFaceMetrics metrics,
            ILogger<AIFaceClient> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            if (channelFactory is null) throw new ArgumentNullException(nameof(channelFactory));

            var channel = channelFactory.CreateAiChannel();
            _client = new FaceService.FaceServiceClient(channel);

            _policy = BuildPolicy();

            _logger.LogInformation("{Service} client initialized.", ServiceName);
        }

        // If your IAIFaceClient interface doesn't have CT, keep this overload and call the CT version with default.
        public Task<FaceVerifyResponse> VerifyFaceAsync(byte[] imageBytes, string cameraId)
            => VerifyFaceAsync(imageBytes, cameraId, CancellationToken.None);

        // Recommended: propagate CT from controller/service
        public async Task<FaceVerifyResponse> VerifyFaceAsync(byte[] imageBytes, string cameraId, CancellationToken cancellationToken)
        {
            ValidateImage(imageBytes);

            var request = new FaceVerifyRequest
            {
                Image = ByteString.CopyFrom(imageBytes),
                CameraId = cameraId ?? string.Empty
            };

            return await _policy.ExecuteAsync(async policyCt =>
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(policyCt, cancellationToken);
                var ct = linked.Token;

                var sw = Stopwatch.StartNew();

                try
                {
                    _logger.LogInformation(
                        "{Service} VerifyFace sent. Camera={CameraId} Size={Size}",
                        ServiceName,
                        cameraId,
                        imageBytes.Length);

                    var options = new CallOptions(
                        deadline: DateTime.UtcNow.Add(VerifyDeadline),
                        cancellationToken: ct);

                    var response = await _client.VerifyFaceAsync(request, options);

                    sw.Stop();

                    var m = response.Metrics;

                    _logger.LogInformation(
                        "{Service} VerifyFace response. Camera={CameraId} Success={Success} FaceDetected={Detected} Faces={Faces} ElapsedMs={Elapsed} " +
                        "DetectionMs={DetectionMs} EmbeddingMs={EmbeddingMs} PreMs={PreMs} TotalMs={TotalMs}",
                        ServiceName,
                        cameraId,
                        response.Success,
                        response.FaceDetected,
                        response.Faces.Count,
                        sw.Elapsed.TotalMilliseconds,
                        m?.DetectionMs ?? 0f,
                        m?.EmbeddingMs ?? 0f,
                        m?.PreprocessingMs ?? 0f,
                        m?.TotalMs ?? 0f);

                    // If later you want a dedicated Verify metric, add a method to IFaceMetrics
                    _metrics.ObserveAiExtractDuration(
                        sw.Elapsed.TotalMilliseconds,
                        success: response.Success ? "true" : "false",
                        faceDetected: response.FaceDetected ? "true" : "false",
                        faces: response.Faces.Count);

                    return response;
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled && ct.IsCancellationRequested)
                {
                    sw.Stop();
                    _logger.LogWarning(
                        "{Service} VerifyFace cancelled by caller. Camera={CameraId} ElapsedMs={Elapsed}",
                        ServiceName,
                        cameraId,
                        sw.Elapsed.TotalMilliseconds);

                    throw;
                }
                catch (RpcException ex) when (!IsTransient(ex))
                {
                    sw.Stop();
                    _logger.LogError(
                        ex,
                        "{Service} VerifyFace non-transient failure. Camera={CameraId} Status={Status} ElapsedMs={Elapsed}",
                        ServiceName,
                        cameraId,
                        ex.StatusCode,
                        sw.Elapsed.TotalMilliseconds);

                    _metrics.ObserveAiExtractDuration(sw.Elapsed.TotalMilliseconds, "false", "false", 0);
                    throw;
                }
                catch (Exception ex)
                {
                    sw.Stop();

                    _logger.LogError(
                        ex,
                        "{Service} VerifyFace failed. Camera={CameraId} ElapsedMs={Elapsed}",
                        ServiceName,
                        cameraId,
                        sw.Elapsed.TotalMilliseconds);

                    _metrics.ObserveAiExtractDuration(sw.Elapsed.TotalMilliseconds, "false", "false", 0);
                    throw;
                }
            }, cancellationToken);
        }

        public async Task<FaceEmbeddingResponse> ExtractEmbeddingAsync(
            byte[] image,
            string? cameraId = null,
            CancellationToken cancellationToken = default)
        {
            ValidateImage(image);

            var request = new FaceEmbeddingRequest
            {
                Image = ByteString.CopyFrom(image),
                CameraId = cameraId ?? string.Empty
            };

            return await _policy.ExecuteAsync(async policyCt =>
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(policyCt, cancellationToken);
                var ct = linked.Token;

                var sw = Stopwatch.StartNew();

                try
                {
                    _logger.LogInformation(
                        "{Service} ExtractEmbedding sent. Camera={CameraId} Size={Size}",
                        ServiceName,
                        cameraId ?? "N/A",
                        image.Length);

                    var options = new CallOptions(
                        deadline: DateTime.UtcNow.Add(EmbedDeadline),
                        cancellationToken: ct);

                    var response = await _client.ExtractEmbeddingAsync(request, options);

                    sw.Stop();

                    var m = response.Metrics;

                    _logger.LogInformation(
                        "{Service} ExtractEmbedding response. Camera={CameraId} Success={Success} FaceDetected={Detected} Faces={Faces} ErrorCode={ErrorCode} ElapsedMs={Elapsed} " +
                        "DetectionMs={DetectionMs} EmbeddingMs={EmbeddingMs} PreMs={PreMs} TotalMs={TotalMs}",
                        ServiceName,
                        cameraId ?? "N/A",
                        response.Success,
                        response.FaceDetected,
                        response.Faces.Count,
                        response.ErrorCode,
                        sw.Elapsed.TotalMilliseconds,
                        m?.DetectionMs ?? 0f,
                        m?.EmbeddingMs ?? 0f,
                        m?.PreprocessingMs ?? 0f,
                        m?.TotalMs ?? 0f);

                    _metrics.ObserveAiExtractDuration(
                        sw.Elapsed.TotalMilliseconds,
                        success: response.Success ? "true" : "false",
                        faceDetected: response.FaceDetected ? "true" : "false",
                        faces: response.Faces.Count);

                    return response;
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled && ct.IsCancellationRequested)
                {
                    sw.Stop();
                    _logger.LogWarning(
                        "{Service} ExtractEmbedding cancelled by caller. Camera={CameraId} ElapsedMs={Elapsed}",
                        ServiceName,
                        cameraId ?? "N/A",
                        sw.Elapsed.TotalMilliseconds);

                    throw;
                }
                catch (RpcException ex) when (!IsTransient(ex))
                {
                    sw.Stop();
                    _logger.LogError(
                        ex,
                        "{Service} ExtractEmbedding non-transient failure. Camera={CameraId} Status={Status} ElapsedMs={Elapsed}",
                        ServiceName,
                        cameraId ?? "N/A",
                        ex.StatusCode,
                        sw.Elapsed.TotalMilliseconds);

                    _metrics.ObserveAiExtractDuration(sw.Elapsed.TotalMilliseconds, "false", "false", 0);
                    throw;
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    _logger.LogError(
                        ex,
                        "{Service} ExtractEmbedding failed. Camera={CameraId} ElapsedMs={Elapsed}",
                        ServiceName,
                        cameraId ?? "N/A",
                        sw.Elapsed.TotalMilliseconds);

                    _metrics.ObserveAiExtractDuration(sw.Elapsed.TotalMilliseconds, "false", "false", 0);
                    throw;
                }
            }, cancellationToken);
        }

        private static void ValidateImage(byte[] image)
        {
            if (image is null || image.Length == 0)
                throw new ArgumentException("Image is empty", nameof(image));
        }

        private static bool IsTransient(RpcException ex)
            => ex.StatusCode == StatusCode.Unavailable
            || ex.StatusCode == StatusCode.DeadlineExceeded
            || ex.StatusCode == StatusCode.ResourceExhausted
            || ex.StatusCode == StatusCode.Internal;

        private IAsyncPolicy BuildPolicy()
        {
            // Retry only transient gRPC errors (NOT Cancelled, NOT InvalidArgument, etc.)
            AsyncRetryPolicy retry = Policy
                .Handle<RpcException>(IsTransient)
                .Or<TimeoutRejectedException>()
                .WaitAndRetryAsync(
                    retryCount: 2,
                    sleepDurationProvider: attempt =>
                    {
                        // exponential backoff + jitter
                        var baseDelayMs = (int)(150 * Math.Pow(2, attempt - 1)); // 150ms, 300ms
                        var jitterMs = Random.Shared.Next(0, 150);
                        return TimeSpan.FromMilliseconds(baseDelayMs + jitterMs);
                    },
                    onRetry: (ex, delay, attempt, _) =>
                    {
                        var reason = ex is RpcException rx ? rx.StatusCode.ToString() : ex.GetType().Name;
                        _logger.LogWarning(
                            ex,
                            "{Service} retry {Attempt} after {Delay}ms. Reason={Reason}",
                            ServiceName,
                            attempt,
                            delay.TotalMilliseconds,
                            reason);
                    });

            // Client-side timeout guard slightly above the gRPC deadline
            AsyncTimeoutPolicy timeout = Policy.TimeoutAsync(
                PollyTimeout,
                TimeoutStrategy.Optimistic,
                onTimeoutAsync: (_, ts, _, ex) =>
                {
                    _logger.LogWarning(
                        ex,
                        "{Service} timeout guard fired after {Timeout}s",
                        ServiceName,
                        ts.TotalSeconds);
                    return Task.CompletedTask;
                });

            // Circuit breaker only for transient failures (avoid opening on Cancelled)
            AsyncCircuitBreakerPolicy breaker = Policy
                .Handle<RpcException>(IsTransient)
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(15),
                    onBreak: (ex, breakDelay) =>
                    {
                        var reason = ex is RpcException rx ? rx.StatusCode.ToString() : ex.GetType().Name;
                        _logger.LogError(
                            ex,
                            "{Service} circuit opened for {Seconds}s. Reason={Reason}",
                            ServiceName,
                            breakDelay.TotalSeconds,
                            reason);
                    },
                    onReset: () => _logger.LogInformation("{Service} circuit reset", ServiceName),
                    onHalfOpen: () => _logger.LogWarning("{Service} circuit half-open", ServiceName));

            // Order: breaker (fast-fail) -> retry -> timeout guard
            return Policy.WrapAsync(breaker, retry, timeout);
        }
    }
}
