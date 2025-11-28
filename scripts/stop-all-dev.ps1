Write-Host "🚀 Starting ALL Dev Services (AI + API)..." -ForegroundColor Yellow

$Root = (Get-Item "$PSScriptRoot").FullName
$Network = "sssp-dev"

# Create network if missing
$netExists = docker network ls --format "{{.Name}}" | Select-String "^$Network$"
if (!$netExists) {
    Write-Host "🔧 Creating Docker network $Network..."
    docker network create $Network | Out-Null
}

Write-Host "🧠 Starting AI Service..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command `"$Root\ai\start-ai-dev.ps1`""

Start-Sleep -Seconds 2

Write-Host "🟦 Starting .NET API Service..." -ForegroundColor Blue
Start-Process powershell -ArgumentList "-NoExit", "-Command `"$Root\api\start-api-dev.ps1`""

Write-Host "✅ ALL services started." -ForegroundColor Green
Write-Host "🌐 URLs:" -ForegroundColor Magenta
Write-Host "   AI REST Docs: http://localhost:8001/api/docs"
Write-Host "   API (.NET):   http://localhost:8080"
