using System.Text;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
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
using Microsoft.AspNetCore.Builder;
using SSSP.BL.Options;
using SSSP.BL.Interfaces;
using SSSP.BL.Managers.Interfaces;


var builder = WebApplication.CreateBuilder(args);

// =======================================
// Logging (Serilog)
// =======================================
builder.Host.UseSerilog((context, config) =>
{
    config
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console();
});

// =======================================
// Controllers & Swagger
// =======================================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SSSP API",
        Version = "v1"
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
// Options / AI Configuration
// =======================================
builder.Services
    .AddOptions<AIOptions>()
    .Bind(builder.Configuration.GetSection("AI"))
    .Validate(opt => !string.IsNullOrWhiteSpace(opt.GrpcUrl), "AI:GrpcUrl is required")
    .Validate(opt => !string.IsNullOrWhiteSpace(opt.RestUrl), "AI:RestUrl is required")
    .ValidateOnStart();

// =======================================
// Database
// =======================================
var connectionString = builder.Configuration.GetConnectionString("MyCon");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null);
    });
});

// =======================================
// Identity
// =======================================
builder.Services
    .AddIdentity<User, Role>(options =>
    {
        options.Password.RequireDigit = false;
        options.Password.RequireUppercase = false;
        options.Password.RequiredLength = 6;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// =======================================
// Authentication / JWT
// =======================================
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = "CollSchema1";
        options.DefaultChallengeScheme = "CollSchema1";
    })
    .AddJwtBearer("CollSchema1", options =>
    {
        var secretKeyString = builder.Configuration.GetValue<string>("SecretKey") ?? string.Empty;
        var secretKeyInBytes = Encoding.ASCII.GetBytes(secretKeyString);
        var securityKey = new SymmetricSecurityKey(secretKeyInBytes);

        options.TokenValidationParameters = new TokenValidationParameters
        {
            IssuerSigningKey = securityKey,
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true
        };
    });


builder.Services.Configure<FaceProfileCacheOptions>(cfg =>
{
    cfg.AbsoluteExpiration = TimeSpan.FromMinutes(1);
});
// =======================================
// Domain / Infrastructure Services
// =======================================

// UoW & Repos
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped(typeof(IRepository<,>), typeof(Repository<,>));
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<ISensorService, SensorService>();

// HTTP + gRPC Channel factory
builder.Services.AddHttpClient();
builder.Services.AddSingleton<GrpcChannelFactory>();

// AI Clients (singleton, channel shared via factory)
builder.Services.AddSingleton<IAIFaceClient, AIFaceClient>();
builder.Services.AddSingleton<IVideoStreamClient, VideoStreamClient>();


// Face Matching
builder.Services.AddSingleton<IFaceMatchingManager>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<FaceMatchingManager>>();
    var config = sp.GetRequiredService<IConfiguration>();
    var threshold = config.GetValue<double>("FaceRecognition:SimilarityThreshold", 0.6);
    return new FaceMatchingManager(threshold, logger);
});

// Face services
builder.Services.AddScoped<IFaceProfileCache, FaceProfileCache>();
builder.Services.AddScoped<IFaceEnrollmentService, FaceEnrollmentService>();
builder.Services.AddScoped<IFaceRecognitionService, FaceRecognitionService>();
builder.Services.AddScoped<IFaceManagementService, FaceManagementService>();

//Camera Service
builder.Services.AddScoped<ICameraService, CameraService>();


// Camera monitoring as background worker + service
builder.Services.AddSingleton<CameraMonitoringWorker>();
builder.Services.AddSingleton<ICameraMonitoringService>(sp =>
    sp.GetRequiredService<CameraMonitoringWorker>());
builder.Services.AddHostedService(sp =>
    sp.GetRequiredService<CameraMonitoringWorker>());

builder.Services.AddSignalR();

builder.Services
    .AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
    .AddCheck<AiGrpcHealthCheck>("ai_grpc", tags: new[] { "ready" });
builder.Services
    .AddHealthChecksUI(options =>
    {
        options.SetEvaluationTimeInSeconds(30);
        options.MaximumHistoryEntriesPerEndpoint(60);

        options.AddHealthCheckEndpoint("SSSP API", "/health/ready");
    })
    .AddInMemoryStorage();

// =======================================
// Build App
// =======================================
var app = builder.Build();

// Dev-time AI config echo
if (app.Environment.IsDevelopment())
{
    var aiOptions = app.Services
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<AIOptions>>()
        .Value;

    Console.WriteLine($"AI REST: {aiOptions.RestUrl}");
    Console.WriteLine($"AI gRPC: {aiOptions.GrpcUrl}");
}

// Role seeding (optional – enable once, or move to a dedicated seeder)
/*
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();

    foreach (UserRole role in Enum.GetValues(typeof(UserRole)))
    {
        var roleName = role.ToString();

        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new Role
            {
                Id = Guid.NewGuid(),
                Name = roleName,
                NormalizedName = roleName.ToUpperInvariant()
            });
        }
    }
}
*/

// =======================================
// Middleware Pipeline
// =======================================
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("live")
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.UseHealthChecksUI(options =>
{
    options.UIPath = "/health-ui";
    options.ApiPath = "/health-ui-api";
});

// app.MapHub<YourHub>("/hubs/whatever");

app.Run();

