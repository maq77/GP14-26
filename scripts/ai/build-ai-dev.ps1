param(
    [string]$Tag = "dev"
)

Write-Host "Building AI Service (development)..." -ForegroundColor Cyan

$Root = (Resolve-Path "$PSScriptRoot\..\..").Path
$Dockerfile = "$Root\apps\ai\Dockerfile"
$ImageName = "sssp-ai-dev:$Tag"

Write-Host "Root Directory: $Root"
Write-Host "Dockerfile:     $Dockerfile"
Write-Host "Image:          $ImageName"

docker build `
    -f $Dockerfile `
    --target development `
    -t $ImageName `
    -t "sssp-ai-dev:latest" `
    $Root

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed." -ForegroundColor Red
    exit 1
}

Write-Host "AI development image built successfully." -ForegroundColor Green
