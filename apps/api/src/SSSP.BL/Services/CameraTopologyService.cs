using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SSSP.BL.Interfaces;
using SSSP.BL.Options;
using SSSP.DAL.Models;
using SSSP.BL.DTOs.Camera;

public sealed class CameraTopologyService : ICameraTopologyService, IDisposable
{
    private readonly ILogger<CameraTopologyService> _logger;
    private readonly CameraTopologyOptions _options;

    private readonly SemaphoreSlim _lock = new(1, 1);
    private volatile TopologySnapshot _snapshot = TopologySnapshot.Empty;
    private int _disposed;

    public CameraTopologyService(
        IOptions<CameraTopologyOptions> options,
        ILogger<CameraTopologyService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }
    public void LoadFromDatabase(IReadOnlyList<Camera> cameras)
    {
        _lock.Wait();
        try
        {
            var cameraToZone = cameras
                .Where(c => c.IsActive)
                .ToDictionary(
                    c => c.Id.ToString(),
                    c => string.IsNullOrWhiteSpace(c.ZoneId) ? "Default" : c.ZoneId,
                    StringComparer.OrdinalIgnoreCase);

            var adjacency = BuildAdjacency(cameraToZone);
            var travelTimes = BuildTravelTimes(cameraToZone);

            _snapshot = new TopologySnapshot(cameraToZone, adjacency, travelTimes);

            _logger.LogInformation(
                "Camera topology loaded. Cameras={Cameras}, Zones={Zones}, WeightedEdges={Edges}",
                cameraToZone.Count,
                cameraToZone.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                travelTimes.Count);
        }
        finally
        {
            _lock.Release();
        }
    }
    public string? GetZoneId(string cameraId)
    {
        ThrowIfDisposed();

        var snap = _snapshot;
        return snap.CameraToZone.TryGetValue(cameraId, out var zone) ? zone : null;
    }

    public IReadOnlyCollection<string> GetNeighborCameras(string cameraId)
    {
        ThrowIfDisposed();

        var snap = _snapshot;

        if (snap.Adjacency.TryGetValue(cameraId, out var neighbors))
            return neighbors;

        return Array.Empty<string>();
    }

    public IReadOnlyDictionary<string, string> GetCameraZones()
    => _snapshot.CameraToZone;

    public double? GetTravelSeconds(string fromCameraId, string toCameraId)
    {
        ThrowIfDisposed();

        var snap = _snapshot;
        var key = (From: fromCameraId, To: toCameraId);

        if (snap.TravelSeconds.TryGetValue(key, out var seconds))
            return seconds;

        return null;
    }

    public IReadOnlyDictionary<string, IReadOnlyCollection<string>> GetGraph()
        => _snapshot.Adjacency;

    private Dictionary<string, IReadOnlyCollection<string>> BuildAdjacency(
        Dictionary<string, string> cameraToZone)
    {
        var result = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase);

        // 1) Same-zone adjacency
        if (_options.SameZoneIsNeighbor)
        {
            var byZone = cameraToZone.GroupBy(k => k.Value);

            foreach (var zone in byZone)
            {
                var ids = zone.Select(x => x.Key).ToList();

                foreach (var id in ids)
                    result[id] = ids.Where(x => x != id).ToList();
            }
        }

        // 2) Explicit adjacency overrides
        foreach (var kvp in _options.Adjacency)
        {
            var from = kvp.Key;

            if (!cameraToZone.ContainsKey(from))
                continue;

            if (!result.TryGetValue(from, out var existing))
                existing = Array.Empty<string>();

            var merged = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

            foreach (var n in kvp.Value)
                if (cameraToZone.ContainsKey(n) && n != from)
                    merged.Add(n);

            result[from] = merged.ToList();
        }

        return result;
    }

    private Dictionary<(string From, string To), double> BuildTravelTimes(
    Dictionary<string, string> cameraToZone)
    {
        var result = new Dictionary<(string From, string To), double>();

        foreach (var fromKvp in _options.TravelSeconds)
        {
            var from = fromKvp.Key;

            if (!cameraToZone.ContainsKey(from))
                continue; // ignore edges for unknown cameras

            foreach (var toKvp in fromKvp.Value)
            {
                var to = toKvp.Key;
                var seconds = toKvp.Value;

                if (!cameraToZone.ContainsKey(to))
                    continue;

                var key = (From: from, To: to);

                // last write wins; usually config won't duplicate
                result[key] = seconds;
            }
        }

        return result;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed != 0)
            throw new ObjectDisposedException(nameof(CameraTopologyService));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _lock.Dispose();
        _logger.LogDebug("CameraTopologyService disposed");
    }
}
