param(
    [string]$Tag = "dev",
    [string]$Network = "sssp-dev"
)

$ContainerName = "sssp-api-dev"
$Root = (Get-Item "$PSScriptRoot\..\..").FullName
$ImageName = "sssp-api-dev:$Tag"

Write-Host "Starting .NET API (Dev Mode)..." -ForegroundColor Yellow
Write-Host "Project Root: $Root"
Write-Host "Container:    $ContainerName"
Write-Host "Image:        $ImageName"
Write-Host "Network:      $Network"
Write-Host ""

# -------------------------
#  Ensure image exists
# -------------------------
$imageExists = docker images --format "{{.Repository}}:{{.Tag}}" | Select-String "^$ImageName$" -Quiet
if (-not $imageExists) {
    Write-Host "Image '$ImageName' not found." -ForegroundColor Red
    Write-Host "Build it first, e.g. via: scripts/dev/build-dev.ps1 or your API build script." -ForegroundColor DarkYellow
    exit 1
}

# -------------------------
#  Create network if not exists
# -------------------------
$netExists = docker network ls --format "{{.Name}}" | Select-String "^$Network$" -Quiet
if (-not $netExists) {
    Write-Host "Creating Docker network '$Network'..."
    docker network create $Network | Out-Null
} else {
    Write-Host "Docker network '$Network' already exists."
}

# -------------------------
#  Stop old container if exists
# -------------------------
$exists = docker ps -a --format "{{.Names}}" | Select-String "^$ContainerName$" -Quiet
if ($exists) {
    Write-Host "Stopping old container..."
    docker stop $ContainerName | Out-Null
    Write-Host "Removing old container..."
    docker rm $ContainerName | Out-Null
}

# -------------------------
#  SAFE DOCKER RUN (SPLAT)
# -------------------------

$runArgs = @(
    "run"
    "-it"
    "--rm"
    "--name", $ContainerName
    "--network", $Network
    "-p", "8080:8080"
    "-p", "8081:8081"
    "-v", "$Root:/src"
    "-e", "ASPNETCORE_ENVIRONMENT=Development"
    "-e", "AI_REST_URL=http://sssp-ai-dev:8001"
    "-e", "AI_GRPC_URL=sssp-ai-dev:50051"
    $ImageName
)

Write-Host "Running container..." -ForegroundColor Cyan
docker @runArgs
