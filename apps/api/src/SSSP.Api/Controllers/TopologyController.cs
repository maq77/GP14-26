using Microsoft.AspNetCore.Mvc;
using SSSP.BL.Interfaces;
using SSSP.DAL.Models;
using SSSP.Infrastructure.Persistence.Interfaces;
using Microsoft.EntityFrameworkCore;
using SSSP.BL.DTOs;


[ApiController]
[Route("api/topology")]
public sealed class TopologyController : ControllerBase
{
    private readonly ICameraTopologyService _topology;
    private readonly IServiceScopeFactory _scopeFactory;

    public TopologyController(ICameraTopologyService topology, IServiceScopeFactory scopeFactory)
    {
        _topology = topology;
        _scopeFactory = scopeFactory;
    }

    [HttpGet("graph")]
    [ProducesResponseType(typeof(CameraTopologyGraphDTO), StatusCodes.Status200OK)]
    public IActionResult GetGraph()
    {
        var zones = _topology.GetCameraZones();
        var adjacency = _topology.GetGraph();

        // Flatten weights 
        var edges = new List<CameraEdgeDTO>();

        foreach (var from in adjacency.Keys)
        {
            var neighbors = adjacency[from];

            foreach (var to in neighbors)
            {
                var weight = _topology.GetTravelSeconds(from, to);

                // deafult
                edges.Add(new CameraEdgeDTO
                {
                    FromCameraId = from,
                    ToCameraId = to,
                    TravelSeconds = weight ?? 1.0  // default 1s 
                });
            }
        }

        var dto = new CameraTopologyGraphDTO
        {
            CameraToZone = zones,
            Adjacency = adjacency,
            Edges = edges
        };

        return Ok(dto);
    }

    [HttpPost("reload")]
    public async Task<IActionResult> Reload(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var cameras = await uow.GetRepository<Camera, int>()
                               .Query
                               .ToListAsync(ct);

        _topology.LoadFromDatabase(cameras);
        return Ok(new { status = "ok" });
    }
}
