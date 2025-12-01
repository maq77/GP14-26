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
using SSSP.BL.Interfaces;
using SSSP.BL.Options;
using SSSP.DAL.Models;
using SSSP.Infrastructure.Persistence.Interfaces;

namespace SSSP.BL.Services
{
    public sealed class DistributedFaceProfileCache : IFaceProfileCache
    {
        private readonly IDistributedCache _cache;
        private readonly IUnitOfWork _uow;
        private readonly ILogger<DistributedFaceProfileCache> _logger;
        private readonly TimeSpan _expiration;

        private const string CACHE_KEY = "FaceProfiles:All";

        public DistributedFaceProfileCache(
            IDistributedCache cache,
            IUnitOfWork uow,
            IOptions<FaceProfileCacheOptions> options,
            ILogger<DistributedFaceProfileCache> logger)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var opts = options?.Value ?? new FaceProfileCacheOptions();
            _expiration = opts.AbsoluteExpiration <= TimeSpan.Zero
                ? TimeSpan.FromMinutes(5)
                : opts.AbsoluteExpiration;

            _logger.LogInformation(
                "DistributedFaceProfileCache initialized. ExpirationMinutes={ExpirationMinutes}",
                _expiration.TotalMinutes);
        }

        public async Task<IReadOnlyList<FaceProfile>> GetAllAsync(CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                var cached = await _cache.GetStringAsync(CACHE_KEY, ct);

                if (cached != null)
                {
                    sw.Stop();

                    var profiles = JsonSerializer.Deserialize<List<FaceProfile>>(cached);

                    if (profiles != null)
                    {
                        _logger.LogDebug(
                            "Cache HIT. Key={Key}, Profiles={Count}, ElapsedMs={ElapsedMs}",
                            CACHE_KEY, profiles.Count, sw.ElapsedMilliseconds);

                        return profiles;
                    }
                }

                _logger.LogInformation("Cache MISS. Refreshing from database. Key={Key}", CACHE_KEY);

                var loadedProfiles = await LoadFromDatabaseAsync(ct);

                var json = JsonSerializer.Serialize(loadedProfiles, new JsonSerializerOptions
                {
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
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

                _logger.LogInformation(
                    "Cache refreshed from database. Key={Key}, Profiles={Count}, TotalEmbeddings={Embeddings}, ElapsedMs={ElapsedMs}",
                    CACHE_KEY,
                    loadedProfiles.Count,
                    CountEmbeddings(loadedProfiles),
                    sw.ElapsedMilliseconds);

                return loadedProfiles;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex,
                    "Cache operation failed. Key={Key}, ElapsedMs={ElapsedMs}",
                    CACHE_KEY, sw.ElapsedMilliseconds);

                _logger.LogWarning("Falling back to direct database query");
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

        private async Task<IReadOnlyList<FaceProfile>> LoadFromDatabaseAsync(CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();

            var repo = _uow.GetRepository<FaceProfile, Guid>();

            var profiles = await repo.Query
                .AsNoTracking()
                .Include(p => p.User)
                .Include(p => p.Embeddings)
                .ToListAsync(ct);

            sw.Stop();

            _logger.LogDebug(
                "Loaded from database. Profiles={Count}, TotalEmbeddings={Embeddings}, ElapsedMs={ElapsedMs}",
                profiles.Count,
                CountEmbeddings(profiles),
                sw.ElapsedMilliseconds);

            return profiles;
        }

        private static int CountEmbeddings(IReadOnlyList<FaceProfile> profiles)
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