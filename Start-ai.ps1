$Root = Split-Path $MyInvocation.MyCommand.Path -Parent
Set-Location $Root

Write-Host "Starting AI API..." -ForegroundColor Green

cd "apps\ai"
.\venv\Scripts\Activate.ps1

cd "..\.."
uvicorn apps.ai.src.api.main:app --reload --port 8001
<<<<<<< HEAD
uvicorn src.api.main:app --reload --port 8001
=======
>>>>>>> main
