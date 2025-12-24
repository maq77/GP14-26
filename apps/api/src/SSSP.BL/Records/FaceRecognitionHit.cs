namespace SSSP.BL.Records
{
    public sealed record FaceRecognitionHit(
        int? FaceId,
        FaceBoundingBox Bbox,
        FaceMatchResult Match,
        float OverallQuality);
}
