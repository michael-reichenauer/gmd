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
        # Only Apple Silicon is released (see scripts/build), so there is nothing to download
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

# A piped script cannot change the PATH of the shell that ran it, so gmd is linked into a folder
# that is on that PATH already, which makes it a command at once, in this shell and in every other.
# The user's own folders first, then /usr/local/bin when it can be written without sudo, e.g. as root.
# The binary stays the user's in ~/gmd, beside which gmd updates itself, finding it through the link.
is_on_path() {
  case ":$PATH:" in
    *":$1:"*) return 0 ;;
  esac
  return 1
}

link_gmd() {
  local DIR="$1"
  local LINK="$DIR/gmd"
  is_on_path "$DIR" || return 1
  # Never over another gmd: a file, or a link to one installed some other way (the dev container
  # feature's in /usr/local/bin), only over a link of this script's or one that leads nowhere
  if [ -L "$LINK" ]; then
    if [ -e "$LINK" ] && [ "$(readlink "$LINK")" != "$HOME/gmd/gmd" ]; then
      return 1
    fi
  elif [ -e "$LINK" ]; then
    return 1
  fi
  if [ -d "$DIR" ] && [ -w "$DIR" ]; then
    ln -sf "$HOME/gmd/gmd" "$LINK" 2>/dev/null
  elif [ ! -e "$DIR" ] && [ "${DIR#"$HOME"/}" != "$DIR" ]; then
    mkdir -p "$DIR" 2>/dev/null && ln -sf "$HOME/gmd/gmd" "$LINK" 2>/dev/null
  else
    return 1
  fi
}

LINKED=""
for DIR in "$HOME/.local/bin" "$HOME/bin" /usr/local/bin; do
  if link_gmd "$DIR"; then
    LINKED="$DIR/gmd"
    break
  fi
done

echo "gmd version: $(~/gmd/gmd --version)"
if [ -n "$LINKED" ]; then
  echo "gmd is ready to use, linked as $LINKED"
  exit 0
fi

# No folder on the PATH could take the link, so ~/gmd is added to the PATH of the shells to come
for PROFILE_FILE in "${PROFILE_FILES[@]}"; do
  if [ -f "$PROFILE_FILE" ] && grep -q 'export PATH=$PATH:~/gmd' "$PROFILE_FILE"; then
    continue
  fi
  echo 'export PATH=$PATH:~/gmd' >>"$PROFILE_FILE"
done

echo "To enable, run: . ${PROFILE_FILES[0]}"
