using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SSSP.DAL.Models;

namespace SSSP.BL.Services.Interfaces
{
    public interface IFaceManagementService
    {
        Task<IReadOnlyList<FaceProfile>> GetProfilesForUserAsync(
            Guid userId,
            CancellationToken ct);

        Task<FaceProfile?> GetProfileAsync(
            Guid profileId,
            CancellationToken ct);

        Task<bool> DeleteProfileAsync(
            Guid profileId,
            CancellationToken ct);

        Task<bool> SetPrimaryProfileAsync(
            Guid profileId,
            CancellationToken ct);
    }
}
