#!/bin/bash

echo "🔍 Verifying SSSP Project Setup..."
echo ""

# Check .NET API
echo "▶ Checking .NET API..."
if [ -f "apps/api/SSSP.sln" ]; then
  cd apps/api || exit
  if dotnet build > /dev/null 2>&1; then
    echo "✅ .NET API builds successfully"
  else
    echo "❌ .NET API build failed"
  fi
  cd ../..
else
  echo "❌ .NET solution not found"
fi

echo ""

# Check Python AI
echo "▶ Checking Python AI Service..."
if [ -f "apps/ai/requirements.txt" ]; then
  echo "✅ Python AI project structure exists"
  if [ -f "apps/ai/src/api/main.py" ]; then
    echo "✅ FastAPI main.py found"
  fi
else
  echo "❌ Python requirements not found"
fi

echo ""

# Check React Web
echo "▶ Checking React Web Dashboard..."
if [ -f "apps/web/package.json" ]; then
  cd apps/web || exit
  if npm run type-check > /dev/null 2>&1; then
    echo "✅ React app type-checks successfully"
  else
    echo "❌ React type-check failed"
  fi
  cd ../..
else
  echo "❌ React package.json not found"
fi

echo ""
echo "======================================"
echo "🎉 Setup verification complete!"
echo "======================================"