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

        private sealed record CameraSession(
            int CameraId,
            string RtspUrl,
            DateTimeOffset StartedAt,
            CancellationTokenSource Cancellation,
            Task WorkerTask
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

        public async Task<bool> StartAsync(
            int cameraId,
            string rtspUrl,
            CancellationToken cancellationToken = default)
        {
            if (_sessions.ContainsKey(cameraId))
            {
                _logger.LogWarning(
                    "Camera monitoring already running for camera {CameraId}",
                    cameraId);
                return false;
            }

            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var startedAt = DateTimeOffset.UtcNow;

            var workerTask = Task.Run(
                () => RunCameraAsync(cameraId, rtspUrl, cts.Token),
                CancellationToken.None);

            var session = new CameraSession(
                cameraId,
                rtspUrl,
                startedAt,
                cts,
                workerTask);

            if (!_sessions.TryAdd(cameraId, session))
            {
                _logger.LogWarning(
                    "Failed to register camera monitoring session for camera {CameraId}",
                    cameraId);
                cts.Cancel();
                return false;
            }

            _logger.LogInformation(
                "Camera monitoring started for camera {CameraId} on {RtspUrl}",
                cameraId,
                rtspUrl);

            await Task.CompletedTask;
            return true;
        }

        public async Task<bool> StopAsync(
            int cameraId,
            CancellationToken cancellationToken = default)
        {
            if (!_sessions.TryRemove(cameraId, out var session))
            {
                _logger.LogWarning(
                    "Stop requested for non-active camera {CameraId}",
                    cameraId);
                return false;
            }

            _logger.LogInformation(
                "Stopping camera monitoring for camera {CameraId}",
                cameraId);

            try
            {
                session.Cancellation.Cancel();
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var task = session.WorkerTask;

                var completed = await Task.WhenAny(
                    task,
                    Task.Delay(TimeSpan.FromSeconds(10), linkedCts.Token));

                if (completed != task)
                {
                    _logger.LogWarning(
                        "Camera monitoring worker did not stop in time for camera {CameraId}",
                        cameraId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error while stopping camera monitoring for camera {CameraId}",
                    cameraId);
            }

            return true;
        }

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

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("CameraMonitoringWorker started");

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    foreach (var kv in _sessions.Values.ToArray())
                    {
                        if (!kv.WorkerTask.IsCompleted)
                            continue;

                        _sessions.TryRemove(kv.CameraId, out _);

                        if (kv.WorkerTask.IsFaulted && kv.WorkerTask.Exception != null)
                        {
                            _logger.LogError(
                                kv.WorkerTask.Exception,
                                "Camera monitoring task faulted for camera {CameraId}",
                                kv.CameraId);
                        }
                        else
                        {
                            _logger.LogInformation(
                                "Camera monitoring task completed for camera {CameraId}",
                                kv.CameraId);
                        }
                    }

                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
            catch (TaskCanceledException)
            {
            }
            finally
            {
                _logger.LogInformation("CameraMonitoringWorker stopping. Cancelling {Count} sessions", _sessions.Count);

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
                        "Error while awaiting camera monitoring tasks during shutdown");
                }

                _sessions.Clear();
                _logger.LogInformation("CameraMonitoringWorker stopped");
            }
        }

        private async Task RunCameraAsync(
            int cameraId,
            string rtspUrl,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Camera monitoring worker started for camera {CameraId} on {RtspUrl}",
                cameraId,
                rtspUrl);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var recognitionService = scope.ServiceProvider.GetRequiredService<FaceRecognitionService>();

                await _videoStreamClient.StreamCameraAsync(
                    cameraId.ToString(),
                    rtspUrl,
                    async response =>
                    {
                        if (response.Faces.Count == 0)
                            return;

                        _logger.LogDebug(
                            "AI frame received. Camera={CameraId} Frame={FrameId} Faces={Count}",
                            response.CameraId,
                            response.FrameId,
                            response.Faces.Count);

                        foreach (var face in response.Faces)
                        {
                            var match = await recognitionService.VerifyEmbeddingAsync(
                                face.Embedding.Vector,
                                response.CameraId,
                                cancellationToken);

                            if (match.IsMatch)
                            {
                                _logger.LogInformation(
                                    "Camera {CameraId} recognized user {UserId} FaceProfile={FaceProfileId} Similarity={Similarity}",
                                    response.CameraId,
                                    match.UserId,
                                    match.FaceProfileId,
                                    match.Similarity);
                            }
                            else
                            {
                                _logger.LogWarning(
                                    "Camera {CameraId} unknown face in frame {FrameId}. BestSimilarity={Similarity}",
                                    response.CameraId,
                                    response.FrameId,
                                    match.Similarity);
                            }
                        }
                    },
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "Camera monitoring cancelled for camera {CameraId}",
                    cameraId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Camera monitoring loop failed for camera {CameraId}",
                    cameraId);
            }

            _logger.LogInformation(
                "Camera monitoring worker finished for camera {CameraId}",
                cameraId);
        }
    }
}
