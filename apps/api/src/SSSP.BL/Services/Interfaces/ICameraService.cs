using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SSSP.DAL.Enums;
using SSSP.DAL.Models;

namespace SSSP.BL.Services.Interfaces
{
    public interface ICameraService
    {
        Task<IReadOnlyList<Camera>> GetAllAsync(CancellationToken ct);
        Task<Camera?> GetByIdAsync(int id, CancellationToken ct);
        Task<Camera> CreateAsync(
            string name,
        string rtspUrl,
            CameraAICapabilities capabilities,
            CameraRecognitionMode recognitionMode,
            double? matchThresholdOverride,
            CancellationToken ct);
        Task<bool> UpdateAsync(
            int id,
            string name,
            string rtspUrl,
        bool isActive,
            CameraAICapabilities capabilities,
            CameraRecognitionMode recognitionMode,
            double? matchThresholdOverride,
            CancellationToken ct);
        Task<bool> DeleteAsync(int id, CancellationToken ct);
    }
}
