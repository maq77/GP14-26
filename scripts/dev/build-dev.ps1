param(
    [string]$Tag = "dev"
)

Write-Host "Building development images..." -ForegroundColor Cyan

$Root = (Get-Item "$PSScriptRoot\..\..").FullName
Write-Host "Project Root: $Root" -ForegroundColor DarkCyan

# ---- AI build ----
$AiImage = "sssp-ai-dev:$Tag"
if (!(docker images --format "{{.Repository}}:{{.Tag}}" | Select-String "^$AiImage$")) {
    Write-Host "AI image not found. Building..." -ForegroundColor Yellow
    docker build `
        -f "$Root/apps/ai/Dockerfile" `
        --target development `
        -t $AiImage `
        -t "sssp-ai-dev:latest" `
        "$Root"
} else {
    Write-Host "AI image already exists. Skipping build." -ForegroundColor Green
}

# ---- API build ----
$ApiImage = "sssp-api-dev:$Tag"
if (!(docker images --format "{{.Repository}}:{{.Tag}}" | Select-String "^$ApiImage$")) {
    Write-Host "API image not found. Building..." -ForegroundColor Yellow
    docker build `
        -f "$Root/apps/api/Dockerfile" `
        --target development `
        -t $ApiImage `
        -t "sssp-api-dev:latest" `
        "$Root"
} else {
    Write-Host "API image already exists. Skipping build." -ForegroundColor Green
}

Write-Host "Development images are ready." -ForegroundColor Green
