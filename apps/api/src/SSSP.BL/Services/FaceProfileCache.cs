using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SSSP.BL.Interfaces;
using SSSP.BL.Options;
using SSSP.DAL.Models;
using SSSP.Infrastructure.Persistence.Interfaces;
using System;
using System.Collections.Generic;
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

        private IReadOnlyList<FaceProfile>? _profiles;
        private DateTime _lastRefreshUtc = DateTime.MinValue;
        private bool _disposed;

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
        }

        public async Task<IReadOnlyList<FaceProfile>> GetAllAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();

            if (_profiles != null &&
                (DateTime.UtcNow - _lastRefreshUtc) < _expiration)
            {
                return _profiles;
            }

            await _refreshLock.WaitAsync(ct);
            try
            {
                if (_profiles != null &&
                    (DateTime.UtcNow - _lastRefreshUtc) < _expiration)
                {
                    return _profiles;
                }

                _logger.LogInformation("Refreshing FaceProfile cache from database...");

                var repo = _uow.GetRepository<FaceProfile, Guid>();

                var list = await repo
                    .Query
                    .Include(p => p.User)
                    .Include(p => p.Embeddings)
                    .ToListAsync(ct);

                _profiles = list;
                _lastRefreshUtc = DateTime.UtcNow;

                _logger.LogInformation(
                    "FaceProfile cache refreshed. Count={Count}",
                    _profiles.Count);

                return _profiles;
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        public Task InvalidateAsync()
        {
            ThrowIfDisposed();

            _logger.LogInformation("FaceProfile cache invalidated.");
            _profiles = null;
            _lastRefreshUtc = DateTime.MinValue;
            return Task.CompletedTask;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FaceProfileCache));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _refreshLock.Dispose();
        }
    }
}
