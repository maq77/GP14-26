using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SSSP.BL.DTOs.Faces;

namespace SSSP.BL.Services
{
    public sealed class NullFaceProfileDistributedSnapshotCache : IFaceProfileDistributedSnapshotCache
    {
        public Task<(bool found, long version, IReadOnlyList<FaceProfileSnapshot> profiles)> TryGetAsync(CancellationToken ct)
            => Task.FromResult<(bool, long, IReadOnlyList<FaceProfileSnapshot>)>((false, 0L, Array.Empty<FaceProfileSnapshot>()));

        public Task<long> SetAsync(IReadOnlyList<FaceProfileSnapshot> profiles, CancellationToken ct)
            => Task.FromResult(0L);

        public Task InvalidateAsync(CancellationToken ct)
            => Task.CompletedTask;

        public Task<(bool acquired, string lockToken)> TryAcquireRefreshLockAsync(CancellationToken ct)
            => Task.FromResult<(bool, string)>((true, "null")); 

        public Task ReleaseRefreshLockAsync(string lockToken)
            => Task.CompletedTask;
    }
}
