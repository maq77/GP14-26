#!/usr/bin/env bash
# -----------------------------------------------------------------------------
# Generate gRPC and Protobuf code for all supported languages
# -----------------------------------------------------------------------------
# Usage:
#   bash scripts/proto-gen.sh
# -----------------------------------------------------------------------------

set -euo pipefail

# -----------------------------------------------------------------------------
# Paths
# -----------------------------------------------------------------------------
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROTO_SRC="$ROOT_DIR/packages/contracts/protos"
PY_OUT="$ROOT_DIR/packages/contracts/python"
CS_PROTO_OUT="$ROOT_DIR/apps/api/src/Infrastructure/Protos"
WEB_OUT="$ROOT_DIR/apps/web/src/protos"    # optional if you’ll add grpc-web later

# -----------------------------------------------------------------------------
# Ensure directories exist
# -----------------------------------------------------------------------------
mkdir -p "$PY_OUT" "$CS_PROTO_OUT" "$WEB_OUT"
touch "$PY_OUT/__init__.py"

# -----------------------------------------------------------------------------
# Python Generation
# -----------------------------------------------------------------------------
echo "🧠 Generating Python gRPC stubs..."
python -m grpc_tools.protoc \
  -I "$PROTO_SRC" \
  --python_out="$PY_OUT" \
  --grpc_python_out="$PY_OUT" \
  "$PROTO_SRC"/*.proto

echo "✅ Python stubs generated at: $PY_OUT"

# -----------------------------------------------------------------------------
# .NET (C#) proto copy
# Grpc.Tools will autogenerate C# code during build.
# -----------------------------------------------------------------------------
echo "⚙️  Copying proto to .NET project..."
cp "$PROTO_SRC"/*.proto "$CS_PROTO_OUT"/
echo "✅ Copied to: $CS_PROTO_OUT"

# -----------------------------------------------------------------------------
# JavaScript / TypeScript (optional)
# Requires `npm i -g grpc-web protoc-gen-grpc-web`
# -----------------------------------------------------------------------------
if command -v protoc-gen-grpc-web >/dev/null 2>&1; then
  echo "🌐 Generating TypeScript gRPC-Web stubs..."
  protoc -I "$PROTO_SRC" \
    "$PROTO_SRC"/*.proto \
    --js_out=import_style=commonjs:"$WEB_OUT" \
    --grpc-web_out=import_style=typescript,mode=grpcwebtext:"$WEB_OUT"
  echo "✅ gRPC-Web stubs generated at: $WEB_OUT"
else
  echo "⚠️  Skipping gRPC-Web (protoc-gen-grpc-web not installed)"
fi

# -----------------------------------------------------------------------------
# Done
# -----------------------------------------------------------------------------
echo "🎉 Proto generation completed successfully."
