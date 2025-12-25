using System.Text;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using SSSP.BL.Managers;
using SSSP.BL.Services;
using SSSP.BL.Services.Interfaces;
using SSSP.DAL.Context;
using SSSP.DAL.Models;
using SSSP.Infrastructure.AI.Grpc.Clients;
using SSSP.Infrastructure.AI.Grpc.Config;
using SSSP.Infrastructure.AI.Grpc.Health;
using SSSP.Infrastructure.AI.Grpc.Interfaces;
using SSSP.Infrastructure.Persistence.Interfaces;
using SSSP.Infrastructure.Persistence.Repos;
using SSSP.Infrastructure.Persistence.UnitOfWork;
using SSSP.BL.Options;
using SSSP.BL.Interfaces;
using SSSP.BL.Managers.Interfaces;
using SSSP.BL.HealthChecks;
using Polly;
using SSSP.Api.Extentions;
using SSSP.BL.Startup;
using System.Threading.RateLimiting;
using SSSP.Api.Middleware;
using SSSP.Api.Hubs;
using SSSP.Api.Services;
using SSSP.BL.Monitoring;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// =======================================
// Serilog Configuration (Production-Grade)
// =======================================
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "SSSP")
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .Enrich.WithThreadId()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/sssp-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// =======================================
// Application Insights (Local Monitoring)
// =======================================
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
    options.EnableAdaptiveSampling = true;
    options.EnablePerformanceCounterCollectionModule = true;
});

try
{
    Log.Information("========================================");
    Log.Information("SSSP Application Starting");
    Log.Information("Environment: {Environment}", builder.Environment.EnvironmentName);
    Log.Information("========================================");

    // =======================================
    // CORS
    // =======================================
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    // =======================================
    // Controllers & API
    // =======================================
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = null;
            options.JsonSerializerOptions.WriteIndented = false;
            options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        });

    builder.Services.AddEndpointsApiExplorer();

    // =======================================
    // Swagger
    // =======================================
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "SSSP Face Recognition API",
            Version = "v1.0",
            Description = "Enterprise Face Recognition & Camera Monitoring System",
            Contact = new OpenApiContact
            {
                Name = "SSSP Team",
                Email = "support@sssp.com"
            }
        });

        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter 'Bearer' [space] and then your token"
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    // =======================================
    // AI Configuration
    // =======================================
    builder.Services
        .AddOptions<AIOptions>()
        .Bind(builder.Configuration.GetSection("AI"))
        .Validate(opt => !string.IsNullOrWhiteSpace(opt.GrpcUrl), "AI:GrpcUrl is required")
        .Validate(opt => !string.IsNullOrWhiteSpace(opt.RestUrl), "AI:RestUrl is required")
        .ValidateOnStart();
    // =======================================
    // Camera Topology Configuration
    // =======================================
    builder.Services
    .AddOptions<CameraTopologyOptions>()
    .Bind(builder.Configuration.GetSection("CameraTopology"))
    .ValidateOnStart();

    // =======================================
    // Startup Validation Options
    // =======================================
    builder.Services
        .AddOptions<StartupValidationOptions>()
        .Bind(builder.Configuration.GetSection("StartupValidation"))
        .ValidateOnStart();

    builder.Services
    .AddOptions<FaceRecognitionOptions>()
    .Bind(builder.Configuration.GetSection("FaceRecognition"))
    .ValidateOnStart();



    // =======================================
    // Database
    // =======================================
    var connectionString = builder.Configuration.GetConnectionString("MyConOnline")  // changed my con to my con online
        ?? throw new InvalidOperationException("Connection string 'MyCon' not found");

    builder.Services.AddDbContext<AppDbContext>(options =>
    {
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
            sqlOptions.CommandTimeout(30);
        });

        if (builder.Environment.IsDevelopment())
        {
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
        }
    });

    // =======================================
    // Identity
    // =======================================
    builder.Services
        .AddIdentity<User, Role>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 8;
            options.User.RequireUniqueEmail = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

    // =======================================
    // JWT Authentication
    // =======================================
    var secretKey = builder.Configuration.GetValue<string>("SecretKey")
        ?? throw new InvalidOperationException("SecretKey not configured");

    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = "CollSchema1";
            options.DefaultChallengeScheme = "CollSchema1";
        })
        .AddJwtBearer("CollSchema1", options =>
        {
            var secretKeyInBytes = Encoding.ASCII.GetBytes(secretKey);
            var securityKey = new SymmetricSecurityKey(secretKeyInBytes);

            options.TokenValidationParameters = new TokenValidationParameters
            {
                IssuerSigningKey = securityKey,
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.Zero
            };

            options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    Log.Warning("JWT authentication failed. Error={Error}", context.Exception.Message);
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    Log.Debug("JWT token validated for user {User}", context.Principal?.Identity?.Name);
                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAuthorization();

    // =======================================
    // Cache Configuration (env aware)
    // =======================================

    builder.Services.AddSingleton<FaceProfileCacheMetrics>();

    builder.Services.Configure<FaceProfileCacheOptions>(cfg =>
    {
        cfg.AbsoluteExpiration = TimeSpan.FromMinutes(5);
    });

    var useRedisFaceCache = builder.Configuration.GetValue<bool>("Cache:UseRedisFaceCache", false);

    if (useRedisFaceCache)
    {
        var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";

        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnection;
            options.InstanceName = "SSSP:";
        });

        Log.Information("Using Hybrid face cache (L1 + l2(Redis) + DB). Connection={RedisConnection}", redisConnection);

        // L2 concrete
        builder.Services.AddScoped<DistributedFaceProfileCache>();

        // L1 Hybrid as the IFaceProfileCache
        builder.Services.AddMemoryCache(options =>
        {
            options.SizeLimit = 1024;
            options.CompactionPercentage = 0.25;
        });

        builder.Services.AddScoped<IFaceProfileCache, HybridFaceProfileCache>();
    }
    else
    {
        Log.Information("Using in-memory FaceProfileCache (Redis disabled)");

        builder.Services.AddScoped<IFaceProfileCache, FaceProfileCache>();
    }



    // =======================================
    // Repository Pattern
    // =======================================
    builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
    builder.Services.AddScoped(typeof(IRepository<,>), typeof(Repository<,>));

    // =======================================
    // Domain Services
    // =======================================
    builder.Services.AddScoped<IUserService, UserService>();
    builder.Services.AddScoped<IRoleService, RoleService>();
    builder.Services.AddScoped<ISensorService, SensorService>();
    builder.Services.AddScoped<ICameraService, CameraService>();
    builder.Services.AddSingleton<IIncidentManager, IncidentManager>();
    builder.Services.AddScoped<IIncidentService, IncidentService>();
    builder.Services.AddSingleton<ICameraTopologyService, CameraTopologyService>();



    // =======================================
    // HTTP Client
    // =======================================
    builder.Services.AddHttpClient("SSSP", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Add("User-Agent", "SSSP/1.0");
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        MaxConnectionsPerServer = 20
    });

    // =======================================
    // gRPC Infrastructure
    // =======================================
    builder.Services.AddSingleton<GrpcChannelFactory>();
    builder.Services.AddSingleton<IAIFaceClient, AIFaceClient>();
    builder.Services.AddSingleton<IVideoStreamClient, VideoStreamClient>();


    // =======================================
    // Face Recognition Services
    // =======================================
    builder.Services.AddSingleton<IFaceMatchingManager>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<FaceMatchingManager>>();
        var threshold = builder.Configuration.GetValue<double>("FaceRecognition:SimilarityThreshold", 0.65);
        Log.Information("Face matching threshold configured: {Threshold}", threshold);
        return new FaceMatchingManager(threshold, logger);
    });

    // DI of SignalR - Notifiaction System - Realtime
    builder.Services.AddSingleton<ITrackingNotificationService, TrackingNotificationService>();

    builder.Services.AddScoped<IFaceTrackingManager, FaceTrackingManager>();
    builder.Services.AddScoped<IFaceAutoEnrollmentService, FaceAutoEnrollmentService>();
    builder.Services.AddScoped<IFaceEnrollmentService, FaceEnrollmentService>();
    builder.Services.AddScoped<IFaceRecognitionService, FaceRecognitionService>();
    builder.Services.AddScoped<IFaceManagementService, FaceManagementService>();

    // =======================================
    // Camera Monitoring
    // =======================================
    builder.Services.AddSingleton<CameraMonitoringWorker>();
    builder.Services.AddSingleton<ICameraMonitoringService>(sp =>
        sp.GetRequiredService<CameraMonitoringWorker>());
    builder.Services.AddHostedService(sp =>
        sp.GetRequiredService<CameraMonitoringWorker>());
    builder.Services.AddHostedService<StartupValidationService>();
    builder.Services.AddHostedService<FaceProfileCacheWarmupService>();
    builder.Services.AddHostedService<CameraTopologyWarmupService>();




    // =======================================
    // SignalR
    // =======================================
    builder.Services.AddSignalR(options =>
    {
        options.EnableDetailedErrors = builder.Environment.IsDevelopment();
        options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    });

    // =======================================
    // Health Checks
    // =======================================
    builder.Services
        .AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy("API is running"), tags: new[] { "live" })
        .AddDbContextCheck<AppDbContext>("database", tags: new[] { "ready", "db" })
        .AddSqlServer(
            connectionString: connectionString,
            name: "sqlserver",
            failureStatus: HealthStatus.Unhealthy,
            tags: new[] { "ready", "db", "sql" })
        .AddCheck<AiGrpcHealthCheck>("ai_grpc", tags: new[] { "ready", "ai" })
        .AddCheck<CameraMonitoringHealthCheck>("camera_monitoring", tags: new[] { "ready", "cameras" })
        .AddCheck<FaceProfileCacheHealthCheck>("face_cache", tags: new[] { "ready", "cache" });

    builder.Services
        .AddHealthChecksUI(options =>
        {
            options.SetEvaluationTimeInSeconds(30);
            options.MaximumHistoryEntriesPerEndpoint(100);
            options.AddHealthCheckEndpoint("SSSP API - Ready", "http://api:8080/health/ready");
            options.AddHealthCheckEndpoint("SSSP API - Live", "http://api:8080/health/live");

            // options.AddHealthCheckEndpoint("SSSP API - Ready", "/health/ready"); //local
            // options.AddHealthCheckEndpoint("SSSP API - Live", "/health/live");  //local
        })
        .AddInMemoryStorage();

    // =======================================
    // Response Compression
    // =======================================
    builder.Services.AddResponseCompression(options =>
    {
        options.EnableForHttps = true;
    });

    // =======================================
    // Polly Resilience Policies (Circuit Breaker)
    // =======================================
    builder.Services.AddSingleton<Polly.IAsyncPolicy>(provider =>
    {
        return Polly.Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    Log.Warning(
                        "Retry {RetryCount} after {Delay}s due to {Exception}",
                        retryCount, timeSpan.TotalSeconds, exception.Message);
                });
    });

    builder.Services.AddSingleton<Polly.CircuitBreaker.ICircuitBreakerPolicy>(provider =>
    {
        return Polly.Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromMinutes(1),
                onBreak: (exception, duration) =>
                {
                    Log.Error("Circuit breaker opened for {Duration}s due to {Exception}",
                        duration.TotalSeconds, exception.Message);
                },
                onReset: () =>
                {
                    Log.Information("Circuit breaker reset");
                });
    });

    // =======================================
    // Rate Limiter
    // =======================================
    builder.Services.AddRateLimiter(options =>
    {
        // 1) Global default policy: per IP, fixed window
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        {
            var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: ip,
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,            // 100 requests...
                    Window = TimeSpan.FromMinutes(1), // ...per 1 minute per IP
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                });
        });

        // 2) Named policy for face APIs (stricter)
        options.AddPolicy("face-api", httpContext =>
        {
            // You can partition by user ID / client ID if you add claims later
            var key =
                httpContext.User.Identity?.Name
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "anonymous";

            return RateLimitPartition.GetTokenBucketLimiter(
                partitionKey: key,
                factory: _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 60,                     // burst size
                    TokensPerPeriod = 60,                // refill 60 tokens...
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1), // ...every 1 min
                    AutoReplenishment = true,
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                });
        });

        // Optional: nice 429 response
        options.OnRejected = async (context, token) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("RateLimiting");

            logger.LogWarning(
                "Rate limit exceeded. Path={Path}, IP={IP}",
                context.HttpContext.Request.Path.Value,
                context.HttpContext.Connection.RemoteIpAddress?.ToString());

            await context.HttpContext.Response.WriteAsJsonAsync(new
            {
                error = "too_many_requests",
                message = "Too many requests. Please slow down."
            }, cancellationToken: token);
        };
    });


    // =======================================
    // Build Application
    // =======================================
    var app = builder.Build();

    // =======================================
    // Startup Validation
    // =======================================
    using (var scope = app.Services.CreateScope())
    {
        var aiOptions = scope.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<AIOptions>>()
            .Value;

        Log.Information("AI Configuration:");
        Log.Information("  - REST URL: {RestUrl}", aiOptions.RestUrl);
        Log.Information("  - gRPC URL: {GrpcUrl}", aiOptions.GrpcUrl);

        try
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var canConnect = await dbContext.Database.CanConnectAsync();

            if (canConnect)
            {
                Log.Information("Database connection: OK");

                var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
                if (pendingMigrations.Any())
                {
                    Log.Warning("Pending database migrations: {Count}", pendingMigrations.Count());
                    foreach (var migration in pendingMigrations)
                    {
                        Log.Warning("  - {Migration}", migration);
                    }
                }
            }
            else
            {
                Log.Error("Database connection: FAILED");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Database validation failed");
        }
    }

    // =======================================
    // Middleware Pipeline
    // =======================================
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseExceptionHandler("/error");
        app.UseHsts();
    }

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseResponseCompression();

    app.UseRateLimiter();

    app.UsePerformanceMonitoring();
    app.UseGlobalExceptionHandler();

    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
            diagnosticContext.Set("RemoteIP", httpContext.Connection.RemoteIpAddress?.ToString());
        };
    });

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "SSSP API v1");
            c.RoutePrefix = "swagger";
        });
    }

    app.UseHttpsRedirection();
    app.UseCors("AllowAll");
    app.UseAuthentication();
    app.UseAuthorization();
    
    app.MapControllers();
    app.MapHub<TrackingHub>(TrackingHub.HubUrl);
    // Prometheus metrics endpoint (for scraping)
    app.MapMetrics("/metrics");


    // =======================================
    // Health Check Endpoints
    // =======================================
    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("live"),
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
        AllowCachingResponses = false
    });

    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
        AllowCachingResponses = false
    });

    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        Predicate = _ => true,
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
        AllowCachingResponses = false
    });

    app.UseHealthChecksUI(options =>
    {
        options.UIPath = "/health-ui";
        options.ApiPath = "/health-ui-api";
    });

    // =======================================
    // Global Error Endpoint
    // =======================================
    app.Map("/error", (HttpContext context) =>
    {
        Log.Error("Unhandled exception occurred");
        return Results.Problem(title: "An error occurred", statusCode: 500);
    });

    // =======================================
    // Startup Complete
    // =======================================
    Log.Information("========================================");
    Log.Information("SSSP Application Started Successfully");
    Log.Information("Listening on: {Urls}", string.Join(", ", app.Urls));
    if (app.Environment.IsDevelopment())
    {
        Log.Information("Swagger UI: {Url}/swagger", app.Urls.FirstOrDefault() ?? "http://localhost:5000");
    }
    Log.Information("Health UI: {Url}/health-ui", app.Urls.FirstOrDefault() ?? "http://localhost:5000");
    Log.Information("========================================");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.Information("Application shutting down");
    Log.CloseAndFlush();
}