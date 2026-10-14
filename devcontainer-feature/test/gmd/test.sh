#!/bin/bash

# The test of the dev container feature 'gmd', run in a container built with it, as the user it
# runs as, by 'devcontainer features test' (see .github/workflows/devcontainer-feature.yml)
set -e
# ldconfig is in sbin, which not every user has on the PATH
export PATH="$PATH:/usr/sbin:/sbin"

# shellcheck source=/dev/null # Given by the devcontainer CLI
source dev-container-features-test-lib

check "gmd says its version" bash -c "gmd --version | grep -E '^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$'"
# shellcheck disable=SC2016 # Expanded by the bash it is given to
check "gmd on the PATH links to the binary" bash -c '[ "$(readlink -f "$(command -v gmd)")" = /usr/local/share/gmd/gmd ]'
check "the binary's folder is the user's, so gmd can update itself" bash -c '[ -w /usr/local/share/gmd ]'
check "git is there" git --version
check "ncursesw is there, for the UI" bash -c "ldconfig -p | grep -q 'libncursesw\.so\.[56]'"

reportResults
