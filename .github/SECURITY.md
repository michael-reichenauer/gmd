# Security Policy

## Reporting a Vulnerability

Please report a vulnerability privately, through
[Report a vulnerability](https://github.com/michael-reichenauer/gmd/security/advisories/new) on the
Security tab, rather than in an issue. Say what an attacker could do, and how to make it happen if
you know. The report and the fix stay private until a release with the fix is out, and the advisory
can then credit you.

## Supported Versions

Fixes go into the next release. Gmd updates itself to the latest release, so only the latest is
supported; `gmd --update` gets it, and **About** in the repo menu says which version you have.

## What Is Worth Reporting

Anything that lets someone other than you make gmd do something, for example:

- A repository, a branch name, a commit message or a remote's answer that makes gmd run a command,
  or write a file outside what was asked for.
- The update: gmd downloads its new version from this repository's releases and replaces itself.
- The install script, `install.sh`, and the checksums it checks its download against.
- The login dialog: gmd answers ssh's and git's questions for the git it started, over a pipe of
  its own (`Askpass`).
- What gmd pushes to origin: the branch structure, kept in `refs/gmd-metadata-key-value/data`, when
  the shared branch structure is turned on.
