Write-Host "🏗️ Building ALL Dev Images (AI + API)..." -ForegroundColor Cyan

$Root = (Get-Item "$PSScriptRoot").FullName

Write-Host "🔨 Building AI Dev Image..."
& "$Root\ai\build-ai-dev.ps1"

Write-Host "🔨 Building API Dev Image..."
& "$Root\api\build-api-dev.ps1"

Write-Host "✅ ALL dev images built successfully!" -ForegroundColor Green
