using SSSP.BL.DTOs;

public sealed class CameraTopologyGraphDTO
{
    public IReadOnlyDictionary<string, string> CameraToZone { get; init; } =
        new Dictionary<string, string>();

    public IReadOnlyDictionary<string, IReadOnlyCollection<string>> Adjacency { get; init; } =
        new Dictionary<string, IReadOnlyCollection<string>>();

    // Edge weights in seconds (travel time: from camera to another)
    public List<CameraEdgeDTO> Edges { get; set; } = new();
}
