param(
    [string]$Tag = "dev"
)

$Root = (Get-Item "$PSScriptRoot\..\..").FullName
$AiImage = "sssp-ai-dev:$Tag"
$ApiImage = "sssp-api-dev:$Tag"

# ============================
# STEP 1 — Ensure images exist
# ============================
if (!(docker images --format "{{.Repository}}:{{.Tag}}" | Select-String "^$AiImage$") -or
    !(docker images --format "{{.Repository}}:{{.Tag}}" | Select-String "^$ApiImage$")) {

    Write-Host "One or more images missing. Triggering build..." -ForegroundColor Yellow
    & "$PSScriptRoot/build-dev.ps1" -Tag $Tag
}


# ============================
# STEP 2 — Create network if not exists
# ============================
$Network = "sssp-dev"
$NetExists = docker network ls --format "{{.Name}}" | Select-String "^$Network$"
if (!$NetExists) {
    Write-Host "Creating dev network '$Network'..." -ForegroundColor Yellow
    docker network create $Network | Out-Null
} else {
    Write-Host "Dev network found." -ForegroundColor Green
}

# ============================
# STEP 3 — Start AI service
# ============================
$AiContainer = "sssp-ai-dev"
$Exists = docker ps -a --format "{{.Names}}" | Select-String "^$AiContainer$"

if ($Exists) {
    Write-Host "Removing old AI container..." -ForegroundColor Yellow
    docker rm -f $AiContainer | Out-Null
}

Write-Host "Starting AI service..." -ForegroundColor Cyan
docker run -d `
    --name $AiContainer `
    --network $Network `
    -p 8001:8001 `
    -p 50051:50051 `
    -v "$Root/apps/ai:/app" `
    -v "$Root/packages/contracts:/app/contracts" `
    --env-file "$Root/apps/ai/.env" `
    $AiImage


# ============================
# STEP 4 — Start API service
# ============================
$ApiContainer = "sssp-api-dev"
$Exists = docker ps -a --format "{{.Names}}" | Select-String "^$ApiContainer$"

if ($Exists) {
    Write-Host "Removing old API container..." -ForegroundColor Yellow
    docker rm -f $ApiContainer | Out-Null
}

Write-Host "Starting API service..." -ForegroundColor Cyan
docker run -d `
    --name $ApiContainer `
    --network $Network `
    -p 5000:5000 `
    -e AI_REST_URL="http://sssp-ai-dev:8001" `
    -e AI_GRPC_URL="sssp-ai-dev:50051" `
    $ApiImage


Write-Host ""
Write-Host "=====================================" -ForegroundColor Green
Write-Host "All dev services started successfully!" -ForegroundColor Green
Write-Host "====================================="