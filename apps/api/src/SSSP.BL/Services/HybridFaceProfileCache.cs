using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SSSP.BL.DTOs.Faces;
using SSSP.BL.Interfaces;
using SSSP.BL.Monitoring;
using SSSP.BL.Options;
using SSSP.DAL.Models;

namespace SSSP.BL.Services
{
    /* 
    /// Hybrid face profile cache:
    /// L1  = in-process memory (IMemoryCache)
    /// L2  = DistributedFaceProfileCache (Redis + DB)
    /// DB  = via DistributedFaceProfileCache
    /// 
    /// IFaceProfileCache -> this class
    */
    public sealed class HybridFaceProfileCache : IFaceProfileCache
    {
        private const string L1_CACHE_KEY = "FaceProfiles:L1";

        private readonly IMemoryCache _memoryCache;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly FaceProfileCacheMetrics _metrics;
        private readonly ILogger<HybridFaceProfileCache> _logger;
        private readonly TimeSpan _l1Expiration;

        public HybridFaceProfileCache(
            IMemoryCache memoryCache,
            IServiceScopeFactory scopeFactory,
            IOptions<FaceProfileCacheOptions> options,
            FaceProfileCacheMetrics metrics,
            ILogger<HybridFaceProfileCache> logger)
        {
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var opts = options?.Value ?? new FaceProfileCacheOptions();

            // You can tune this to be shorter than Redis if you want.
            _l1Expiration = opts.AbsoluteExpiration <= TimeSpan.Zero
                ? TimeSpan.FromMinutes(1)
                : opts.AbsoluteExpiration;

            _logger.LogInformation(
                "HybridFaceProfileCache initialized. L1ExpirationMinutes={Minutes}",
                _l1Expiration.TotalMinutes);
        }

        public async Task<IReadOnlyList<FaceProfileSnapshot>> GetAllAsync(CancellationToken ct = default)
        {
            if (_memoryCache.TryGetValue(L1_CACHE_KEY, out IReadOnlyList<FaceProfileSnapshot> cached)
                && cached is { Count: > 0 })
            {
                _logger.LogDebug("L1 face cache HIT. Profiles={Count}", cached.Count);
                _metrics.IncrementL1Hit();
                return cached;
            }

            _logger.LogDebug("L1 face cache MISS. Resolving from L2 (Redis + DB).");
            _metrics.IncrementL1Miss();

            var sw = Stopwatch.StartNew();

            using var scope = _scopeFactory.CreateScope();
            var l2 = scope.ServiceProvider.GetRequiredService<DistributedFaceProfileCache>();

            var profiles = await l2.GetAllAsync(ct);

            sw.Stop();

            _logger.LogInformation(
                "L2 face cache load completed. Profiles={Count}, ElapsedMs={ElapsedMs}",
                profiles.Count,
                sw.ElapsedMilliseconds);

            _memoryCache.Set(
                L1_CACHE_KEY,
                profiles,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = _l1Expiration,
                    Size = profiles.Count
                });

            return profiles;
        }

        public async Task InvalidateAsync()
        {
            _logger.LogInformation("Hybrid face cache invalidation requested. Clearing L1 and L2.");

            _memoryCache.Remove(L1_CACHE_KEY);

            using var scope = _scopeFactory.CreateScope();
            var l2 = scope.ServiceProvider.GetRequiredService<DistributedFaceProfileCache>();
            await l2.InvalidateAsync();
        }

    }
}
