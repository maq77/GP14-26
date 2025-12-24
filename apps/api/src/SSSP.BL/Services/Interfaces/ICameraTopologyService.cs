using System.Collections.Generic;
using SSSP.DAL.Models;

namespace SSSP.BL.Interfaces
{
    public interface ICameraTopologyService
    {
        string? GetZoneId(string cameraId);
        IReadOnlyCollection<string> GetNeighborCameras(string cameraId);

        // Visualze or export: and helper 
        IReadOnlyDictionary<string, string> GetCameraZones();
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> GetGraph();

        // Weighted edge: travel time in seconds between cameras
        double? GetTravelSeconds(string fromCameraId, string toCameraId);

        // For warmup / reload / API
        void LoadFromDatabase(IReadOnlyList<Camera> cameras);
    }
}
