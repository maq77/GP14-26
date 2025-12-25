using System.Collections.Generic;
using SSSP.BL.DTOs.Faces;
using SSSP.BL.Records;
using SSSP.DAL.Models;

namespace SSSP.BL.Managers.Interfaces
{
    public interface IFaceMatchingManager
    {
        double DefaultThreshold { get; }
        FaceMatchResult Match(
            IReadOnlyList<float> probeEmbedding,
            IReadOnlyList<FaceProfileSnapshot> knownProfiles,
            double? thresholdOverride = null);
    }
}
