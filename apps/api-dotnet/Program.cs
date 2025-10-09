using System.ComponentModel.Design;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Grpc.Net.Client;
using Inference; // generated from proto
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------
// 1) Configuration (env-first)
// ---------------------------
string env(string key, string? fallback = null) =>
    Environment.GetEnvironmentVariable(key) ?? builder.Configuration[key] ?? fallback ?? "";

var allowedOriginsCsv = env("ALLOWED_ORIGINS", "http://localhost:5173");
//"Server=AMIN\\SQLSERVER,1433;Database=TechXpressDB;Integrated Security=True;Trusted_Connection=True;TrustServerCertificate=True"
var sqlConn = env("SQLSERVER_CONNSTRING",
    "Server=sqlserver,1433;Database=sssp;User Id=sa;Password=Your_password123;Encrypt=True;TrustServerCertificate=True;");
var aiGrpcUrl = env("AI_GRPC_URL", "http://localhost:50051");
var issuer = env("JWT_ISSUER", "sssp.local");
var publicPem = env("JWT_PUBLIC_KEY", "");  // RS256 verify key
var privatePem = env("JWT_PRIVATE_KEY", ""); // RS256 sign key (for DEV token endpoint only)

// ---------------------------
// 2) Services
// ---------------------------

builder.Services.AddDbContext<AppDb>(opt => opt.UseSqlServer(sqlConn));

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    var origins = allowedOriginsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));

builder.Services.AddSignalR();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "SSSP API", Version = "v1" });
    // JWT in Swagger
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement{
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme{
                Reference = new Microsoft.OpenApi.Models.OpenApiReference{
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer"
                }
            },
            new string[]{}
        }
    });
});

// JWT Auth (RS256 verify if PUBLIC key provided; otherwise dev symmetric fallback)
var tokenValidationParams = new TokenValidationParameters
{
    ValidIssuer = issuer,
    ValidateIssuer = true,
    ValidateAudience = false,
    ValidateIssuerSigningKey = true,
    ValidateLifetime = true,
    ClockSkew = TimeSpan.FromSeconds(30)
};

if (!string.IsNullOrWhiteSpace(publicPem))
{
    using var rsa = RSA.Create();
    rsa.ImportFromPem(publicPem);
    tokenValidationParams.IssuerSigningKey = new RsaSecurityKey(rsa);
}
else
{
    // DEV fallback (symmetric) — DO NOT use in production
    var devKey = env("JWT_DEV_KEY", "dev-key-change-me-please-very-long");
    tokenValidationParams.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(devKey));
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = tokenValidationParams;
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                // Allow SignalR access token via query (?access_token=) for WebSockets
                var accessToken = ctx.Request.Query["access_token"];
                var path = ctx.HttpContext.Request.Path;
                if (!StringValues.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hub/alerts"))
                    ctx.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// gRPC client for AI inference
builder.Services.AddGrpcClient<InferenceService.InferenceServiceClient>(o => 
            o.Address = new Uri(aiGrpcUrl));


// ---------------------------
// 3) App pipeline
// ---------------------------
var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

// SignalR hub
app.MapHub<AlertsHub>("/hub/alerts");

// Health
app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }))
   .WithName("Health");

// ---------------------------
// 4) Minimal endpoints
// ---------------------------

// (DEV ONLY) Issue a JWT to test 
app.MapPost("/auth/dev-token", (HttpContext http) =>
{
    if (!app.Environment.IsDevelopment())
        return Results.Unauthorized();

    var subject = "user-1";
    var role = "Operator";
    var now = DateTime.UtcNow;

    SecurityKey signingKey;
    string alg;

    if (!string.IsNullOrWhiteSpace(privatePem))
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privatePem);
        signingKey = new RsaSecurityKey(rsa);
        alg = SecurityAlgorithms.RsaSha256;
    }
    else
    {
        var devKey = env("JWT_DEV_KEY", "dev-key-change-me-please-very-long");
        signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(devKey));
        alg = SecurityAlgorithms.HmacSha256;
    }

    var token = new JwtSecurityToken(
        issuer: issuer,
        audience: null,
        claims: new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, subject),
            new Claim(ClaimTypes.Role, role),
            new Claim("operator_id", "op-1")
        },
        notBefore: now,
        expires: now.AddMinutes(15),
        signingCredentials: new SigningCredentials(signingKey, alg));

    var jwt = new JwtSecurityTokenHandler().WriteToken(token);
    return Results.Ok(new { access_token = jwt, token_type = "Bearer", expires_in = 900 });
});

// List incidents (protected)
app.MapGet("/api/incidents", async (AppDb db) =>
    Results.Ok(await db.Incidents.OrderByDescending(i => i.CreatedAtUtc).ToListAsync()))
   .RequireAuthorization();

// Create incident (protected) and broadcast via SignalR
app.MapPost("/api/incidents", async (AppDb db, IHubContext<AlertsHub> hub, IncidentCreateDto dto) =>
{
    var inc = new Incident
    {
        Id = Guid.NewGuid(),
        Type = dto.Type,
        Severity = dto.Severity,
        Source = dto.Source,
        Status = "Open",
        PayloadJson = dto.PayloadJson,
        CreatedAtUtc = DateTime.UtcNow
    };
    db.Incidents.Add(inc);
    await db.SaveChangesAsync();

    await hub.Clients.All.SendAsync("incident", new
    {
        id = inc.Id,
        type = inc.Type,
        severity = inc.Severity,
        ts = inc.CreatedAtUtc
    });

    return Results.Created($"/api/incidents/{inc.Id}", inc);
}).RequireAuthorization();

// Call AI via gRPC (demo)
app.MapGet("/test/detect", async (InferenceService.InferenceServiceClient client) =>
{
    var resp = await client.GenerateAsync(new InferenceRequest
    {
        Prompt = "Describe the image",
        Model = "gpt-vision"
    });
    return Results.Ok(resp);
});


// Run ef migrations auto
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var attempt = 0;
    while (true)
    {
        try { await db.Database.MigrateAsync(); break; }
        catch when (attempt++ < 10) { await Task.Delay(2000); }
    }
}


app.Run();

// ---------------------------
// 5) SignalR hub
// ---------------------------
public class AlertsHub : Hub { }

// ---------------------------
// 6) EF Core (quick inline DbContext & entity)
// ---------------------------
public class AppDb : DbContext
{
    public AppDb(DbContextOptions<AppDb> opt) : base(opt) { }
    public DbSet<Incident> Incidents => Set<Incident>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Incident>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasMaxLength(64).IsRequired();
            e.Property(x => x.Severity).HasMaxLength(32).IsRequired();
            e.Property(x => x.Source).HasMaxLength(64).IsRequired();
            e.Property(x => x.Status).HasMaxLength(32).IsRequired();
        });
    }
}

public class Incident
{
    public Guid Id { get; set; }
    public string Type { get; set; } = default!;
    public string Severity { get; set; } = default!;
    public string Source { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string? PayloadJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public record IncidentCreateDto(string Type, string Severity, string Source, string? PayloadJson);
