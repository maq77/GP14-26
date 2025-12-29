using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SSSP.BL.DTOs.Tracking;
using SSSP.BL.Interfaces;
using SSSP.BL.Outbox;

namespace SSSP.Api.Services;

public sealed class TrackingNotificationService : ITrackingNotificationService
{
    private readonly IOutboxWriter _outbox;
    private readonly ILogger<TrackingNotificationService> _logger;

    public TrackingNotificationService(IOutboxWriter outbox, ILogger<TrackingNotificationService> logger)
    {
        _outbox = outbox;
        _logger = logger;
    }

    public async Task NotifyUserTrackedAsync(UserTrackingSession session, string cameraId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (session is null) throw new ArgumentNullException(nameof(session));
        if (string.IsNullOrWhiteSpace(cameraId)) throw new ArgumentException("cameraId is required.", nameof(cameraId));

        var userId = session.UserId;

        var payload = new UserTrackedNotification
        {
            CameraId = cameraId,
            Session = session,
            TimestampUtc = DateTime.UtcNow
        };

        await _outbox.EnqueueAsync(
            aggregateType: "tracking",
            aggregateId: userId.ToString(),
            topic: "tracking",
            @event: "user_tracked",
            scope: "user",
            scopeKey: userId.ToString(),
            payload: payload,
            idempotencyKey: $"tracking.user_tracked:{userId}:{session.AvgSimilarity}",
            ct: ct);

        _logger.LogInformation("Tracking notify enqueued in outbox for user {UserId}, camera {CameraId}", userId, cameraId);
    }
}
