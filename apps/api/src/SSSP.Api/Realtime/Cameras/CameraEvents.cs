/*namespace SSSP.Api.Realtime.Contracts.Cameras;

public static class CameraTopics
{
    public const string Topic = "cameras";

    public const string Status = "status";           // online/offline/fps...
    public const string Tracking = "tracking";       // face tracking updates
    public const string Updated = "updated";
}

public sealed record CameraStatusPayload(
    string CameraId,
    bool IsOnline,
    double? Fps = null,
    string? Message = null
);

public sealed record CameraTrackingPayload(
    string CameraId,
    string TrackId,
    string Label,
    double Confidence,
    object? Box // keep flexible for now (x,y,w,h) or typed later
);
*/