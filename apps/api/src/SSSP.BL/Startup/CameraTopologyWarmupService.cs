using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SSSP.Infrastructure.Persistence.Interfaces;
using SSSP.DAL.Models;
using SSSP.BL.Interfaces;

public sealed class CameraTopologyWarmupService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CameraTopologyWarmupService> _logger;

    public CameraTopologyWarmupService(
        IServiceScopeFactory scopeFactory,
        ILogger<CameraTopologyWarmupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        _logger.LogInformation("Camera topology warmup started...");

        using var scope = _scopeFactory.CreateScope();

        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var topology = scope.ServiceProvider.GetRequiredService<ICameraTopologyService>();

        var cameras = await uow.GetRepository<Camera, int>()
                               .Query
                               .ToListAsync(ct);

        topology.LoadFromDatabase(cameras);

        _logger.LogInformation("Camera topology warmup finished. Cameras loaded={Count}", cameras.Count);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
