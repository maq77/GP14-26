using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SSSP.DAL.Context;
using SSSP.Infrastructure.AI.Grpc.Config;

namespace SSSP.BL.Startup
{
    public sealed class StartupValidationService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<StartupValidationService> _logger;
        private readonly IHostApplicationLifetime _lifetime;

        public StartupValidationService(
            IServiceProvider serviceProvider,
            ILogger<StartupValidationService> logger,
            IHostApplicationLifetime lifetime)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _lifetime = lifetime;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("========================================");
            _logger.LogInformation("Starting startup validation checks");
            _logger.LogInformation("========================================");

            var validationFailed = false;

            using var scope = _serviceProvider.CreateScope();

            validationFailed |= !await ValidateDatabaseAsync(scope);
            validationFailed |= !ValidateAIConfiguration(scope);
            validationFailed |= !ValidateRequiredServices(scope);

            _logger.LogInformation("========================================");

            if (validationFailed)
            {
                _logger.LogCritical("Startup validation FAILED. Application will not start.");
                _lifetime.StopApplication();
                return;
            }

            _logger.LogInformation("Startup validation completed successfully");
            _logger.LogInformation("========================================");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private async Task<bool> ValidateDatabaseAsync(IServiceScope scope)
        {
            try
            {
                _logger.LogInformation("Validating database connection...");

                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var canConnect = await dbContext.Database.CanConnectAsync();

                if (!canConnect)
                {
                    _logger.LogError("Database connection FAILED");
                    return false;
                }

                _logger.LogInformation("Database connection OK");

                var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
                if (pendingMigrations.Any())
                {
                    _logger.LogWarning("Pending database migrations detected:");
                    foreach (var migration in pendingMigrations)
                    {
                        _logger.LogWarning("   - {Migration}", migration);
                    }
                    _logger.LogWarning("Run: dotnet ef database update");
                }
                else
                {
                    _logger.LogInformation("Database schema up to date");
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database validation failed");
                return false;
            }
        }

        private bool ValidateAIConfiguration(IServiceScope scope)
        {
            try
            {
                _logger.LogInformation("Validating AI configuration...");

                var aiOptions = scope.ServiceProvider
                    .GetRequiredService<IOptions<AIOptions>>()
                    .Value;

                if (string.IsNullOrWhiteSpace(aiOptions.GrpcUrl))
                {
                    _logger.LogError("AI:GrpcUrl not configured");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(aiOptions.RestUrl))
                {
                    _logger.LogError("AI:RestUrl not configured");
                    return false;
                }

                _logger.LogInformation("AI Configuration:");
                _logger.LogInformation(" - gRPC URL: {GrpcUrl}", aiOptions.GrpcUrl);
                _logger.LogInformation(" - REST URL: {RestUrl}", aiOptions.RestUrl);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI configuration validation failed");
                return false;
            }
        }

        private bool ValidateRequiredServices(IServiceScope scope)
        {
            try
            {
                _logger.LogInformation("Validating required services...");

                var requiredServices = new[]
                {
                    typeof(AppDbContext),
                    typeof(SSSP.BL.Services.Interfaces.IFaceRecognitionService),
                    typeof(SSSP.BL.Services.Interfaces.ICameraMonitoringService),
                    typeof(SSSP.BL.Interfaces.IFaceProfileCache)
                };

                foreach (var serviceType in requiredServices)
                {
                    var service = scope.ServiceProvider.GetService(serviceType);
                    if (service == null)
                    {
                        _logger.LogError("Required service not registered: {ServiceType}", serviceType.Name);
                        return false;
                    }
                }

                _logger.LogInformation("All required services registered");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Service validation failed");
                return false;
            }
        }
    }
}