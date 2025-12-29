namespace SSSP.Api.Realtime.Contracts.Faces;

public static class FaceTopics
{
    public const string Topic = "faces";
    public const string Recognized = "recognized";
    public const string Enrolled = "enrolled";
    public const string Updated = "updated";
}

public sealed record FaceRecognizedPayload(
    string CameraId,
    Guid? UserId,
    string DisplayName,
    double Similarity,
    DateTimeOffset TsUtc
);
