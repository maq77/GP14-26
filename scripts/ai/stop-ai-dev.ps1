$ContainerName = "sssp-ai-dev"

Write-Host "Stopping AI Service Dev Container..." -ForegroundColor Red

$exists = docker ps -a --format "{{.Names}}" | Select-String "^$ContainerName$"

if ($exists) {
    docker stop $ContainerName | Out-Null
    docker rm $ContainerName | Out-Null
    Write-Host "Container stopped and removed."
}
else {
    Write-Host "No container named $ContainerName found."
}
