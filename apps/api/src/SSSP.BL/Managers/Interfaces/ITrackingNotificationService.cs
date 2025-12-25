using System.Threading;
using System.Threading.Tasks;
using SSSP.BL.DTOs.Tracking;

namespace SSSP.BL.Interfaces
{
    public interface ITrackingNotificationService
    {
        Task NotifyUserTrackedAsync(
            UserTrackingSession session,
            string cameraId,
            CancellationToken ct = default);
    }
}
