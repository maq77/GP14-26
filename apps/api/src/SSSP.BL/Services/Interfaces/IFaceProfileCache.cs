using SSSP.DAL.Models;

namespace SSSP.BL.Interfaces
{
    public interface IFaceProfileCache
    {
        Task<IReadOnlyList<FaceProfile>> GetAllAsync(CancellationToken ct = default);
        Task InvalidateAsync();
    }
}
