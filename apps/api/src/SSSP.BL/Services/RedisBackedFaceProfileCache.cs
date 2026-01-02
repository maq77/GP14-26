using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SSSP.BL.DTOs.Faces;
using SSSP.BL.Interfaces;

namespace SSSP.BL.Services
{
    public sealed class RedisBackedFaceProfileCache : IFaceProfileCache
    {
        private readonly FaceProfileCacheStore _store;
        private readonly IFaceProfileDistributedSnapshotCache _redis;
        private readonly ILogger<RedisBackedFaceProfileCache> _logger;

        public RedisBackedFaceProfileCache(
            FaceProfileCacheStore store,
            IFaceProfileDistributedSnapshotCache redis,
            ILogger<RedisBackedFaceProfileCache> logger)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<IReadOnlyList<FaceProfileSnapshot>> GetAllAsync(CancellationToken ct = default)
        {
            // Fast path always: serve in-memory snapshot
            return Task.FromResult(_store.Current);
        }

        public async Task InvalidateAsync()
        {
            // local instance: refresh soon
            _store.RequestRefresh();

            // multi-instance: notify others by bumping version
            await _redis.InvalidateAsync(CancellationToken.None);

            _logger.LogInformation("FaceProfile cache invalidation signaled (refresh requested).");
        }
    }
}
