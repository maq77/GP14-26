#!/bin/bash
set -e

echo "🚀 SSSP Complete Setup"
echo "======================"
echo ""

# Color codes
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Function to print colored output
print_success() { echo -e "${GREEN}✅ $1${NC}"; }
print_info() { echo -e "${BLUE}ℹ️  $1${NC}"; }
print_warning() { echo -e "${YELLOW}⚠️  $1${NC}"; }
print_error() { echo -e "${RED}❌ $1${NC}"; }
print_step() { echo -e "\n${BLUE}▶ $1${NC}"; }

# Function to check if command exists
command_exists() { command -v "$1" >/dev/null 2>&1; }

# ==================================================
# STEP 1: Check Prerequisites
# ==================================================
print_step "Checking prerequisites..."

MISSING_DEPS=()
for dep in git docker docker-compose node npm dotnet python3; do
  if ! command_exists "$dep"; then MISSING_DEPS+=("$dep"); fi
done

if [ ${#MISSING_DEPS[@]} -ne 0 ]; then
  print_error "Missing required dependencies: ${MISSING_DEPS[*]}"
  echo ""
  echo "Please install:"
  for dep in "${MISSING_DEPS[@]}"; do echo "  - $dep"; done
  exit 1
fi
print_success "All prerequisites installed"

if ! docker info >/dev/null 2>&1; then
  print_error "Docker is not running. Please start Docker first."
  exit 1
fi
print_success "Docker is running"

# ==================================================
# STEP 2: Git Repository Setup
# ==================================================
print_step "Setting up Git repository..."

if [ ! -d .git ]; then git init && print_success "Git repository initialized"; else print_info "Git repository already initialized"; fi

if [ ! -f .gitignore ]; then
  cat > .gitignore << 'GITIGNORE'
# Environment
.env
.env.local
*.local

# OS
.DS_Store
Thumbs.db
.AppleDouble
.LSOverride

# IDEs
.vscode/
.idea/
*.suo
*.user
*.swp
*~

# Dependencies
node_modules/
packages/
vendor/

# Build outputs
bin/
obj/
dist/
build/
out/
*.dll
*.exe
*.pdb

# Logs
logs/
*.log
npm-debug.log*

# Docker
.dockerignore

# ML
*.pt
*.pth
*.onnx
*.h5
__pycache__/
*.pyc
.ipynb_checkpoints/
.pytest_cache/

# Data
data/
datasets/
models/cache/

# Coverage
coverage/
*.coverage
.nyc_output/
htmlcov/

# Temporary
tmp/
temp/
*.tmp
GITIGNORE
  print_success "Created .gitignore"
else
  print_info ".gitignore already exists"
fi

# ==================================================
# STEP 3: Environment Configuration
# ==================================================
print_step "Setting up environment configuration..."
if [ ! -f infrastructure/docker/.env ]; then
  if [ -f infrastructure/docker/.env.example ]; then
    cp infrastructure/docker/.env.example infrastructure/docker/.env
    print_success "Created .env from template"
    print_warning "Please edit infrastructure/docker/.env with your configuration"
  else
    print_error ".env.example not found!"
    exit 1
  fi
else
  print_info ".env already exists"
fi

# ==================================================
# STEP 4: Install Git Hooks
# ==================================================
print_step "Setting up Git hooks..."
if command_exists npm; then
  if [ ! -f package.json ]; then
    cat > package.json << 'PKG'
{
  "name": "sssp-root",
  "version": "1.0.0",
  "private": true,
  "description": "SSSP - Smart Security & Sustainability Platform",
  "scripts": {
    "prepare": "husky install"
  },
  "devDependencies": {
    "husky": "^8.0.3",
    "lint-staged": "^15.2.0",
    "@commitlint/cli": "^18.4.3",
    "@commitlint/config-conventional": "^18.4.3"
  }
}
PKG
    print_success "Created root package.json"
  fi

  npm install --save-dev husky lint-staged @commitlint/cli @commitlint/config-conventional
  npx husky install

  mkdir -p .husky
  cat > .husky/pre-commit << 'HOOK'
#!/usr/bin/env sh
. "$(dirname -- "$0")/_/husky.sh"
echo "🔍 Running pre-commit checks..."
echo "🔐 Checking for secrets..."
git diff --cached --name-only -z | xargs -0 grep -nHE '(password|secret|key|token)\s*=\s*["\047][^"\047]{8,}' && {
  echo "❌ Potential secrets detected! Please remove them."
  exit 1
} || echo "✅ No secrets detected"
echo "✅ Pre-commit checks passed!"
HOOK
  chmod +x .husky/pre-commit

  cat > .husky/commit-msg << 'HOOK'
#!/usr/bin/env sh
. "$(dirname -- "$0")/_/husky.sh"
npx --no -- commitlint --edit ${1}
HOOK
  chmod +x .husky/commit-msg
  print_success "Git hooks installed"
else
  print_warning "npm not available, skipping Git hooks setup"
fi

# ==================================================
# STEP 5: Setup .NET Project
# ==================================================
print_step "Setting up .NET API project..."
if [ -d apps/api ]; then
  cd apps/api
  if [ -f SSSP.sln ]; then dotnet restore SSSP.sln && print_success ".NET dependencies restored"; else print_warning "SSSP.sln not found"; fi
  cd ../..
else
  print_warning "apps/api directory not found"
fi

# ==================================================
# STEP 6: Setup Python AI Service
# ==================================================
print_step "Setting up Python AI service..."
if [ -d apps/ai ]; then
  cd apps/ai
  if [ ! -d venv ]; then python3 -m venv venv && print_success "Virtual environment created"; fi
  source venv/bin/activate
  if [ -f requirements.txt ]; then pip install --upgrade pip && pip install -r requirements.txt && print_success "Python dependencies installed"; else print_warning "requirements.txt not found"; fi
  deactivate
  cd ../..
else
  print_warning "apps/ai directory not found"
fi

# ==================================================
# STEP 7: Setup React Web Dashboard
# ==================================================
print_step "Setting up React web dashboard..."
if [ -d apps/web ]; then
  cd apps/web
  if [ -f package.json ]; then npm install && print_success "Node.js dependencies installed"; else print_warning "package.json not found"; fi
  cd ../..
else
  print_warning "apps/web directory not found"
fi

# ==================================================
# STEP 8: Pull Docker Base Images
# ==================================================
print_step "Pulling Docker base images..."
docker pull mcr.microsoft.com/dotnet/aspnet:9.0-alpine
docker pull mcr.microsoft.com/dotnet/sdk:9.0-alpine
docker pull python:3.11-slim
docker pull node:20-alpine
docker pull nginx:1.25-alpine
docker pull mcr.microsoft.com/mssql/server:2022-latest
docker pull redis:7-alpine
docker pull rabbitmq:3.12-management-alpine
docker pull quay.io/minio/minio:latest
print_success "Docker base images pulled"

# ==================================================
# STEP 9: Create Required Directories
# ==================================================
print_step "Creating required directories..."
mkdir -p logs data/uploads data/models data/cache tests/performance/results docs/architecture/{ADRs,c4}
print_success "Directories created"

# ==================================================
# STEP 10: Generate Sample Configuration Files
# ==================================================
print_step "Generating sample configuration files..."
if [ ! -f README.md ]; then
  cat > README.md << 'README'
# SSSP - Smart Security & Sustainability Platform
AI-powered IoT platform for smart cities combining security monitoring, environmental sustainability, and citizen engagement.
README
  print_success "Created README.md"
fi

if [ ! -f CONTRIBUTING.md ]; then
  cat > CONTRIBUTING.md << 'CONTRIB'
# Contributing to SSSP
Thank you for contributing to SSSP!
CONTRIB
  print_success "Created CONTRIBUTING.md"
fi

# ==================================================
# STEP 11: Make Scripts Executable
# ==================================================
print_step "Making scripts executable..."
chmod +x scripts/*.sh 2>/dev/null || true
print_success "Scripts are executable"

# ==================================================
# STEP 12: Test Docker Compose Configuration
# ==================================================
print_step "Validating Docker Compose configuration..."
cd infrastructure/docker
if docker-compose config >/dev/null 2>&1; then
  print_success "Docker Compose configuration is valid"
else
  print_error "Docker Compose configuration has errors"
  exit 1
fi
cd ../..

# ==================================================
# STEP 13: Initial Git Commit
# ==================================================
print_step "Creating initial Git commit..."
git add .
if git diff-index --quiet HEAD -- 2>/dev/null; then
  print_info "No changes to commit"
else
  git commit -m "chore: initial project setup" || print_info "Already committed"
  print_success "Initial commit created"
fi

# ==================================================
# STEP 14: Display Summary
# ==================================================
echo ""
echo "======================================"
echo "🎉 Setup Complete!"
echo "======================================"
echo ""
print_success "SSSP development environment is ready!"
echo ""
echo "📝 Next Steps:"
echo "   nano infrastructure/docker/.env"
echo "   ./scripts/setup-dev.sh"
echo ""
print_info "See README.md and docs/"
print_warning "Don't forget to update .env and CI/CD secrets"
echo "Happy coding! 🚀"