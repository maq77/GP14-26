using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SSSP.BL.Managers.Interfaces;


namespace SSSP.BL.BackgroundJobs
{
    public sealed class TrackingCleanupWorker : BackgroundService
    {
        private readonly IFaceTrackingManager _trackingManager;
        private readonly ILogger<TrackingCleanupWorker> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(1);
        private readonly TimeSpan _expiration = TimeSpan.FromMinutes(5);


        public TrackingCleanupWorker(IFaceTrackingManager trackingManager, ILogger<TrackingCleanupWorker> logger)
        {
            _trackingManager = trackingManager;
            _logger = logger;
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("TrackingCleanupWorker started.");


            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _trackingManager.CleanupExpired();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during tracking cleanup.");
                }


                await Task.Delay(_interval, stoppingToken);
            }


            _logger.LogInformation("TrackingCleanupWorker stopped.");
        }
    }
}