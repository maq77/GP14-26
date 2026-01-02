namespace SSSP.Api.Realtime.Contracts.Cameras;

public static class CameraTopics
{
    public const string Topic = "camera";
    public const string Status = "status";
    public const string Tracking = "tracking";
    public const string CrossCameraReId = "cross_camera_reid"; 
}

public sealed record CameraTrackingPayload(
    string CameraId,
    Guid UserId,
    Guid FaceProfileId,
    string DisplayName,
    double AvgSimilarity,
    int TotalSightings,
    List<string> SeenCameras,
    Dictionary<string, DateTimeOffset> VisitedZones,
    DateTimeOffset FirstSeenUtc,
    DateTimeOffset LastSeenUtc
);

public sealed record CrossCameraReIdPayload(
    Guid UserId,
    string DisplayName,
    string FromCameraId,
    string ToCameraId,
    string ZoneId,
    double TravelSeconds,
    double Similarity,
    DateTimeOffset TsUtc
);

public sealed record CameraStatusPayload(
    string CameraId,
    bool IsOnline,
    double? Fps = null,
    string? Message = null
);