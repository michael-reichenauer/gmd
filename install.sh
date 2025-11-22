#!/bin/bash
 
# usage: curl -sL https://raw.githubusercontent.com/michael-reichenauer/gmd/main/install.sh | bash
# usage: curl -sL https://github.com/michael-reichenauer/gmd/releases/latest/download/install.sh | bash

ARCH="$(uname -m)"
case "$ARCH" in
  x86_64|amd64)
    ASSET="gmd_linux_x64"
    ;;
  arm64|aarch64)
    ASSET="gmd_linux_arm64"
    ;;
  *)
    echo "Unsupported architecture: $ARCH"
    exit 1
    ;;
esac

echo "Downloading gmd ($ASSET) ..."
curl -fsS -L --create-dirs -o ~/gmd/gmd "https://github.com/michael-reichenauer/gmd/releases/latest/download/$ASSET" && chmod +x ~/gmd/gmd
if ! grep -q 'export PATH=$PATH:~/gmd' ~/.profile; then
  echo 'export PATH=$PATH:~/gmd' >>~/.profile
fi

. ~/.profile
echo "gmd version: $(gmd --version)"
echo 'To enable, run: . ~/.profile'
