<#
.SYNOPSIS
  Generate gRPC stubs for Python, .NET, and optionally gRPC-Web (TypeScript).
.DESCRIPTION
  Run this from the repo root:
      pwsh scripts/proto-gen.ps1
      (or)
      powershell -File scripts\proto-gen.ps1
#>

$ErrorActionPreference = "Stop"

# -----------------------------------------------------------------------------
# Paths
# -----------------------------------------------------------------------------
# $PSScriptRoot is the folder containing this script.
$RootDir    = Split-Path -Parent $PSScriptRoot
$ProtoRoot  = Join-Path $RootDir "packages/contracts/protos"
$PyOut      = Join-Path $RootDir "packages/contracts/python"

# NOTE: you pointed to SSSP.Infrastructure in your last message; keep or change as needed:
$CsProtoOut = Join-Path $RootDir "apps/api/src/SSSP.Infrastructure/Protos"

# Optional: only used if you generate grpc-web
$WebOut     = Join-Path $RootDir "apps/web/src/protos"

# -----------------------------------------------------------------------------
# Ensure directories
# -----------------------------------------------------------------------------
Write-Host "Ensuring output directories exist..."
New-Item -ItemType Directory -Force -Path $PyOut      | Out-Null
New-Item -ItemType Directory -Force -Path $CsProtoOut | Out-Null
New-Item -ItemType Directory -Force -Path $WebOut     | Out-Null

# Ensure __init__.py exists for Python package import
$InitFile = Join-Path $PyOut "__init__.py"
if (-not (Test-Path $InitFile)) { New-Item -ItemType File -Path $InitFile | Out-Null }

# -----------------------------------------------------------------------------
# Python generation
# -----------------------------------------------------------------------------
Write-Host "Generating Python gRPC stubs..."
python -m grpc_tools.protoc `
  -I "$ProtoRoot" `
  --python_out="$PyOut" `
  --grpc_python_out="$PyOut" `
  "$ProtoRoot\*.proto"

Write-Host "Python stubs generated at: $PyOut"

# -----------------------------------------------------------------------------
# .NET: copy .proto (C# code will be generated at build by Grpc.Tools)
# -----------------------------------------------------------------------------
Write-Host "Copying .proto files for .NET build..."
Copy-Item "$ProtoRoot\*.proto" -Destination "$CsProtoOut" -Force
Write-Host "Copied proto to: $CsProtoOut"
Write-Host "C# stubs will be generated on next 'dotnet build'."

# -----------------------------------------------------------------------------
# Optional: gRPC-Web (TypeScript)
# -----------------------------------------------------------------------------
$GrpcWebExe = Get-Command "protoc-gen-grpc-web" -ErrorAction SilentlyContinue
if ($GrpcWebExe) {
  Write-Host "Generating TypeScript gRPC-Web stubs..."
  protoc -I "$ProtoRoot" `
    "$ProtoRoot\*.proto" `
    --js_out="import_style=commonjs:$WebOut" `
    --grpc-web_out="import_style=typescript,mode=grpcwebtext:$WebOut"
  Write-Host "gRPC-Web stubs generated at: $WebOut"
}
else {
  Write-Host "Skipping gRPC-Web (protoc-gen-grpc-web not installed)."
}

Write-Host ""
Write-Host "Proto generation completed successfully."
