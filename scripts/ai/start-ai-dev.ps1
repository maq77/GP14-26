param(
    [string]$Tag = "dev",
    [string]$Network = "sssp-dev"
)

$ContainerName = "sssp-ai-dev"
$Root = (Get-Item "$PSScriptRoot\..\..").FullName
$EnvFile = "$Root\apps\ai\.env"
$ImageName = "sssp-ai-dev:$Tag"

Write-Host "Starting AI Service (Dev Mode)..." -ForegroundColor Yellow
Write-Host "Container: $ContainerName"
Write-Host "Image:     $ImageName"
Write-Host "Network:   $Network"

# Create network if not exists
$netExists = docker network ls --format "{{.Name}}" | Select-String "^$Network$"
if (!$netExists) {
    Write-Host "Creating Docker network $Network..."
    docker network create $Network | Out-Null
}

# Stop existing container
$exists = docker ps -a --format "{{.Names}}" | Select-String "^$ContainerName$"
if ($exists) {
    Write-Host "Stopping old container..."
    docker stop $ContainerName | Out-Null
    docker rm $ContainerName | Out-Null
}

# Ensure .env exists
if (!(Test-Path $EnvFile)) {
    Write-Host ".env file not found. Creating from example..."
    Copy-Item "$Root\apps\ai\.env.example" $EnvFile
}

# Start container
docker run -it --rm `
    --name $ContainerName `
    --network $Network `
    --env-file $EnvFile `
    -p 8001:8001 `
    -p 50051:50051 `
    -v "$Root\apps\ai\src:/app/src" `
    -v "$Root\apps\ai\data:/app/data" `
    -v "$Root\packages\contracts:/app/contracts" `
    $ImageName
