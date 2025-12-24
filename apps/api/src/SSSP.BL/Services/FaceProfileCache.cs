using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SSSP.BL.DTOs.Faces;
using SSSP.BL.Interfaces;
using SSSP.BL.Monitoring;
using SSSP.BL.Options;
using SSSP.DAL.Models;
using SSSP.Infrastructure.Persistence.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SSSP.BL.Services
{
    public sealed class FaceProfileCache : IFaceProfileCache, IDisposable
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<FaceProfileCache> _logger;
        private readonly TimeSpan _expiration;
        private readonly SemaphoreSlim _refreshLock = new(1, 1);
        private readonly FaceProfileCacheMetrics _metrics;

        private volatile IReadOnlyList<FaceProfileSnapshot>? _profiles;
        private long _lastRefreshTicks = DateTime.MinValue.Ticks;
        private int _disposed;

        public FaceProfileCache(
            IUnitOfWork uow,
            IOptions<FaceProfileCacheOptions> options,
            FaceProfileCacheMetrics metrics,
            ILogger<FaceProfileCache> logger)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var opts = options?.Value ?? new FaceProfileCacheOptions();
            _expiration = opts.AbsoluteExpiration <= TimeSpan.Zero
                ? TimeSpan.FromMinutes(1)
                : opts.AbsoluteExpiration;

            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));

            _logger.LogInformation("FaceProfileCache initialized. ExpirationMinutes={ExpirationMinutes}",
                _expiration.TotalMinutes);
        }

        private DateTime _lastRefreshUtc
        {
            get => new DateTime(
                Interlocked.Read(ref _lastRefreshTicks),
                DateTimeKind.Utc);

            set => Interlocked.Exchange(ref _lastRefreshTicks, value.Ticks);
        }

        public async Task<IReadOnlyList<FaceProfileSnapshot>> GetAllAsync(
            CancellationToken ct = default)
        {
            ThrowIfDisposed();

            var currentProfiles = _profiles;
            var age = DateTime.UtcNow - _lastRefreshUtc;

            // L1 HIT (in-process _profiles)
            if (currentProfiles != null && age < _expiration)
            {
                _metrics.IncrementL1Hit();

                _logger.LogDebug(
                    "FaceProfile L1 cache HIT. Count={Count}, AgeSeconds={AgeSeconds:F2}",
                    currentProfiles.Count,
                    age.TotalSeconds);

                return currentProfiles;
            }

            // Fast path: try to acquire without waiting
            var acquired = await _refreshLock.WaitAsync(0, ct);
            if (!acquired)
            {
                // someone else is refreshing → wait once and return what they loaded
                await _refreshLock.WaitAsync(ct);
                _refreshLock.Release();

                currentProfiles = _profiles;
                if (currentProfiles != null)
                {
                    _metrics.IncrementL1Hit();

                    _logger.LogDebug(
                        "FaceProfile L1 cache HIT after wait. Count={Count}",
                        currentProfiles.Count);

                    return currentProfiles;
                }
            }

            try
            {
                // Double-check after taking the lock
                currentProfiles = _profiles;
                age = DateTime.UtcNow - _lastRefreshUtc;

                if (currentProfiles != null && age < _expiration)
                {
                    _metrics.IncrementL1Hit();

                    _logger.LogDebug(
                        "FaceProfile L1 cache HIT (post-lock). Count={Count}, AgeSeconds={AgeSeconds:F2}",
                        currentProfiles.Count,
                        age.TotalSeconds);

                    return currentProfiles;
                }

                // We are definitely going to DB
                _metrics.IncrementL1Miss();
                _metrics.IncrementDbLoad();

                var sw = Stopwatch.StartNew();
                var previousCount = currentProfiles?.Count ?? 0;

                _logger.LogInformation(
                    "Refreshing FaceProfile cache from database. PreviousCount={PreviousCount}, CacheAgeSeconds={CacheAgeSeconds:F2}",
                    previousCount,
                    age.TotalSeconds);

                var repo = _uow.GetRepository<FaceProfile, Guid>();

                var entities = await repo.Query
                    .AsNoTracking()
                    .Include(p => p.User)
                    .Include(p => p.Embeddings)
                    .ToListAsync(ct);

                // Map EF → snapshots
                var snapshots = new List<FaceProfileSnapshot>(entities.Count);

                foreach (var profile in entities)
                {
                    if (profile == null)
                        continue;

                    var embeddingSnapshots = new List<FaceEmbeddingSnapshot>();

                    if (profile.Embeddings != null)
                    {
                        foreach (var emb in profile.Embeddings)
                        {
                            if (emb == null || emb.Vector == null || emb.Vector.Length == 0)
                                continue;

                            // Convert byte[] → float[]
                            float[] vector;
                            if (emb.Vector.Length % sizeof(float) != 0)
                            {
                                vector = Array.Empty<float>(); // corrupted / unexpected length
                            }
                            else
                            {
                                var floatCount = emb.Vector.Length / sizeof(float);
                                vector = new float[floatCount];
                                Buffer.BlockCopy(emb.Vector, 0, vector, 0, emb.Vector.Length);
                            }

                            embeddingSnapshots.Add(new FaceEmbeddingSnapshot
                            {
                                Id = emb.Id,
                                Vector = vector
                            });
                        }
                    }

                    snapshots.Add(new FaceProfileSnapshot
                    {
                        Id = profile.Id,
                        UserId = profile.UserId,
                        UserName = profile.User?.UserName ?? "N/A",
                        FullName = profile.User?.FullName ?? "Name Unassigned",
                        IsPrimary = profile.IsPrimary,
                        CreatedAt = profile.CreatedAt,
                        Embeddings = embeddingSnapshots
                    });
                }

                _profiles = snapshots;
                _lastRefreshUtc = DateTime.UtcNow;

                sw.Stop();

                var (l1h, l1m, l2h, l2m, dbLoads) = _metrics.Snapshot();

                _logger.LogInformation(
                    "FaceProfile cache refreshed. Count={Count}, PreviousCount={PreviousCount}, Delta={Delta}, ElapsedMs={ElapsedMs}, " +
                    "L1Hits={L1Hits}, L1Misses={L1Misses}, L2Hits={L2Hits}, L2Misses={L2Misses}, DbLoads={DbLoads}",
                    snapshots.Count,
                    previousCount,
                    snapshots.Count - previousCount,
                    sw.ElapsedMilliseconds,
                    l1h, l1m, l2h, l2m, dbLoads);

                return _profiles!;
            }
            finally
            {
                if (acquired)
                    _refreshLock.Release();
            }
        }



        public Task InvalidateAsync()
        {
            ThrowIfDisposed();

            var previousCount = _profiles?.Count ?? 0;
            var cacheAge = DateTime.UtcNow - _lastRefreshUtc;

            _profiles = null;
            _lastRefreshUtc = DateTime.MinValue;

            _logger.LogInformation("FaceProfile cache invalidated. PreviousCount={PreviousCount}, CacheAge={CacheAgeSeconds}s",
                previousCount, cacheAge.TotalSeconds);

            return Task.CompletedTask;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed != 0)
                throw new ObjectDisposedException(nameof(FaceProfileCache));
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            _refreshLock.Dispose();
            _logger.LogDebug("FaceProfileCache disposed");
        }
    }
}