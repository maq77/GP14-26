using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SSSP.BL.Services.Interfaces
{
    public interface IFaceAutoEnrollmentService
    {
        Task TryAutoEnrollAsync(
            Guid userId,
            Guid faceProfileId,
            IReadOnlyList<float> embedding,
            string cameraId,
            CancellationToken ct);
    }
}
