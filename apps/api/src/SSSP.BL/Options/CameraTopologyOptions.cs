using System.Collections.Generic;

namespace SSSP.BL.Options
{
    public sealed class CameraTopologyOptions
    {
        public bool SameZoneIsNeighbor { get; set; } = true;
        public Dictionary<string, List<string>> Adjacency { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, Dictionary<string, double>> TravelSeconds { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }
}
