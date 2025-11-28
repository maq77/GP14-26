param(
    [string]$ComposeFileName = "docker-compose-v2.yml",
    [switch]$Rebuild
)

Write-Host "=== SSSP Dev Environment ===" -ForegroundColor Yellow

$Root = (Get-Item "$PSScriptRoot\..\..").FullName
$ComposeDir  = Join-Path $Root "infrastructure\docker"
$ComposePath = Join-Path $ComposeDir $ComposeFileName

Write-Host "Project Root:  $Root"
Write-Host "Compose Dir:   $ComposeDir"
Write-Host "Compose File:  $ComposePath"
Write-Host ""

if (-not (Test-Path $ComposePath)) {
    Write-Host "docker-compose file not found at: $ComposePath" -ForegroundColor Red
    exit 1
}

$oldLocation = Get-Location
Set-Location $ComposeDir

try {
    $composeArgs = @(
        "compose"
        "-f", $ComposeFileName
        "up"
        "-d"
    )

    if ($Rebuild) {
        Write-Host "Forcing rebuild of images (--build)..." -ForegroundColor Yellow
        $composeArgs += "--build"
    } else {
        Write-Host "Starting dev stack (will build images only if missing)..." -ForegroundColor Cyan
    }

    docker @composeArgs

    if ($LASTEXITCODE -ne 0) {
        Write-Host "docker compose up failed." -ForegroundColor Red
        exit $LASTEXITCODE
    }

    Write-Host ""
    Write-Host "=====================================" -ForegroundColor Green
    Write-Host "Dev environment is up (docker compose)" -ForegroundColor Green
    Write-Host "====================================="
    Write-Host "Use:  docker compose -f $ComposeFileName ps" -ForegroundColor DarkGray
    Write-Host "      docker compose -f $ComposeFileName logs -f" -ForegroundColor DarkGray
}
finally {
    Set-Location $oldLocation
}
