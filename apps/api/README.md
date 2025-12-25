# SSSP API (.NET 9)
Backend API for Smart Security & Sustainability Platform

## Quick Start
```bash
# Install dependencies
cd apps/api
dotnet restore

# Update appsettings.json with your database connection

# Run migrations
dotnet ef database update --project src/SSSP.Infrastructure --startup-project src/SSSP.Api

# Run API
cd src/SSSP.Api
dotnet run

# API: https://localhost:5001
# Swagger: https://localhost:5001/swagger
```

## Configuration
Edit `src/SSSP.Api/appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=sssp;User Id=sa;Password=Your_password123!;"
  },
  "JwtSettings": {
    "Issuer": "sssp.local",
    "ExpiryMinutes": 15
  }
}
```

## Project Structure
```bash
src/
├── SSSP.Api/              # Controllers, Middleware
├── SSSP.Application/      # Use Cases, DTOs
├── SSSP.Domain/           # Entities, Interfaces
└── SSSP.Infrastructure/   # Database, Repositories
```

## Common Commands
```bash
dotnet watch run
dotnet test
dotnet ef migrations add MigrationName --project src/SSSP.Infrastructure --startup-project src/SSSP.Api
dotnet format
```

## Test Login
```bash
curl -X POST http://localhost:5001/api/auth/login   -H "Content-Type: application/json"   -d '{"email": "admin@test.com", "password": "Admin123!"}'
```

## Troubleshooting
- **Database error**: Check SQL Server is running
- **Port in use**: Change port in launchSettings.json
- **Migration error**: Drop database and recreate

## Contact
Backend Team - #backend-channel
