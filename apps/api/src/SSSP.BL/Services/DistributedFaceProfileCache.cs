using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SSSP.BL.DTOs.Faces;
using SSSP.BL.Interfaces;
using SSSP.BL.Monitoring;
using SSSP.BL.Options;
using SSSP.BL.Utils;
using SSSP.DAL.Models;
using SSSP.Infrastructure.Persistence.Interfaces;

namespace SSSP.BL.Services
{
    public sealed class DistributedFaceProfileCache : IFaceProfileCache
    {
        private readonly IDistributedCache _cache;
        private readonly IUnitOfWork _uow;
        private readonly ILogger<DistributedFaceProfileCache> _logger;
        private readonly FaceProfileCacheMetrics _metrics;
        private readonly TimeSpan _expiration;

        private const string CACHE_KEY = "FaceProfiles:All";

        public DistributedFaceProfileCache(
            IDistributedCache cache,
            IUnitOfWork uow,
            IOptions<FaceProfileCacheOptions> options,
            FaceProfileCacheMetrics metrics,
            ILogger<DistributedFaceProfileCache> logger)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var opts = options?.Value ?? new FaceProfileCacheOptions();
            _expiration = opts.AbsoluteExpiration <= TimeSpan.Zero
                ? TimeSpan.FromMinutes(5)
                : opts.AbsoluteExpiration;

            _logger.LogInformation(
                "DistributedFaceProfileCache initialized. ExpirationMinutes={ExpirationMinutes}",
                _expiration.TotalMinutes);
        }

        public async Task<IReadOnlyList<FaceProfileSnapshot>> GetAllAsync(
            CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                var cached = await _cache.GetStringAsync(CACHE_KEY, ct);

                if (!string.IsNullOrEmpty(cached))
                {
                    var profiles = JsonSerializer.Deserialize<List<FaceProfileSnapshot>>(cached);

                    if (profiles != null)
                    {
                        sw.Stop();
                        _metrics.IncrementL2Hit();

                        _logger.LogDebug(
                            "Distributed face cache HIT. Key={Key}, Profiles={Count}, ElapsedMs={ElapsedMs}",
                            CACHE_KEY,
                            profiles.Count,
                            sw.ElapsedMilliseconds);

                        return profiles;
                    }
                }

                // Miss in Redis
                _metrics.IncrementL2Miss();
                _metrics.IncrementDbLoad();

                _logger.LogInformation(
                    "Distributed face cache MISS. Loading from database. Key={Key}",
                    CACHE_KEY);

                var loadedProfiles = await LoadFromDatabaseAsync(ct);

                var json = JsonSerializer.Serialize(loadedProfiles, new JsonSerializerOptions
                {
                    // snapshots have no cycles, but safe to keep it simple
                    WriteIndented = false
                });

                await _cache.SetStringAsync(
                    CACHE_KEY,
                    json,
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = _expiration
                    },
                    ct);

                sw.Stop();

                var (l1h, l1m, l2h, l2m, dbLoads) = _metrics.Snapshot();

                _logger.LogInformation(
                    "Distributed face cache refreshed from database. Key={Key}, Profiles={Count}, ElapsedMs={ElapsedMs}, " +
                    "L1Hits={L1Hits}, L1Misses={L1Misses}, L2Hits={L2Hits}, L2Misses={L2Misses}, DbLoads={DbLoads}",
                    CACHE_KEY,
                    loadedProfiles.Count,
                    sw.ElapsedMilliseconds,
                    l1h, l1m, l2h, l2m, dbLoads);

                return loadedProfiles;
            }
            catch (Exception ex)
            {
                sw.Stop();

                _logger.LogError(
                    ex,
                    "Distributed face cache operation failed. Key={Key}, ElapsedMs={ElapsedMs}",
                    CACHE_KEY,
                    sw.ElapsedMilliseconds);

                _logger.LogWarning("Falling back to direct database query for FaceProfiles.");
                return await LoadFromDatabaseAsync(ct);
            }
        }


        public async Task InvalidateAsync()
        {
            var sw = Stopwatch.StartNew();

            try
            {
                await _cache.RemoveAsync(CACHE_KEY);

                sw.Stop();

                _logger.LogInformation(
                    "Cache invalidated. Key={Key}, ElapsedMs={ElapsedMs}",
                    CACHE_KEY, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex,
                    "Cache invalidation failed. Key={Key}, ElapsedMs={ElapsedMs}",
                    CACHE_KEY, sw.ElapsedMilliseconds);
            }
        }

        private async Task<IReadOnlyList<FaceProfileSnapshot>> LoadFromDatabaseAsync(
            CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();

            var repo = _uow.GetRepository<FaceProfile, Guid>();

            var entities = await repo.Query
                .AsNoTracking()
                .Include(p => p.User)
                .Include(p => p.Embeddings)
                .ToListAsync(ct);

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

                        float[] vector;
                        if (emb.Vector.Length % sizeof(float) != 0)
                        {
                            vector = Array.Empty<float>();
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

            sw.Stop();

            _logger.LogDebug(
                "Loaded FaceProfiles from database for distributed cache. Profiles={Count}, ElapsedMs={ElapsedMs}",
                snapshots.Count,
                sw.ElapsedMilliseconds);

            return snapshots;
        }


        private static int CountEmbeddings(IReadOnlyList<FaceProfileSnapshot> profiles)
        {
            var count = 0;
            foreach (var profile in profiles)
            {
                if (profile.Embeddings != null)
                    count += profile.Embeddings.Count;
            }
            return count;
        }
    }
}