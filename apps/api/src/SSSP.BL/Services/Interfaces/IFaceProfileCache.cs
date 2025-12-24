using SSSP.BL.DTOs.Faces;
using SSSP.DAL.Models;

namespace SSSP.BL.Interfaces
{
    public interface IFaceProfileCache
    {
        Task<IReadOnlyList<FaceProfileSnapshot>> GetAllAsync(CancellationToken ct = default);
        Task InvalidateAsync();
    }
}
