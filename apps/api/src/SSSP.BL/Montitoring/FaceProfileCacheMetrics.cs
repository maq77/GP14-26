using System;
using System.Threading;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace SSSP.BL.Monitoring
{
    public sealed class FaceProfileCacheMetrics
    {
        private long _l1Hits;
        private long _l1Misses;
        private long _l2Hits;
        private long _l2Misses;
        private long _dbLoads;

        private readonly ILogger<FaceProfileCacheMetrics> _logger;

        private static readonly Gauge L1HitsGauge =
            Metrics.CreateGauge("face_cache_l1_hits_total", "Total number of L1 cache hits.");

        private static readonly Gauge L1MissesGauge =
            Metrics.CreateGauge("face_cache_l1_misses_total", "Total number of L1 cache misses.");

        private static readonly Gauge L2HitsGauge =
            Metrics.CreateGauge("face_cache_l2_hits_total", "Total number of L2 cache hits.");

        private static readonly Gauge L2MissesGauge =
            Metrics.CreateGauge("face_cache_l2_misses_total", "Total number of L2 cache misses.");

        private static readonly Gauge DbLoadsGauge =
            Metrics.CreateGauge("face_cache_db_loads_total", "Total number of DB loads for face profiles.");

        public FaceProfileCacheMetrics(ILogger<FaceProfileCacheMetrics> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void IncrementL1Hit()
        {
            var newValue = Interlocked.Increment(ref _l1Hits);
            L1HitsGauge.Set(newValue);
        }

        public void IncrementL1Miss()
        {
            var newValue = Interlocked.Increment(ref _l1Misses);
            L1MissesGauge.Set(newValue);
        }

        public void IncrementL2Hit()
        {
            var newValue = Interlocked.Increment(ref _l2Hits);
            L2HitsGauge.Set(newValue);
        }

        public void IncrementL2Miss()
        {
            var newValue = Interlocked.Increment(ref _l2Misses);
            L2MissesGauge.Set(newValue);
        }

        public void IncrementDbLoad()
        {
            var newValue = Interlocked.Increment(ref _dbLoads);
            DbLoadsGauge.Set(newValue);
        }

        public (long L1Hits, long L1Misses, long L2Hits, long L2Misses, long DbLoads) Snapshot()
        {
            return (
                Interlocked.Read(ref _l1Hits),
                Interlocked.Read(ref _l1Misses),
                Interlocked.Read(ref _l2Hits),
                Interlocked.Read(ref _l2Misses),
                Interlocked.Read(ref _dbLoads)
            );
        }

        public void LogSnapshot()
        {
            var (l1h, l1m, l2h, l2m, db) = Snapshot();

            _logger.LogInformation(
                "Face cache metrics: L1(Hit={L1H}, Miss={L1M}), L2(Hit={L2H}, Miss={L2M}), DbLoads={DbLoads}",
                l1h, l1m, l2h, l2m, db);
        }
    }
}
