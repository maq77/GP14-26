using System;
using System.Threading;
using System.Threading.Tasks;
using SSSP.DAL.Models;

namespace SSSP.BL.Services.Interfaces
{
    public interface IFaceEnrollmentService
    {
        Task<FaceProfile> EnrollAsync(
            Guid userId,
            byte[] image,
            string? description,
            CancellationToken ct);
    }
}
