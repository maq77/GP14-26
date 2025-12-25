using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SSSP.BL.Interfaces;

namespace SSSP.BL.Startup
{
    public sealed class FaceProfileCacheWarmupService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<FaceProfileCacheWarmupService> _logger;

        public FaceProfileCacheWarmupService(
            IServiceProvider serviceProvider,
            ILogger<FaceProfileCacheWarmupService> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Fire-and-forget warmup in background
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var cache = scope.ServiceProvider.GetRequiredService<IFaceProfileCache>();

                    _logger.LogInformation("FaceProfile cache warm-up started.");
                    var profiles = await cache.GetAllAsync(cancellationToken);
                    _logger.LogInformation(
                        "FaceProfile cache warm-up completed. Profiles={Count}",
                        profiles.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "FaceProfile cache warm-up failed.");
                }
            }, cancellationToken);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            // Nothing to clean up
            return Task.CompletedTask;
        }
    }
}
