using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using SSSP.BL.Managers;
using SSSP.DAL.Context;
using SSSP.DAL.Models;
using SSSP.Infrastructure.AI.Grpc.Clients;
using SSSP.Infrastructure.AI.Grpc.Config;
using SSSP.Infrastructure.AI.Grpc.Interfaces;
using SSSP.Infrastructure.Persistence.Repos;
using SSSP.Infrastructure.Persistence.Interfaces;
using System.Text;
using SSSP.Infrastructure.Persistence.UnitOfWork;
using SSSP.BL.Services;
using SSSP.DAL.Enums;
using SSSP.BL.Services.Interfaces;

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

    // JWT Bearer in Swagger
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

// =======================================
// Domain / Infrastructure Services
// =======================================

// uof & Repo
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped(typeof(IRepository<,>), typeof(Repository<,>));
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<ISensorService, SensorService>();

builder.Services.AddSingleton<IAIFaceClient, AIFaceClient>();
builder.Services.AddSingleton<IVideoStreamClient, VideoStreamClient>();

builder.Services.AddSingleton<FaceMatchingManager>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<FaceMatchingManager>>();
    return new FaceMatchingManager(0.6, logger);
});

builder.Services.AddScoped<FaceRecognitionService>();
builder.Services.AddScoped<FaceEnrollmentService>();
builder.Services.AddScoped<CameraMonitoringService>();

builder.Services.AddSignalR();
builder.Services.AddHealthChecks();


// =======================================
// Build App
// =======================================
var app = builder.Build();

// Just to verify AI config on startup (Dev only)
if (app.Environment.IsDevelopment())
{
    var aiOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AIOptions>>().Value;
    Console.WriteLine($"AI REST: {aiOptions.RestUrl}");
    Console.WriteLine($"AI gRPC: {aiOptions.GrpcUrl}");
}

// use to seed roles (once only)
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
app.MapHealthChecks("/health");
// app.MapHub<YourHub>("/hubs/whatever");

app.Run();
