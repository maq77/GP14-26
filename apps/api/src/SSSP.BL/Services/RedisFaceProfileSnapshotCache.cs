using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using SSSP.BL.DTOs.Faces;
using SSSP.BL.Options;

namespace SSSP.BL.Services
{
    public interface IFaceProfileDistributedSnapshotCache
    {
        Task<(bool found, long version, IReadOnlyList<FaceProfileSnapshot> profiles)> TryGetAsync(CancellationToken ct);
        Task<long> SetAsync(IReadOnlyList<FaceProfileSnapshot> profiles, CancellationToken ct);
        Task InvalidateAsync(CancellationToken ct);
        Task<(bool acquired, string lockToken)> TryAcquireRefreshLockAsync(CancellationToken ct);
        Task ReleaseRefreshLockAsync(string lockToken);
    }

    /// <summary>
    /// Redis distributed snapshot cache for FaceProfiles.
    ///
    /// Pattern:
    /// - Readers: read payload + version (best-effort). If missing => not found.
    /// - Writers: write payload with TTL, then bump version (signals "new snapshot").
    /// - Lock: distributed mutex to ensure only one instance refreshes DB at a time.
    ///
    /// Tenant support (OperatorId) is prepared but commented out for now.
    /// </summary>
    public sealed class RedisFaceProfileDistributedSnapshotCache : IFaceProfileDistributedSnapshotCache
    {
        private readonly IDatabase _db;
        private readonly FaceProfileCacheOptions _opts;
        private readonly ILogger<RedisFaceProfileDistributedSnapshotCache> _logger;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = false,
            PropertyNamingPolicy = null
        };

        // TODO (later): OperatorId suffix
        // private static string Key(string baseKey, Guid operatorId) => $"{baseKey}:op:{operatorId}";

        private const string VersionKey = "FaceProfiles:Version";
        private const string PayloadKey = "FaceProfiles:Payload";
        private const string LockKey = "FaceProfiles:RefreshLock";

        private const string ReleaseLockLua = @"
                                                    if redis.call('get', KEYS[1]) == ARGV[1] then
                                                      return redis.call('del', KEYS[1])
                                                    else
                                                      return 0
                                                    end
                                                ";


        public RedisFaceProfileDistributedSnapshotCache(
            IConnectionMultiplexer mux,
            IOptions<FaceProfileCacheOptions> options,
            ILogger<RedisFaceProfileDistributedSnapshotCache> logger)
        {
            if (mux == null) throw new ArgumentNullException(nameof(mux));
            if (options == null) throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _db = mux.GetDatabase();
            _opts = options.Value;

            if (_opts.DistributedTtl <= TimeSpan.Zero)
                _opts.DistributedTtl = TimeSpan.FromMinutes(3);

            if (_opts.LockTtl <= TimeSpan.Zero)
                _opts.LockTtl = TimeSpan.FromSeconds(20);
        }

        public async Task<(bool found, long version, IReadOnlyList<FaceProfileSnapshot> profiles)> TryGetAsync(CancellationToken ct)
        {
            try
            {
                var payload = await _db.StringGetAsync(PayloadKey).ConfigureAwait(false);
                if (payload.IsNullOrEmpty)
                    return (false, 0L, Array.Empty<FaceProfileSnapshot>());

                var versionVal = await _db.StringGetAsync(VersionKey).ConfigureAwait(false);
                var version = 0L;

                if (!versionVal.IsNullOrEmpty && long.TryParse(versionVal.ToString(), out var parsed))
                    version = parsed;

                try
                {
                    var profiles = JsonSerializer.Deserialize<List<FaceProfileSnapshot>>(payload!, JsonOpts)
                                  ?? new List<FaceProfileSnapshot>();

                    return (true, version, profiles);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Redis FaceProfiles payload deserialize failed; treating as cache miss.");
                    return (false, 0L, Array.Empty<FaceProfileSnapshot>());
                }
            }
            catch (RedisConnectionException ex)
            {
                _logger.LogWarning(ex, "Redis unavailable in TryGetAsync; treating as cache miss.");
                return (false, 0L, Array.Empty<FaceProfileSnapshot>());
            }
            catch (RedisTimeoutException ex)
            {
                _logger.LogWarning(ex, "Redis timeout in TryGetAsync; treating as cache miss.");
                return (false, 0L, Array.Empty<FaceProfileSnapshot>());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unexpected Redis error in TryGetAsync; treating as cache miss.");
                return (false, 0L, Array.Empty<FaceProfileSnapshot>());
            }
        }

        public async Task<long> SetAsync(IReadOnlyList<FaceProfileSnapshot> profiles, CancellationToken ct)
        {
            try
            {
                profiles ??= Array.Empty<FaceProfileSnapshot>();

                var json = JsonSerializer.Serialize(profiles, JsonOpts);

                var payloadOk = await _db.StringSetAsync(PayloadKey, json, _opts.DistributedTtl).ConfigureAwait(false);
                if (!payloadOk)
                {
                    _logger.LogWarning("Redis payload write returned false. Snapshot not persisted.");
                    return 0L;
                }

                var version = await _db.StringIncrementAsync(VersionKey).ConfigureAwait(false);
                return version;
            }
            catch (RedisConnectionException ex)
            {
                _logger.LogWarning(ex, "Redis unavailable in SetAsync; snapshot not persisted.");
                return 0L;
            }
            catch (RedisTimeoutException ex)
            {
                _logger.LogWarning(ex, "Redis timeout in SetAsync; snapshot not persisted.");
                return 0L;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unexpected Redis error in SetAsync; snapshot not persisted.");
                return 0L;
            }
        }

        public async Task InvalidateAsync(CancellationToken ct)
        {
            try
            {
                await _db.StringIncrementAsync(VersionKey).ConfigureAwait(false);
                await _db.KeyExpireAsync(PayloadKey, TimeSpan.FromSeconds(1)).ConfigureAwait(false);
            }
            catch (RedisConnectionException ex)
            {
                _logger.LogWarning(ex, "Redis unavailable in InvalidateAsync; ignoring.");
            }
            catch (RedisTimeoutException ex)
            {
                _logger.LogWarning(ex, "Redis timeout in InvalidateAsync; ignoring.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unexpected Redis error in InvalidateAsync; ignoring.");
            }
        }

        public async Task<(bool acquired, string lockToken)> TryAcquireRefreshLockAsync(CancellationToken ct)
        {
            try
            {
                var token = Guid.NewGuid().ToString("N");

                var acquired = await _db.StringSetAsync(
                    LockKey,
                    token,
                    expiry: _opts.LockTtl,
                    when: When.NotExists
                ).ConfigureAwait(false);

                return (acquired, token);
            }
            catch (RedisConnectionException ex)
            {
                _logger.LogWarning(ex, "Redis unavailable in TryAcquireRefreshLockAsync; lock not acquired.");
                return (false, string.Empty);
            }
            catch (RedisTimeoutException ex)
            {
                _logger.LogWarning(ex, "Redis timeout in TryAcquireRefreshLockAsync; lock not acquired.");
                return (false, string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unexpected Redis error in TryAcquireRefreshLockAsync; lock not acquired.");
                return (false, string.Empty);
            }
        }

        public async Task ReleaseRefreshLockAsync(string lockToken)
        {
            if (string.IsNullOrWhiteSpace(lockToken))
                return;

            try
            {
                await _db.ScriptEvaluateAsync(
                    ReleaseLockLua,
                    new RedisKey[] { LockKey },
                    new RedisValue[] { lockToken }
                ).ConfigureAwait(false);
            }
            catch (RedisConnectionException ex)
            {
                _logger.LogWarning(ex, "Redis unavailable in ReleaseRefreshLockAsync; ignoring.");
            }
            catch (RedisTimeoutException ex)
            {
                _logger.LogWarning(ex, "Redis timeout in ReleaseRefreshLockAsync; ignoring.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unexpected Redis error in ReleaseRefreshLockAsync; ignoring.");
            }
        }
    }
}
