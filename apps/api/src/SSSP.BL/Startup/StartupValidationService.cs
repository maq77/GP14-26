using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SSSP.BL.Options;
using SSSP.DAL.Context;
using SSSP.Infrastructure.AI.Grpc.Config;

namespace SSSP.BL.Startup
{
    public sealed class StartupValidationService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<StartupValidationService> _logger;
        private readonly IHostApplicationLifetime _lifetime;
        private readonly IHostEnvironment _environment;
        private readonly StartupValidationOptions _options;
        private readonly TelemetryClient? _telemetry;

        public StartupValidationService(
            IServiceProvider serviceProvider,
            ILogger<StartupValidationService> logger,
            IHostApplicationLifetime lifetime,
            IHostEnvironment environment,
            IOptions<StartupValidationOptions> options,
            TelemetryClient? telemetry = null)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _lifetime = lifetime;
            _environment = environment;
            _options = options.Value;
            _telemetry = telemetry;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation("Startup validation is disabled via configuration. Skipping checks.");
                TrackEventSafe("StartupValidationSkipped", new()
                {
                    ["Environment"] = _environment.EnvironmentName
                });
                return;
            }

            _logger.LogInformation("========================================");
            _logger.LogInformation("Starting startup validation checks");
            _logger.LogInformation("Environment: {Env}", _environment.EnvironmentName);
            _logger.LogInformation("========================================");

            var validationFailed = false;

            using var scope = _serviceProvider.CreateScope();

            validationFailed |= !await ValidateDatabaseAsync(scope, cancellationToken);
            validationFailed |= !ValidateAIConfiguration(scope);
            validationFailed |= !ValidateRequiredServices(scope);

            _logger.LogInformation("========================================");

            if (validationFailed)
            {
                var isProd = _environment.IsProduction();
                var failFast =
                    isProd || (_options.FailFastInDevelopment && _environment.IsDevelopment());

                _logger.LogCritical(
                    "Startup validation FAILED. Environment={Environment}, FailFast={FailFast}",
                    _environment.EnvironmentName, failFast);

                TrackEventSafe("StartupValidationFailed", new()
                {
                    ["Environment"] = _environment.EnvironmentName,
                    ["FailFast"] = failFast.ToString()
                });

                if (failFast)
                {
                    _lifetime.StopApplication();
                }
                else
                {
                    _logger.LogWarning(
                        "Validation failed but application will continue running for debugging (Environment={Environment})",
                        _environment.EnvironmentName);
                }

                return;
            }

            _logger.LogInformation("Startup validation completed successfully");
            _logger.LogInformation("========================================");

            TrackEventSafe("StartupValidationSucceeded", new()
            {
                ["Environment"] = _environment.EnvironmentName
            });
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private async Task<bool> ValidateDatabaseAsync(IServiceScope scope, CancellationToken cancellationToken)
        {
            try
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var maxRetries = _environment.IsProduction()
                    ? _options.MaxDbRetriesProduction
                    : _options.MaxDbRetriesDevelopment;

                maxRetries = Math.Max(maxRetries, 1);
                var delay = TimeSpan.FromSeconds(Math.Max(_options.DbRetryDelaySeconds, 1));

                for (var attempt = 1; attempt <= maxRetries; attempt++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        _logger.LogInformation(
                            "Validating database connection... (Attempt {Attempt}/{Max})",
                            attempt, maxRetries);

                        var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

                        if (!canConnect)
                        {
                            _logger.LogError("Database connection FAILED on attempt {Attempt}", attempt);
                        }
                        else
                        {
                            _logger.LogInformation("Database connection OK");

                            var pendingMigrations =
                                await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);

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

                            TrackEventSafe("StartupValidation_DatabaseSucceeded", new()
                            {
                                ["Environment"] = _environment.EnvironmentName,
                                ["Attempts"] = attempt.ToString()
                            });

                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Database validation failed on attempt {Attempt}/{Max}",
                            attempt, maxRetries);
                    }

                    if (attempt < maxRetries)
                    {
                        _logger.LogWarning(
                            "Retrying database validation after {DelaySeconds}s...",
                            delay.TotalSeconds);

                        try
                        {
                            await Task.Delay(delay, cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            _logger.LogWarning("Database validation cancelled while waiting to retry.");
                            return false;
                        }
                    }
                }

                _logger.LogError(
                    "Database validation FAILED after {MaxRetries} attempts",
                    maxRetries);

                TrackEventSafe("StartupValidation_DatabaseFailed", new()
                {
                    ["Environment"] = _environment.EnvironmentName,
                    ["MaxRetries"] = maxRetries.ToString()
                });

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database validation failed with unexpected error");
                TrackEventSafe("StartupValidation_DatabaseException", new()
                {
                    ["Environment"] = _environment.EnvironmentName,
                    ["ExceptionType"] = ex.GetType().Name
                });
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
                    TrackEventSafe("StartupValidation_AIConfigFailed", new()
                    {
                        ["Environment"] = _environment.EnvironmentName,
                        ["Reason"] = "MissingGrpcUrl"
                    });
                    return false;
                }

                if (string.IsNullOrWhiteSpace(aiOptions.RestUrl))
                {
                    _logger.LogError("AI:RestUrl not configured");
                    TrackEventSafe("StartupValidation_AIConfigFailed", new()
                    {
                        ["Environment"] = _environment.EnvironmentName,
                        ["Reason"] = "MissingRestUrl"
                    });
                    return false;
                }

                _logger.LogInformation("AI Configuration:");
                _logger.LogInformation(" - gRPC URL: {GrpcUrl}", aiOptions.GrpcUrl);
                _logger.LogInformation(" - REST URL: {RestUrl}", aiOptions.RestUrl);

                TrackEventSafe("StartupValidation_AIConfigSucceeded", new()
                {
                    ["Environment"] = _environment.EnvironmentName
                });

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI configuration validation failed");
                TrackEventSafe("StartupValidation_AIConfigException", new()
                {
                    ["Environment"] = _environment.EnvironmentName,
                    ["ExceptionType"] = ex.GetType().Name
                });
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

                        TrackEventSafe("StartupValidation_ServiceMissing", new()
                        {
                            ["Environment"] = _environment.EnvironmentName,
                            ["ServiceType"] = serviceType.FullName ?? serviceType.Name
                        });

                        return false;
                    }
                }

                _logger.LogInformation("All required services registered");

                TrackEventSafe("StartupValidation_ServicesSucceeded", new()
                {
                    ["Environment"] = _environment.EnvironmentName
                });

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Service validation failed");
                TrackEventSafe("StartupValidation_ServicesException", new()
                {
                    ["Environment"] = _environment.EnvironmentName,
                    ["ExceptionType"] = ex.GetType().Name
                });
                return false;
            }
        }

        private void TrackEventSafe(string eventName, System.Collections.Generic.Dictionary<string, string> properties)
        {
            if (!_options.EmitTelemetry || _telemetry == null)
                return;

            try
            {
                _telemetry.TrackEvent(eventName, properties);
            }
            catch (Exception ex)
            {
                // Never let telemetry crash startup; just log at debug level
                _logger.LogDebug(ex, "Failed to send telemetry event {EventName}", eventName);
            }
        }
    }
}
