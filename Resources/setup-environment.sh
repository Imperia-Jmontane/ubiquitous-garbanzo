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

DOTNET_ROOT="$HOME/.dotnet"

echo "=== Creating global symlinks for dotnet CLI ==="
# Create symlinks in /usr/local/bin so dotnet is available system-wide
sudo ln -sf "$DOTNET_ROOT/dotnet" /usr/local/bin/dotnet

# Also create symlinks for common dotnet tools location
sudo mkdir -p /usr/local/share/dotnet
sudo ln -sf "$DOTNET_ROOT/dotnet" /usr/local/share/dotnet/dotnet

echo "=== Configuring environment variables ==="

# Add to /etc/environment for system-wide availability (non-interactive shells)
if ! grep -q 'DOTNET_ROOT' /etc/environment 2>/dev/null; then
  echo "DOTNET_ROOT=$DOTNET_ROOT" | sudo tee -a /etc/environment > /dev/null
fi

# Add to ~/.profile for login shells (works for non-interactive too)
if ! grep -q 'DOTNET_ROOT' ~/.profile 2>/dev/null; then
  cat >> ~/.profile << EOF

# .NET SDK configuration
export DOTNET_ROOT="$DOTNET_ROOT"
export PATH="\$DOTNET_ROOT:\$DOTNET_ROOT/tools:\$PATH"
EOF
fi

# Add to ~/.bashrc for interactive bash shells
if ! grep -q 'DOTNET_ROOT' ~/.bashrc 2>/dev/null; then
  cat >> ~/.bashrc << EOF

# .NET SDK configuration
export DOTNET_ROOT="$DOTNET_ROOT"
export PATH="\$DOTNET_ROOT:\$DOTNET_ROOT/tools:\$PATH"
EOF
fi

# Apply immediately for current session
export DOTNET_ROOT="$DOTNET_ROOT"
export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"

echo "=== Verifying installation ==="
echo "Testing dotnet via symlink (/usr/local/bin/dotnet):"
/usr/local/bin/dotnet --version

echo ""
echo "Testing dotnet via PATH:"
dotnet --info
dotnet --list-sdks

echo ""
echo "=== Verifying symlink ==="
ls -la /usr/local/bin/dotnet

echo ""
echo "=== Setup complete ==="
echo ".NET SDK is available system-wide via /usr/local/bin/dotnet"
