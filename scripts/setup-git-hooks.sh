#!/bin/bash
# Setup Git hooks for the project

echo "🪝 Setting up Git hooks..."

# Install husky (Node.js required)
if command -v npm &> /dev/null; then
  npm install -g husky
  
  # Initialize husky
  npx husky init
  
  # Create pre-commit hook
  cat > .husky/pre-commit << 'HOOK'
#!/usr/bin/env sh
. "$(dirname -- "$0")/_/husky.sh"

echo "🔍 Running pre-commit checks..."

# Run lint-staged if available
if command -v npx &> /dev/null; then
  npx lint-staged
fi

# Check for secrets
echo "🔐 Checking for secrets..."
git diff --cached --name-only | xargs grep -nHE '(password|secret|key|token)\s*=\s*["\x27][^"\x27]{8,}' && {
  echo "❌ Potential secrets detected! Please remove them."
  exit 1
} || echo "✅ No secrets detected"

echo "✅ Pre-commit checks passed!"
HOOK

  chmod +x .husky/pre-commit
  
  # Create commit-msg hook for commitlint
  cat > .husky/commit-msg << 'HOOK'
#!/usr/bin/env sh
. "$(dirname -- "$0")/_/husky.sh"

npx --no -- commitlint --edit ${1}
HOOK

  chmod +x .husky/commit-msg
  
  echo "✅ Git hooks installed successfully!"
else
  echo "⚠️  npm not found. Skipping Husky setup."
fi