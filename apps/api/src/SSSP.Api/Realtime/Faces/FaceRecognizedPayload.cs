namespace SSSP.Api.Realtime.Contracts.Faces;

public static class FaceTopics
{
    public const string Topic = "faces";
    public const string Recognized = "recognized";
    public const string Enrolled = "enrolled";
    public const string Updated = "updated";
    public const string Unknown = "unknown"; //  dev mode
}

// Bounding box for visualization
public sealed record BoundingBox(
    int X,
    int Y,
    int Width,
    int Height
);

// Face quality metrics
public sealed record FaceQuality(
    float OverallScore,
    float Sharpness,
    float Brightness,
    float Contrast
);

// Full payload with all details
public sealed record FaceRecognizedPayload(
    string CameraId,
    string FrameId,
    Guid? UserId,
    Guid? FaceProfileId,
    string? DisplayName,
    double Similarity,
    double Confidence,
    BoundingBox BBox,
    FaceQuality? Quality,
    string? TrackingId,
    DateTimeOffset TsUtc
);