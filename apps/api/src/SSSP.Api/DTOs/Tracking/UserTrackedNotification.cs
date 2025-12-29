namespace SSSP.BL.DTOs.Tracking;

public sealed class UserTrackedNotification
{
    public string CameraId { get; set; } = default!;
    public UserTrackingSession Session { get; set; } = default!;
    public DateTime TimestampUtc { get; set; }
}
