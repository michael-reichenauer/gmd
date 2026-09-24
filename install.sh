#!/bin/bash
 
# usage: curl -sL https://raw.githubusercontent.com/michael-reichenauer/gmd/main/install.sh | bash

OS="$(uname -s)"
ARCH="$(uname -m)"

case "$OS" in
  Linux)
    case "$ARCH" in
      x86_64|amd64) ASSET="gmd_linux_x64" ;;
      arm64|aarch64) ASSET="gmd_linux_arm64" ;;
      *)
        echo "Unsupported architecture for Linux: $ARCH"
        exit 1
        ;;
    esac
    PROFILE_FILES=(~/.profile)
    ;;
  Darwin)
    case "$ARCH" in
      arm64|aarch64) ASSET="gmd_osx_arm64" ;;
      x86_64|amd64)
        # Only Apple Silicon is released (see ./build), so there is nothing to download
        echo "There is no gmd release for Intel Macs, only for Apple Silicon (arm64)"
        exit 1
        ;;
      *)
        echo "Unsupported architecture for macOS: $ARCH"
        exit 1
        ;;
    esac
    PROFILE_FILES=(~/.zprofile ~/.bash_profile ~/.profile)
    ;;
  *)
    echo "Unsupported OS: $OS"
    exit 1
    ;;
esac

echo "Downloading gmd ($ASSET) for $OS/$ARCH ..."
if ! curl -fsS -L --create-dirs -o ~/gmd/gmd "https://github.com/michael-reichenauer/gmd/releases/latest/download/$ASSET"; then
  echo "Failed to download $ASSET"
  exit 1
fi
chmod +x ~/gmd/gmd

for PROFILE_FILE in "${PROFILE_FILES[@]}"; do
  if [ -f "$PROFILE_FILE" ] && grep -q 'export PATH=$PATH:~/gmd' "$PROFILE_FILE"; then
    continue
  fi
  echo 'export PATH=$PATH:~/gmd' >>"$PROFILE_FILE"
done

echo "gmd version: $(~/gmd/gmd --version)"
echo "To enable, run: . ${PROFILE_FILES[0]}"
