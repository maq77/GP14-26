using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SSSP.BL.Options;
using SSSP.Telemetry.Abstractions.Faces;

namespace SSSP.BL.Services
{
    public sealed class FaceProfileCacheRefresherWorker : BackgroundService
    {
        private readonly FaceProfileCacheStore _store;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IFaceProfileDistributedSnapshotCache _redis;
        private readonly FaceProfileCacheOptions _opts;
        private readonly ILogger<FaceProfileCacheRefresherWorker> _logger;
        private readonly IFaceMetrics _metrics;

        private readonly Random _rng = new();

        public FaceProfileCacheRefresherWorker(
            FaceProfileCacheStore store,
            IServiceScopeFactory scopeFactory,
            IFaceProfileDistributedSnapshotCache redis,
            IOptions<FaceProfileCacheOptions> options,
            IFaceMetrics metrics,
            ILogger<FaceProfileCacheRefresherWorker> logger)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _opts = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await RefreshAsync(startup: true, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = WithJitter(_opts.RefreshInterval);

                try
                {
                    for (var i = 0; i < 10; i++)
                    {
                        if (stoppingToken.IsCancellationRequested) return;
                        if (_store.ConsumeRefreshRequested()) break;

                        await Task.Delay(TimeSpan.FromMilliseconds(delay.TotalMilliseconds / 10), stoppingToken);
                    }

                    await RefreshAsync(startup: false, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "FaceProfile cache refresher loop error.");
                }
                finally
                {
                    _metrics.SetCacheAgeSeconds(_store.Age.TotalSeconds);
                }
            }
        }

        private TimeSpan WithJitter(TimeSpan baseInterval)
        {
            var pct = Math.Clamp(_opts.JitterPercent, 0, 0.5);
            var delta = baseInterval.TotalMilliseconds * pct;
            var jitter = (_rng.NextDouble() * 2 - 1) * delta;
            var ms = Math.Max(250, baseInterval.TotalMilliseconds + jitter);
            return TimeSpan.FromMilliseconds(ms);
        }

        private async Task RefreshAsync(bool startup, CancellationToken ct)
        {
            if (_store.IsRefreshing) return;

            _store.MarkRefreshing(true);
            var sw = Stopwatch.StartNew();

            try
            {
                // 1) Startup warm from Redis
                if (startup && _opts.PreferRedisOnStartup)
                {
                    var (found, ver, profiles) = await _redis.TryGetAsync(ct);
                    if (found)
                    {
                        _store.UpdateSuccess(profiles, 0, ver);
                        _metrics.IncrementCacheRefresh("startup_redis", "success");
                        _metrics.ObserveCacheRefreshDuration("startup_redis", 0);

                        _logger.LogInformation("FaceProfile cache warm-started from Redis. Count={Count}, Version={Version}", profiles.Count, ver);
                        return;
                    }
                }

                // 2) Distributed lock
                var (acquired, token) = await _redis.TryAcquireRefreshLockAsync(ct);

                if (!acquired)
                {
                    var (found, ver, profiles) = await _redis.TryGetAsync(ct);
                    if (found)
                    {
                        _store.UpdateSuccess(profiles, sw.ElapsedMilliseconds, ver);
                        _metrics.IncrementCacheRefresh("redis", "success");
                        _metrics.ObserveCacheRefreshDuration("redis", sw.ElapsedMilliseconds);
                        return;
                    }

                    // small backoff to avoid storm
                    await Task.Delay(_opts.NonLeaderEmptyRedisBackoff, ct);

                    (found, ver, profiles) = await _redis.TryGetAsync(ct);
                    if (found)
                    {
                        _store.UpdateSuccess(profiles, sw.ElapsedMilliseconds, ver);
                        _metrics.IncrementCacheRefresh("redis_backoff", "success");
                        _metrics.ObserveCacheRefreshDuration("redis_backoff", sw.ElapsedMilliseconds);
                        return;
                    }

                    var tooStale = _store.Age > _opts.MaxStaleness;

                    if (_opts.AllowEmergencyDbRefreshIfStale && tooStale)
                    {
                        _logger.LogWarning("Emergency DB refresh (non-leader) because snapshot too stale. Age={Age}", _store.Age);
                        await RefreshFromDbAndPublishAsync(sw, ct, source: "db_emergency");
                        return;
                    }

                    _metrics.IncrementCacheRefresh("redis", "miss");
                    _logger.LogWarning("Refresh skipped (non-leader). Redis snapshot missing. Count={Count}, Age={Age}", _store.Current.Count, _store.Age);
                    return;
                }

                try
                {
                    await RefreshFromDbAndPublishAsync(sw, ct, source: "db");
                }
                finally
                {
                    await _redis.ReleaseRefreshLockAsync(token);
                }
            }
            catch (Exception ex)
            {
                _store.UpdateFailure(ex, sw.ElapsedMilliseconds);
                _metrics.IncrementCacheRefresh("db", "fail");
                _metrics.ObserveCacheRefreshDuration("db", sw.ElapsedMilliseconds);

                _logger.LogError(ex, "FaceProfile cache refresh failed. ElapsedMs={ElapsedMs}", sw.ElapsedMilliseconds);
            }
            finally
            {
                sw.Stop();
                _store.MarkRefreshing(false);
                _metrics.SetCacheAgeSeconds(_store.Age.TotalSeconds);
            }
        }

        private async Task RefreshFromDbAndPublishAsync(Stopwatch sw, CancellationToken ct, string source)
        {
            using var scope = _scopeFactory.CreateScope();
            var loader = scope.ServiceProvider.GetRequiredService<IFaceProfileLoader>();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(_opts.RefreshTimeout);

            var profiles = await loader.LoadAsync(timeoutCts.Token);

            var newVersion = await _redis.SetAsync(profiles, ct);

            _store.UpdateSuccess(profiles, sw.ElapsedMilliseconds, newVersion);

            _metrics.IncrementCacheRefresh(source, "success");
            _metrics.ObserveCacheRefreshDuration(source, sw.ElapsedMilliseconds);

            _logger.LogInformation("FaceProfile cache refreshed from DB. Count={Count}, Version={Version}, ElapsedMs={ElapsedMs}",
                profiles.Count, newVersion, sw.ElapsedMilliseconds);
        }
    }
}
