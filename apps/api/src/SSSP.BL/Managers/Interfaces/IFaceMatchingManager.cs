using System.Collections.Generic;
using SSSP.BL.Records;
using SSSP.DAL.Models;

namespace SSSP.BL.Managers.Interfaces
{
    public interface IFaceMatchingManager
    {
        double DefaultThreshold { get; }
        FaceMatchResult Match(
            IReadOnlyList<float> probeEmbedding,
            IEnumerable<FaceProfile> knownProfiles,
            double? thresholdOverride = null);
    }
}
