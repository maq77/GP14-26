using System;

namespace SSSP.BL.Records
{
    public sealed record FaceMatchResult(
        bool IsMatch,
        Guid? UserId,
        Guid? FaceProfileId,
        double Similarity
    );
}
