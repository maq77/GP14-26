using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SSSP.BL.Interfaces;
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

        private volatile IReadOnlyList<FaceProfile>? _profiles;
        private long _lastRefreshTicks = DateTime.MinValue.Ticks;
        private int _disposed;

        public FaceProfileCache(
            IUnitOfWork uow,
            IOptions<FaceProfileCacheOptions> options,
            ILogger<FaceProfileCache> logger)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var opts = options?.Value ?? new FaceProfileCacheOptions();
            _expiration = opts.AbsoluteExpiration <= TimeSpan.Zero
                ? TimeSpan.FromMinutes(1)
                : opts.AbsoluteExpiration;

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

        public async Task<IReadOnlyList<FaceProfile>> GetAllAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();

            var currentProfiles = _profiles;
            var age = DateTime.UtcNow - _lastRefreshUtc;

            if (currentProfiles != null && age < _expiration)
            {
                return currentProfiles;
            }

            var acquired = await _refreshLock.WaitAsync(0, ct);
            if (!acquired)
            {
                await _refreshLock.WaitAsync(ct);
                _refreshLock.Release();

                currentProfiles = _profiles;
                if (currentProfiles != null)
                    return currentProfiles;
            }

            try
            {
                currentProfiles = _profiles;
                age = DateTime.UtcNow - _lastRefreshUtc;

                if (currentProfiles != null && age < _expiration)
                {
                    return currentProfiles;
                }

                var sw = Stopwatch.StartNew();
                var previousCount = currentProfiles?.Count ?? 0;

                _logger.LogInformation("Refreshing FaceProfile cache. PreviousCount={PreviousCount}, CacheAge={CacheAgeSeconds}s",
                    previousCount, age.TotalSeconds);

                var repo = _uow.GetRepository<FaceProfile, Guid>();
                var list = await repo
                    .Query
                    .AsNoTracking()
                    .Include(p => p.User)
                    .Include(p => p.Embeddings)
                    .ToListAsync(ct);

                _profiles = list;
                _lastRefreshUtc = DateTime.UtcNow;

                sw.Stop();

                _logger.LogInformation(
                    "FaceProfile cache refreshed. Count={Count}, PreviousCount={PreviousCount}, Delta={Delta}, ElapsedMs={ElapsedMs}",
                    list.Count, previousCount, list.Count - previousCount, sw.ElapsedMilliseconds);

                return _profiles;
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