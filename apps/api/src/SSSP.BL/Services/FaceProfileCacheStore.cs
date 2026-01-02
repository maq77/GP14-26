using System;
using System.Collections.Generic;
using System.Threading;
using SSSP.BL.DTOs.Faces;
using SSSP.Telemetry.Abstractions.Faces;

namespace SSSP.BL.Services
{
    public sealed class FaceProfileCacheStore
    {
        private IReadOnlyList<FaceProfileSnapshot> _current = Array.Empty<FaceProfileSnapshot>();
        private readonly IFaceMetrics _metrics;

        private long _lastRefreshUtcTicks;
        private int _lastRefreshSucceeded; // 0/1
        private long _lastRefreshDurationMs;
        private string? _lastError;
        private long _version;
        private int _isRefreshing; // 0/1
        private int _refreshRequested; // 0/1

        public FaceProfileCacheStore(IFaceMetrics metrics)
        {
            _lastRefreshUtcTicks = DateTime.UtcNow.Ticks;
            _lastRefreshSucceeded = 1;
            _lastRefreshDurationMs = 0;
            _version = 0;
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        }

        public IReadOnlyList<FaceProfileSnapshot> Current => Volatile.Read(ref _current);

        public DateTime LastRefreshUtc => new DateTime(Interlocked.Read(ref _lastRefreshUtcTicks), DateTimeKind.Utc);
        public TimeSpan Age => DateTime.UtcNow - LastRefreshUtc;

        public bool LastRefreshSucceeded => Interlocked.CompareExchange(ref _lastRefreshSucceeded, 0, 0) == 1;
        public long LastRefreshDurationMs => Interlocked.Read(ref _lastRefreshDurationMs);
        public string? LastError => Volatile.Read(ref _lastError);
        public long Version => Interlocked.Read(ref _version);

        public bool IsRefreshing => Interlocked.CompareExchange(ref _isRefreshing, 0, 0) == 1;
        public void MarkRefreshing(bool refreshing)
        {
            _metrics.SetCacheRefreshing(refreshing);
            Interlocked.Exchange(ref _isRefreshing, refreshing ? 1 : 0);
        }

        public bool ConsumeRefreshRequested() => Interlocked.Exchange(ref _refreshRequested, 0) == 1;
        public void RequestRefresh() => Interlocked.Exchange(ref _refreshRequested, 1);

        public void UpdateSuccess(IReadOnlyList<FaceProfileSnapshot> snapshots, long durationMs, long newVersion)
        {
            snapshots ??= Array.Empty<FaceProfileSnapshot>();

            Volatile.Write(ref _current, snapshots);
            Interlocked.Exchange(ref _lastRefreshUtcTicks, DateTime.UtcNow.Ticks);
            Interlocked.Exchange(ref _lastRefreshSucceeded, 1);
            Interlocked.Exchange(ref _lastRefreshDurationMs, durationMs);
            Volatile.Write(ref _lastError, null);
            Interlocked.Exchange(ref _version, newVersion);

            _metrics.SetCacheProfilesCount(snapshots.Count);
            _metrics.SetCacheEmbeddingsCount(CountEmbeddings(snapshots));
            _metrics.SetCacheAgeSeconds(0);
            _metrics.SetCacheVersion(newVersion);
        }


        public void UpdateFailure(Exception ex, long durationMs)
        {
            Interlocked.Exchange(ref _lastRefreshSucceeded, 0);
            Interlocked.Exchange(ref _lastRefreshDurationMs, durationMs);
            Volatile.Write(ref _lastError, ex.Message);
        }


        private static int CountEmbeddings(IReadOnlyList<FaceProfileSnapshot> profiles)
        {
            var total = 0;
            foreach (var p in profiles)
                if (p?.Embeddings != null) total += p.Embeddings.Count;
            return total;
        }

    }
}
