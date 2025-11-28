param(
    [switch]$OpenNewWindows
)

$Root = Split-Path $MyInvocation.MyCommand.Path -Parent

# 1) Start AI
$aiCmd = "cd `"$Root/apps/ai`"; .\venv\Scripts\activate; uvicorn src.api.main:app --host 0.0.0.0 --port 8001 --reload"

# 2) Start API
$apiCmd = "cd `"$Root/apps/api/src/SSSP.Api`"; dotnet watch run --urls `"http://localhost:8080`""

if ($OpenNewWindows) {
    Start-Process powershell -ArgumentList "-NoExit", "-Command", $aiCmd
    Start-Process powershell -ArgumentList "-NoExit", "-Command", $apiCmd
} else {
    Write-Host "Run these in two terminals:" -ForegroundColor Yellow
    Write-Host $aiCmd -ForegroundColor Cyan
    Write-Host $apiCmd -ForegroundColor Green
}
