#!/bin/sh

# Installs gmd, the dev container feature 'gmd' (devcontainer-feature.json), the latest release.
# Run as root as the image is built, with the user the container runs as in _REMOTE_USER, and that
# user's home in _REMOTE_USER_HOME.
#
# The binary goes in the user's ~/.local/share/gmd, and /usr/local/bin/gmd, on the PATH, links to
# it: gmd updates itself by replacing its binary, which it finds through the link, beside it, so the
# folder must be the user's, as /usr/local/bin is not. In the user's home, since on a Linux host the
# user's id is changed to the host user's as the container starts, and only the home is given the
# new id: a folder elsewhere was the old id's, which no one had any more. A rebuild of the container
# installs the latest release anyway.
set -e
# ldconfig is in sbin, which not every image has on the PATH
export PATH="$PATH:/usr/sbin:/sbin"

URL="https://github.com/michael-reichenauer/gmd/releases/latest/download"
BIN="/usr/local/bin"
USER_NAME="${_REMOTE_USER:-root}"
if ! id "$USER_NAME" > /dev/null 2>&1; then
  USER_NAME="root"
fi
USER_HOME="${_REMOTE_USER_HOME:-$(getent passwd "$USER_NAME" | cut -d: -f6)}"
SHARE="$USER_HOME/.local/share/gmd"

if [ -f /etc/alpine-release ]; then
  echo "There is no gmd for Alpine, since its binaries need glibc, which Alpine has not" >&2
  exit 1
fi

case "$(uname -m)" in
  x86_64 | amd64) ASSET="gmd_linux_x64" ;;
  aarch64 | arm64) ASSET="gmd_linux_arm64" ;;
  *)
    echo "There is no gmd for $(uname -m), only for x86_64 and arm64" >&2
    exit 1
    ;;
esac

# Besides itself gmd needs git, ncursesw, which its UI is drawn with, and ICU, which .NET reads
# text with. A Debian or Ubuntu image is given those it lacks; another image is to have them.
if command -v apt-get > /dev/null 2>&1; then
  PACKAGES=""
  command -v curl > /dev/null 2>&1 || PACKAGES="$PACKAGES curl ca-certificates"
  command -v git > /dev/null 2>&1 || PACKAGES="$PACKAGES git"
  ldconfig -p | grep -q 'libncursesw\.so\.[56]' || PACKAGES="$PACKAGES libncursesw6"
  IS_ICU_MISSING=""
  ldconfig -p | grep -q 'libicuuc\.so' || IS_ICU_MISSING="yes"
  if [ -n "$PACKAGES" ] || [ -n "$IS_ICU_MISSING" ]; then
    export DEBIAN_FRONTEND=noninteractive
    apt-get update -y
    if [ -n "$IS_ICU_MISSING" ]; then
      # Named after its version, libicu72, libicu74, ..., which differs between releases
      PACKAGES="$PACKAGES $(apt-cache pkgnames libicu | grep -E '^libicu[0-9]+$' | sort -V | tail -n 1)"
    fi
    echo "Installing what gmd needs:$PACKAGES"
    # shellcheck disable=SC2086 # One word per package
    apt-get install -y --no-install-recommends $PACKAGES
    rm -rf /var/lib/apt/lists/*
  fi
fi

echo "Downloading gmd ($ASSET) for $USER_NAME ..."
# Each folder made the user's, ~/.local too should it be made here, which other tools write to
for FOLDER in "$USER_HOME/.local" "$USER_HOME/.local/share" "$SHARE"; do
  if [ ! -d "$FOLDER" ]; then
    mkdir "$FOLDER"
    chown "$USER_NAME:$(id -g "$USER_NAME")" "$FOLDER"
  fi
done
curl -fsSL -o "$SHARE/gmd.download" "$URL/$ASSET"

# Checked against the checksums of the release, as install.sh and gmd's own updates check theirs
EXPECTED="$(curl -fsSL "$URL/SHA256SUMS" | awk -v f="$ASSET" '$2 == f { print $1 }')"
ACTUAL="$(sha256sum "$SHARE/gmd.download" | awk '{ print $1 }')"
if [ -z "$EXPECTED" ] || [ "$EXPECTED" != "$ACTUAL" ]; then
  rm -f "$SHARE/gmd.download"
  echo "The download of $ASSET does not match its checksum in SHA256SUMS" >&2
  exit 1
fi
chmod 755 "$SHARE/gmd.download"
chown "$USER_NAME:$(id -g "$USER_NAME")" "$SHARE/gmd.download"
mv -f "$SHARE/gmd.download" "$SHARE/gmd"
ln -sf "$SHARE/gmd" "$BIN/gmd"

# With a home of its own, since gmd writes its config and log there, which root's would keep
TEMP_HOME="$(mktemp -d)"
echo "Installed gmd $(HOME="$TEMP_HOME" "$SHARE/gmd" --version)"
rm -rf "$TEMP_HOME"
