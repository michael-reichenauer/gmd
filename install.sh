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

URL="https://github.com/michael-reichenauer/gmd/releases/latest/download"

# Downloaded beside gmd and moved over it once checked, so a gmd that is running is replaced
# rather than written into, which Linux refuses ("Text file busy")
echo "Downloading gmd ($ASSET) for $OS/$ARCH ..."
mkdir -p ~/gmd
if ! curl -fsS -L -o ~/gmd/gmd.download "$URL/$ASSET"; then
  echo "Failed to download $ASSET"
  rm -f ~/gmd/gmd.download
  exit 1
fi

# Every release from this script on has the checksums of its files, the older ones have none
if SUMS="$(curl -fsS -L "$URL/SHA256SUMS" 2>/dev/null)"; then
  EXPECTED="$(echo "$SUMS" | awk -v f="$ASSET" '$2 == f { print $1 }')"
  if command -v sha256sum >/dev/null; then
    ACTUAL="$(sha256sum ~/gmd/gmd.download | awk '{ print $1 }')"
  else
    ACTUAL="$(shasum -a 256 ~/gmd/gmd.download | awk '{ print $1 }')"
  fi
  if [ -z "$EXPECTED" ] || [ "$EXPECTED" != "$ACTUAL" ]; then
    echo "The download of $ASSET does not match its checksum in SHA256SUMS"
    rm -f ~/gmd/gmd.download
    exit 1
  fi
  echo "Checksum verified"
else
  echo "This release has no SHA256SUMS, so the download was not verified"
fi
chmod +x ~/gmd/gmd.download
mv -f ~/gmd/gmd.download ~/gmd/gmd

for PROFILE_FILE in "${PROFILE_FILES[@]}"; do
  if [ -f "$PROFILE_FILE" ] && grep -q 'export PATH=$PATH:~/gmd' "$PROFILE_FILE"; then
    continue
  fi
  echo 'export PATH=$PATH:~/gmd' >>"$PROFILE_FILE"
done

echo "gmd version: $(~/gmd/gmd --version)"
echo "To enable, run: . ${PROFILE_FILES[0]}"
