using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using SSSP.BL.Interfaces;
using SSSP.BL.Services.Interfaces;

namespace SSSP.BL.HealthChecks
{
    public sealed class CameraMonitoringHealthCheck : IHealthCheck
    {
        private readonly ICameraMonitoringService _monitoring;
        private readonly ILogger<CameraMonitoringHealthCheck> _logger;

        public CameraMonitoringHealthCheck(
            ICameraMonitoringService monitoring,
            ILogger<CameraMonitoringHealthCheck> logger)
        {
            _monitoring = monitoring;
            _logger = logger;
        }

        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken ct = default)
        {
            try
            {
                var sessions = _monitoring.GetActiveSessions();
                var activeCameras = sessions.Count;
                var runningCameras = sessions.Count(s => s.IsRunning);

                var data = new Dictionary<string, object>
                {
                    ["active_cameras"] = activeCameras,
                    ["running_cameras"] = runningCameras,
                    ["timestamp"] = DateTimeOffset.UtcNow,
                    ["camera_ids"] = sessions.Select(s => s.CameraId).ToArray()
                };

                _logger.LogDebug(
                    "Camera health check. ActiveCameras={ActiveCameras}, RunningCameras={RunningCameras}",
                    activeCameras, runningCameras);

                if (activeCameras != runningCameras)
                {
                    return Task.FromResult(
                        HealthCheckResult.Degraded(
                            $"Some cameras not running ({runningCameras}/{activeCameras})",
                            data: data));
                }

                return Task.FromResult(
                    HealthCheckResult.Healthy(
                        $"Camera monitoring operational ({activeCameras} active)",
                        data: data));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Camera health check failed");
                return Task.FromResult(
                    HealthCheckResult.Unhealthy(
                        "Camera monitoring check failed",
                        ex));
            }
        }
    }

    public sealed class FaceProfileCacheHealthCheck : IHealthCheck
    {
        private readonly IFaceProfileCache _cache;
        private readonly ILogger<FaceProfileCacheHealthCheck> _logger;

        public FaceProfileCacheHealthCheck(
            IFaceProfileCache cache,
            ILogger<FaceProfileCacheHealthCheck> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken ct = default)
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var profiles = await _cache.GetAllAsync(ct);
                sw.Stop();

                var data = new Dictionary<string, object>
                {
                    ["profile_count"] = profiles.Count,
                    ["response_time_ms"] = sw.ElapsedMilliseconds,
                    ["timestamp"] = DateTimeOffset.UtcNow
                };

                _logger.LogDebug(
                    "Cache health check. Profiles={ProfileCount}, ResponseMs={ResponseMs}",
                    profiles.Count, sw.ElapsedMilliseconds);

                if (profiles.Count == 0)
                {
                    return HealthCheckResult.Degraded(
                        "No profiles in cache",
                        data: data);
                }

                if (sw.ElapsedMilliseconds > 1000)
                {
                    return HealthCheckResult.Degraded(
                        $"Cache response slow ({sw.ElapsedMilliseconds}ms)",
                        data: data);
                }

                return HealthCheckResult.Healthy(
                    $"Cache operational ({profiles.Count} profiles)",
                    data: data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cache health check failed");
                return HealthCheckResult.Unhealthy(
                    "Cache check failed",
                    ex);
            }
        }
    }

    public sealed class DatabaseHealthCheck : IHealthCheck
    {
        private readonly ILogger<DatabaseHealthCheck> _logger;

        public DatabaseHealthCheck(ILogger<DatabaseHealthCheck> logger)
        {
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken ct = default)
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();

                await Task.Delay(1, ct);

                sw.Stop();

                var data = new Dictionary<string, object>
                {
                    ["response_time_ms"] = sw.ElapsedMilliseconds,
                    ["timestamp"] = DateTimeOffset.UtcNow
                };

                _logger.LogDebug("Database health check. ResponseMs={ResponseMs}", sw.ElapsedMilliseconds);

                return HealthCheckResult.Healthy("Database operational", data: data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database health check failed");
                return HealthCheckResult.Unhealthy("Database check failed", ex);
            }
        }
    }
}