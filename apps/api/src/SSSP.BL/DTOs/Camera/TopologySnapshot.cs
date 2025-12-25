using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSSP.BL.DTOs.Camera
{
    public sealed class TopologySnapshot
    {
        public static readonly TopologySnapshot Empty =
            new(new Dictionary<string, string>(),
                new Dictionary<string, IReadOnlyCollection<string>>(),
                new Dictionary<(string From, string To), double>());

        public TopologySnapshot(
            Dictionary<string, string> cameraToZone,
            Dictionary<string, IReadOnlyCollection<string>> adjacency,
            Dictionary<(string From, string To), double> travelSeconds)
        {
            CameraToZone = cameraToZone;
            Adjacency = adjacency;
            TravelSeconds = travelSeconds;
        }

        public Dictionary<string, string> CameraToZone { get; }
        public Dictionary<string, IReadOnlyCollection<string>> Adjacency { get; }

        // Directed edge weights in seconds
        public Dictionary<(string From, string To), double> TravelSeconds { get; }
    }


}
