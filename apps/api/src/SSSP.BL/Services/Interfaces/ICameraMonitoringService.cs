using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SSSP.BL.Services.Interfaces
{
    public sealed record CameraMonitoringStatus(
        int CameraId,
        string RtspUrl,
        DateTimeOffset StartedAt,
        bool IsRunning
    );

    public interface ICameraMonitoringService
    {
        Task<bool> StartAsync(
            int cameraId,
            string rtspUrl,
            CancellationToken cancellationToken = default);

        Task<bool> StopAsync(
            int cameraId,
            CancellationToken cancellationToken = default);

        IReadOnlyCollection<CameraMonitoringStatus> GetActiveSessions();
    }
}
