// SSSP.Api/Services/TrackingNotificationService.cs
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using SSSP.Api.Hubs;
using SSSP.BL.DTOs.Tracking;
using SSSP.BL.Interfaces;

namespace SSSP.Api.Services
{
    public sealed class TrackingNotificationService : ITrackingNotificationService
    {
        private readonly IHubContext<TrackingHub> _hubContext;

        public TrackingNotificationService(IHubContext<TrackingHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public Task NotifyUserTrackedAsync(
            UserTrackingSession session,
            string cameraId,
            CancellationToken ct = default)
        {
            return _hubContext.Clients.Group(cameraId).SendAsync("UserTracked", new
            {
                UserId = session.UserId,
                Cameras = session.SeenCameras.ToList(),
                Zones = session.VisitedZones.Keys.ToList(),
                AverageSimilarity = session.AvgSimilarity,
                LastSeen = session.LastSeenUtc
            }, ct);
        }
    }
}
