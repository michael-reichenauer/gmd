---
paths:
  - "gmd/Installation/**"
  - "gmdTest/Installation/**"
  - "install.sh"
  - "install.ps1"
  - "scripts/brewformula"
---

# The updater and the installers

Beyond CLAUDE.md's gotcha "The updater of one version installs the next":

- Check every change to the updater very carefully, as the maintainer asked: a gmd whose updater is
  broken cannot update itself out of it, and only a reinstall by hand (`install.sh`, `install.ps1`)
  helps, so a bug ships silently and surfaces one release later.
- Test the swap and the download with failures injected, not only the path that works, and keep
  what the gmd versions out there rely on (CLAUDE.md lists it).
- Which install channels there are, and which were declined, is decided: `workflow.md`, "Decided or
  parked".
