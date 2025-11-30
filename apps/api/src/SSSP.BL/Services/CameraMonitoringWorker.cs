using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SSSP.BL.Services.Interfaces;
using SSSP.Infrastructure.AI.Grpc.Interfaces;

namespace SSSP.BL.Services
{
    public sealed class CameraMonitoringWorker :
        BackgroundService,
        ICameraMonitoringService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IVideoStreamClient _videoStreamClient;
        private readonly ILogger<CameraMonitoringWorker> _logger;

        private const int MAX_RETRY_ATTEMPTS = 10;
        private static readonly TimeSpan BASE_RETRY_DELAY = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan MAX_RETRY_DELAY = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan STOP_TIMEOUT = TimeSpan.FromSeconds(15);

        private sealed record CameraSession(
            int CameraId,
            string RtspUrl,
            DateTimeOffset StartedAt,
            CancellationTokenSource Cancellation,
            Task WorkerTask,
            int RetryCount
        );

        private readonly ConcurrentDictionary<int, CameraSession> _sessions = new();

        public CameraMonitoringWorker(
            IServiceScopeFactory scopeFactory,
            IVideoStreamClient videoStreamClient,
            ILogger<CameraMonitoringWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _videoStreamClient = videoStreamClient;
            _logger = logger;
        }

        // ============================================================
        // START CAMERA
        // ============================================================
        public Task<bool> StartAsync(
            int cameraId,
            string rtspUrl,
            CancellationToken cancellationToken = default)
        {
            if (_sessions.ContainsKey(cameraId))
            {
                _logger.LogWarning(
                    "Camera {CameraId} start ignored – already running",
                    cameraId);
                return Task.FromResult(false);
            }

            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var startedAt = DateTimeOffset.UtcNow;

            var workerTask = Task.Run(
                () => RunCameraSupervisorLoop(cameraId, rtspUrl, cts.Token),
                CancellationToken.None);

            var session = new CameraSession(
                cameraId,
                rtspUrl,
                startedAt,
                cts,
                workerTask,
                RetryCount: 0);

            if (!_sessions.TryAdd(cameraId, session))
            {
                cts.Cancel();
                _logger.LogError(
                    "Failed to register camera session in dictionary. Camera={CameraId}",
                    cameraId);
                return Task.FromResult(false);
            }

            _logger.LogInformation(
                "Camera {CameraId} monitoring STARTED on {RtspUrl}",
                cameraId,
                rtspUrl);

            return Task.FromResult(true);
        }

        // ============================================================
        // STOP CAMERA
        // ============================================================
        public async Task<bool> StopAsync(
            int cameraId,
            CancellationToken cancellationToken = default)
        {
            if (!_sessions.TryRemove(cameraId, out var session))
            {
                _logger.LogWarning(
                    "Stop ignored for non-running camera {CameraId}",
                    cameraId);
                return false;
            }

            _logger.LogInformation(
                "Stopping camera {CameraId} monitoring",
                cameraId);

            try
            {
                session.Cancellation.Cancel();

                var completed = await Task.WhenAny(
                    session.WorkerTask,
                    Task.Delay(STOP_TIMEOUT, cancellationToken));

                if (completed != session.WorkerTask)
                {
                    _logger.LogWarning(
                        "Camera {CameraId} did not stop in time",
                        cameraId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Fatal error while stopping camera {CameraId}",
                    cameraId);
            }

            return true;
        }

        // ============================================================
        // ACTIVE SESSIONS
        // ============================================================
        public IReadOnlyCollection<CameraMonitoringStatus> GetActiveSessions()
        {
            return _sessions.Values
                .Select(s => new CameraMonitoringStatus(
                    s.CameraId,
                    s.RtspUrl,
                    s.StartedAt,
                    !s.WorkerTask.IsCompleted))
                .ToArray();
        }

        // ============================================================
        // WORKER SUPERVISOR LOOP (AUTO-RETRY ENGINE)
        // ============================================================
        private async Task RunCameraSupervisorLoop(
            int cameraId,
            string rtspUrl,
            CancellationToken cancellationToken)
        {
            var retry = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    retry++;

                    _logger.LogInformation(
                        "Camera {CameraId} CONNECT attempt {Attempt}",
                        cameraId, retry);

                    await RunCameraStreamOnce(
                        cameraId,
                        rtspUrl,
                        cancellationToken);

                    _logger.LogWarning(
                        "Camera {CameraId} stream ended unexpectedly – reconnecting",
                        cameraId);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation(
                        "Camera {CameraId} monitoring cancelled",
                        cameraId);
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Camera {CameraId} stream crashed. Attempt {Attempt}",
                        cameraId,
                        retry);
                }

                if (retry >= MAX_RETRY_ATTEMPTS)
                {
                    _logger.LogCritical(
                        "Camera {CameraId} exceeded max retry attempts ({Max}). DISABLING.",
                        cameraId,
                        MAX_RETRY_ATTEMPTS);

                    _sessions.TryRemove(cameraId, out _);
                    return;
                }

                var delay = ComputeBackoff(retry);

                _logger.LogWarning(
                    "Camera {CameraId} retrying in {DelaySeconds} sec",
                    cameraId,
                    delay.TotalSeconds);

                await Task.Delay(delay, cancellationToken);
            }

            _logger.LogInformation(
                "Camera {CameraId} supervisor loop EXITED",
                cameraId);
        }

        // ============================================================
        // SINGLE STREAM LIFECYCLE
        // ============================================================
        private async Task RunCameraStreamOnce(
            int cameraId,
            string rtspUrl,
            CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var recognitionService =
                scope.ServiceProvider.GetRequiredService<FaceRecognitionService>();

            _logger.LogInformation(
                "Camera {CameraId} stream session STARTED",
                cameraId);

            await _videoStreamClient.StreamCameraAsync(
                cameraId.ToString(),
                rtspUrl,
                async response =>
                {
                    if (response.Faces.Count == 0)
                        return;

                    foreach (var face in response.Faces)
                    {
                        var match =
                            await recognitionService.VerifyEmbeddingAsync(
                                face.Embedding.Vector,
                                response.CameraId,
                                cancellationToken);

                        if (match.IsMatch)
                        {
                            _logger.LogInformation(
                                "MATCH Camera={CameraId} User={UserId} Similarity={Similarity}",
                                response.CameraId,
                                match.UserId,
                                match.Similarity);
                        }
                        else
                        {
                            _logger.LogDebug(
                                "UNKNOWN Camera={CameraId} Similarity={Similarity}",
                                response.CameraId,
                                match.Similarity);
                        }
                    }
                },
                cancellationToken);

            _logger.LogWarning(
                "Camera {CameraId} stream session STOPPED",
                cameraId);
        }

        // ============================================================
        // EXPONENTIAL BACKOFF
        // ============================================================
        private static TimeSpan ComputeBackoff(int attempt)
        {
            var delayMs = Math.Min(
                BASE_RETRY_DELAY.TotalMilliseconds * Math.Pow(2, attempt),
                MAX_RETRY_DELAY.TotalMilliseconds);

            return TimeSpan.FromMilliseconds(delayMs);
        }

        // ============================================================
        // HOST SHUTDOWN HANDLING
        // ============================================================
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("CameraMonitoringWorker HOST started");

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                    await Task.Delay(1000, stoppingToken);
            }
            catch (TaskCanceledException) { }

            _logger.LogInformation(
                "CameraMonitoringWorker shutting down – cancelling {Count} sessions",
                _sessions.Count);

            foreach (var session in _sessions.Values)
                session.Cancellation.Cancel();

            try
            {
                await Task.WhenAll(_sessions.Values.Select(s => s.WorkerTask));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Error awaiting camera workers during shutdown");
            }

            _sessions.Clear();

            _logger.LogInformation("CameraMonitoringWorker SHUTDOWN complete");
        }
    }
}
