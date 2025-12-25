using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Sssp.Ai.Stream;
using SSSP.BL.Records;
using SSSP.BL.Services.Interfaces;
using SSSP.Infrastructure.AI.Grpc.Interfaces;

namespace SSSP.BL.Services
{
    public sealed class CameraMonitoringWorker : BackgroundService, ICameraMonitoringService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IVideoStreamClient _videoStreamClient;
        private readonly ILogger<CameraMonitoringWorker> _logger;
        private readonly TelemetryClient _telemetry;
        private readonly Channel<TelemetryEvent> _telemetryBuffer;
        private readonly CancellationTokenSource _shutdownToken = new();

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
            int RetryCount);

        private readonly ConcurrentDictionary<int, CameraSession> _sessions = new();

        public CameraMonitoringWorker(
            IServiceScopeFactory scopeFactory,
            IVideoStreamClient videoStreamClient,
            ILogger<CameraMonitoringWorker> logger,
            TelemetryClient telemetry)
        {
            _scopeFactory = scopeFactory;
            _videoStreamClient = videoStreamClient;
            _logger = logger;
            _telemetry = telemetry;
            _telemetryBuffer = Channel.CreateBounded<TelemetryEvent>(
                                new BoundedChannelOptions(1000)
                                {
                                    FullMode = BoundedChannelFullMode.DropOldest
                                });
            _ = Task.Run(FlushTelemetryLoop);
        }

        public Task<bool> StartAsync(int cameraId, string rtspUrl, CancellationToken cancellationToken = default)
        {
            if (_sessions.ContainsKey(cameraId))
            {
                _logger.LogWarning("Camera start ignored - already running. CameraId={CameraId}", cameraId);
                return Task.FromResult(false);
            }

            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var startedAt = DateTimeOffset.UtcNow;

            var workerTask = Task.Run(
                () => RunCameraSupervisorLoop(cameraId, rtspUrl, cts.Token),
                CancellationToken.None);

            var session = new CameraSession(cameraId, rtspUrl, startedAt, cts, workerTask, 0);

            if (!_sessions.TryAdd(cameraId, session))
            {
                cts.Cancel();
                _logger.LogError("Failed to register camera session. CameraId={CameraId}", cameraId);
                return Task.FromResult(false);
            }

            _logger.LogInformation("Camera monitoring started. CameraId={CameraId}, RtspUrl={RtspUrl}, StartedAt={StartedAt:o}",
                cameraId, rtspUrl, startedAt);

            return Task.FromResult(true);
        }

        public async Task<bool> StopAsync(int cameraId, CancellationToken cancellationToken = default)
        {
            if (!_sessions.TryRemove(cameraId, out var session))
            {
                _logger.LogWarning("Stop ignored - camera not running. CameraId={CameraId}", cameraId);
                return false;
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            _logger.LogInformation("Stopping camera monitoring. CameraId={CameraId}", cameraId);

            try
            {
                session.Cancellation.Cancel();

                var completed = await Task.WhenAny(
                    session.WorkerTask,
                    Task.Delay(STOP_TIMEOUT, cancellationToken));

                stopwatch.Stop();

                if (completed != session.WorkerTask)
                {
                    _logger.LogWarning("Camera stop timeout exceeded. CameraId={CameraId}, TimeoutMs={TimeoutMs}, ElapsedMs={ElapsedMs}",
                        cameraId, STOP_TIMEOUT.TotalMilliseconds, stopwatch.ElapsedMilliseconds);
                }
                else
                {
                    _logger.LogInformation("Camera monitoring stopped successfully. CameraId={CameraId}, ElapsedMs={ElapsedMs}",
                        cameraId, stopwatch.ElapsedMilliseconds);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error stopping camera. CameraId={CameraId}, ElapsedMs={ElapsedMs}",
                    cameraId, stopwatch.ElapsedMilliseconds);
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

        private async Task RunCameraSupervisorLoop(int cameraId, string rtspUrl, CancellationToken cancellationToken)
        {
            var attempt = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                attempt++;

                try
                {
                    _logger.LogInformation("Camera connection attempt. CameraId={CameraId}, Attempt={Attempt}/{MaxAttempts}",
                        cameraId, attempt, MAX_RETRY_ATTEMPTS);

                    await RunCameraStreamOnce(cameraId, rtspUrl, cancellationToken);

                    _logger.LogWarning("Camera stream ended unexpectedly. CameraId={CameraId}, Attempt={Attempt}",
                        cameraId, attempt);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Camera monitoring cancelled. CameraId={CameraId}, TotalAttempts={TotalAttempts}",
                        cameraId, attempt);
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Camera stream crashed. CameraId={CameraId}, Attempt={Attempt}/{MaxAttempts}, ExceptionType={ExceptionType}",
                        cameraId, attempt, MAX_RETRY_ATTEMPTS, ex.GetType().Name);
                }

                if (attempt >= MAX_RETRY_ATTEMPTS)
                {
                    _logger.LogCritical("Camera exceeded max retry attempts - DISABLING. CameraId={CameraId}, MaxAttempts={MaxAttempts}, RtspUrl={RtspUrl}",
                        cameraId, MAX_RETRY_ATTEMPTS, rtspUrl);

                    _sessions.TryRemove(cameraId, out _);
                    return;
                }

                var delay = ComputeBackoff(attempt);
                _logger.LogInformation("Camera retry scheduled. CameraId={CameraId}, Attempt={Attempt}, DelaySeconds={DelaySeconds}",
                    cameraId, attempt, delay.TotalSeconds);

                await Task.Delay(delay, cancellationToken);
            }

            _logger.LogInformation("Camera supervisor loop exited. CameraId={CameraId}, TotalAttempts={TotalAttempts}",
                cameraId, attempt);
        }

        private async Task RunCameraStreamOnce(int cameraId, string rtspUrl, CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var recognitionService = scope.ServiceProvider.GetRequiredService<IFaceRecognitionService>();
            var sessionStart = DateTimeOffset.UtcNow;

            _logger.LogInformation("Camera stream session started. CameraId={CameraId}, RtspUrl={RtspUrl}, SessionStart={SessionStart:o}",
                cameraId, rtspUrl, sessionStart);

            var frameCount = 0;
            var faceCount = 0;
            var matchCount = 0;
            var totalAiProcessingMs = 0.0;
            var avgAiProcessingMs = 0.0;

            // Window counters for FPS / rolling metrics
            var windowStart = DateTimeOffset.UtcNow;
            var windowFrames = 0;
            var windowFaces = 0;
            var windowMatches = 0;

            await _videoStreamClient.StreamCameraAsync(
                cameraId.ToString(),
                rtspUrl,
                async response =>
                {
                    frameCount++;
                    windowFrames++;
                    if (response.ProcessingTimeMs > 0)
                    {
                        totalAiProcessingMs += response.ProcessingTimeMs;
                    }
                    if (response.Faces.Count == 0)
                    {
                        // Heartbeat log every 100 frames
                        if (frameCount % 100 == 0)
                        {
                            _logger.LogDebug(
                                "Camera heartbeat. CameraId={CameraId}, ProcessedFrames={FrameCount}, DetectedFaces={FaceCount}, Matches={MatchCount}",
                                cameraId, frameCount, faceCount, matchCount);

                            TrackCameraHeartbeat(cameraId);
                        }

                        TryEmitWindowMetricsIfNeeded(cameraId, ref windowStart, ref windowFrames, ref windowFaces, ref windowMatches);
                        return;
                    }

                    faceCount += response.Faces.Count;
                    windowFaces += response.Faces.Count;

                    _logger.LogDebug(
                        "Frame received with faces. CameraId={CameraId}, FrameId={FrameId}, Faces={FaceCount}, TotalFrames={TotalFrames}",
                        cameraId, response.FrameId, response.Faces.Count, frameCount);
                    
                    var embeddings = response.Faces
                                             .Select(f => (IReadOnlyList<float>)f.Embedding.Vector.ToArray())
                                             .ToList();
                    var matches = await recognitionService.VerifyEmbeddingsBatchAsync(
                                                            embeddings,
                                                            response.CameraId,
                                                            cancellationToken);
                    for (int i = 0; i < matches.Count; i++)
                    {
                        var match = matches[i];
                        var face = response.Faces[i];

                        if (match.IsMatch)
                        {
                            matchCount++;
                            windowMatches++;

                            _logger.LogInformation(
                                "FACE MATCH. CameraId={CameraId}, FrameId={FrameId}, UserId={UserId}, FaceProfileId={FaceProfileId}, Similarity={Similarity:F4}, MatchNumber={MatchNumber}",
                                cameraId, response.FrameId, match.UserId, match.FaceProfileId, match.Similarity, matchCount);
                        }
                        else
                        {
                            _logger.LogDebug(
                                "Unknown face detected. CameraId={CameraId}, FrameId={FrameId}, BestSimilarity={Similarity:F4}",
                                cameraId, response.FrameId, match.Similarity);
                        }
                    }
                    avgAiProcessingMs = frameCount > 0 ? totalAiProcessingMs / frameCount : 0.0;
                    LogFaceQualityMetrics(cameraId, response.Faces,avgAiProcessingMs);
                    TryEmitWindowMetricsIfNeeded(cameraId, ref windowStart, ref windowFrames, ref windowFaces, ref windowMatches);
                },
                cancellationToken);

            var sessionDuration = DateTimeOffset.UtcNow - sessionStart;

            // Flush remaining window if any frames left
            EmitWindowMetrics(cameraId, windowStart, windowFrames, windowFaces, windowMatches);

            // Session-level telemetry
            TrackCameraSessionSummary(cameraId, sessionDuration, frameCount, faceCount, matchCount);

            _logger.LogWarning(
                "Camera stream session ended. CameraId={CameraId}, Duration={DurationSeconds}s, ProcessedFrames={FrameCount}, DetectedFaces={FaceCount}, Matches={MatchCount}, AvgAiMs={AvgAiMs:F2}",
                cameraId, sessionDuration.TotalSeconds, frameCount, faceCount, matchCount, avgAiProcessingMs);
        }

        private void LogFaceQualityMetrics(
            int cameraId,
            IReadOnlyList<FaceResult> faces,
            double avgAiProcessingMs)
        {
            if (faces.Count == 0)
                return;

            var avgQuality = faces.Average(f => f.Quality?.OverallScore ?? 0f);
            var avgConfidence = faces.Average(f => f.Confidence);
            var avgSharpness = faces.Average(f => f.Quality?.Sharpness ?? 0f);
            var avgBrightness = faces.Average(f => f.Quality?.Brightness ?? 0f);

            var props = new Dictionary<string, string>
            {
                ["CameraId"] = cameraId.ToString()
            };

            _telemetry.TrackMetric("FaceAvgQuality", avgQuality, props);
            _telemetry.TrackMetric("FaceAvgConfidence", avgConfidence, props);
            _telemetry.TrackMetric("FaceAvgSharpness", avgSharpness, props);
            _telemetry.TrackMetric("FaceAvgBrightness", avgBrightness, props);
            _telemetry.TrackMetric("CameraSessionAvgAiProcessingMs", avgAiProcessingMs, props);
        }
        private static TimeSpan ComputeBackoff(int attempt)
        {
            var delayMs = Math.Min(
                BASE_RETRY_DELAY.TotalMilliseconds * Math.Pow(2, attempt),
                MAX_RETRY_DELAY.TotalMilliseconds);

            return TimeSpan.FromMilliseconds(delayMs);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("CameraMonitoringWorker host started. Environment={Environment}",
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production");

            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (TaskCanceledException)
            {
            }

            var activeSessions = _sessions.Count;
            _logger.LogInformation("CameraMonitoringWorker shutdown initiated. ActiveSessions={ActiveSessions}", activeSessions);

            foreach (var session in _sessions.Values)
                session.Cancellation.Cancel();

            try
            {
                await Task.WhenAll(_sessions.Values.Select(s => s.WorkerTask));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error awaiting camera workers during shutdown. SessionCount={SessionCount}", activeSessions);
            }

            _sessions.Clear();
            _logger.LogInformation("CameraMonitoringWorker shutdown complete");
        }

        private void TrackCameraHeartbeat(int cameraId)
        {
            var props = new Dictionary<string, string>
            {
                ["CameraId"] = cameraId.ToString()
            };

            _telemetry.TrackMetric("CameraHeartbeat", 1, props);
        }

        private void TryEmitWindowMetricsIfNeeded(
            int cameraId,
            ref DateTimeOffset windowStart,
            ref int windowFrames,
            ref int windowFaces,
            ref int windowMatches)
        {
            var now = DateTimeOffset.UtcNow;
            var windowDuration = now - windowStart;

            // Emit metrics every 5 seconds OR every 100 frames
            if (windowFrames == 0)
                return;

            if (windowDuration.TotalSeconds < 5 && windowFrames < 100)
                return;

            EmitWindowMetrics(cameraId, windowStart, windowFrames, windowFaces, windowMatches);

            // reset window
            windowStart = now;
            windowFrames = 0;
            windowFaces = 0;
            windowMatches = 0;
        }

        private void EmitWindowMetrics(
            int cameraId,
            DateTimeOffset windowStart,
            int windowFrames,
            int windowFaces,
            int windowMatches)
        {
            if (windowFrames <= 0)
                return;

            var windowDuration = DateTimeOffset.UtcNow - windowStart;
            var seconds = windowDuration.TotalSeconds > 0 ? windowDuration.TotalSeconds : 1;
            var fps = windowFrames / seconds;

            var props = new Dictionary<string, string>
            {
                ["CameraId"] = cameraId.ToString()
            };

            _telemetry.TrackMetric("CameraFramesProcessed", windowFrames, props);

            if (windowFaces > 0)
                _telemetry.TrackMetric("CameraFacesDetected", windowFaces, props);

            if (windowMatches > 0)
                _telemetry.TrackMetric("CameraFaceMatches", windowMatches, props);

            _telemetry.TrackMetric("CameraFps", fps, props);

            _logger.LogDebug(
                "Camera metrics window. CameraId={CameraId}, WindowSeconds={WindowSeconds:F1}, Frames={Frames}, Faces={Faces}, Matches={Matches}, Fps={Fps:F2}",
                cameraId, seconds, windowFrames, windowFaces, windowMatches, fps);
        }

        private void TrackCameraSessionSummary(
            int cameraId,
            TimeSpan duration,
            int totalFrames,
            int totalFaces,
            int totalMatches)
        {
            var props = new Dictionary<string, string>
            {
                ["CameraId"] = cameraId.ToString(),
                ["SessionDurationSeconds"] = duration.TotalSeconds.ToString("F2")
            };

            _telemetry.TrackMetric("CameraSessionDurationSeconds", duration.TotalSeconds, props);
            _telemetry.TrackMetric("CameraSessionFrames", totalFrames, props);
            _telemetry.TrackMetric("CameraSessionFaces", totalFaces, props);
            _telemetry.TrackMetric("CameraSessionMatches", totalMatches, props);

            if (duration.TotalSeconds > 0 && totalFrames > 0)
            {
                var avgFps = totalFrames / duration.TotalSeconds;
                _telemetry.TrackMetric("CameraSessionAvgFps", avgFps, props);
            }

            _telemetry.TrackEvent("CameraSessionEnded", new Dictionary<string, string>
            {
                ["CameraId"] = cameraId.ToString(),
                ["DurationSeconds"] = duration.TotalSeconds.ToString("F2"),
                ["Frames"] = totalFrames.ToString(),
                ["Faces"] = totalFaces.ToString(),
                ["Matches"] = totalMatches.ToString()
            });
        }

        private async Task FlushTelemetryLoop()
        {
            var buffer = new List<TelemetryEvent>(100);
            var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));

            try
            {
                while (await timer.WaitForNextTickAsync(_shutdownToken.Token))
                {
                    // Drain buffer
                    while (_telemetryBuffer.Reader.TryRead(out var evt))
                    {
                        buffer.Add(evt);
                        if (buffer.Count >= 100) break;
                    }

                    if (buffer.Count == 0) continue;

                    // Batch send
                    foreach (var evt in buffer)
                    {
                        foreach (var metric in evt.Metrics)
                        {
                            _telemetry.TrackMetric(evt.Name, metric.Value, evt.Properties);
                        }
                    }

                    _logger.LogDebug("Flushed {Count} telemetry events", buffer.Count);
                    buffer.Clear();
                }
            }
            catch (OperationCanceledException)
            {
                // Final flush
                while (_telemetryBuffer.Reader.TryRead(out var evt))
                {
                    buffer.Add(evt);
                }

                foreach (var evt in buffer)
                {
                    foreach (var metric in evt.Metrics)
                    {
                        _telemetry.TrackMetric(evt.Name, metric.Value, evt.Properties);
                    }
                }
            }
        }

    }
}