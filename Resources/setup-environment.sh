#!/bin/bash
# ============================================================================
# Environment Setup for Code Analysis Feature
# Run this script to prepare the environment for Roslyn-based code indexing
# ============================================================================

set -e

echo "=== Installing system dependencies ==="
sudo apt-get update
sudo apt-get install -y --no-install-recommends \
  ca-certificates \
  curl \
  jq \
  zlib1g \
  libunwind8 \
  libicu74 \
  libssl3 \
  libc6 \
  libgcc-s1 \
  libstdc++6 \
  git

echo "=== Installing .NET 9 SDK ==="
curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
chmod +x /tmp/dotnet-install.sh
/tmp/dotnet-install.sh --channel 9.0

echo "=== Configuring environment variables ==="
# Add to .bashrc if not already present
if ! grep -q 'DOTNET_ROOT' ~/.bashrc; then
  cat >> ~/.bashrc << 'EOF'

# .NET SDK configuration
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"
EOF
fi

# Apply immediately for current session
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"

echo "=== Verifying installation ==="
dotnet --info
dotnet --list-sdks

echo ""
echo "=== Setup complete ==="
echo "Run 'source ~/.bashrc' or start a new terminal to apply PATH changes."
