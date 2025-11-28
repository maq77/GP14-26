param(
    [string]$Tag = "dev"
)

Write-Host "Building .NET API (development)..." -ForegroundColor Cyan

$Root = (Get-Item "$PSScriptRoot\..\..").FullName
$Dockerfile = "$Root\apps\api\Dockerfile"
$ImageName = "sssp-api-dev:$Tag"

Write-Host "Root Directory: $Root"
Write-Host "Dockerfile:     $Dockerfile"
Write-Host "Image:          $ImageName"

docker build `
    -f $Dockerfile `
    --target development `
    -t $ImageName `
    -t "sssp-api-dev:latest" `
    $Root

Write-Host ".NET API development image built successfully." -ForegroundColor Green
