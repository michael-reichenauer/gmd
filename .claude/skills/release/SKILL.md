---
name: release
description: Make a gmd preview (from dev) or release (from main) - write the user-facing notes in CHANGELOG.md's Unreleased section, get the user's go, and run the Release workflow. Use when asked to make, publish or cut a preview, pre-release or release.
---

# Make a preview or a release

docs/RELEASING.md is the process and the style of the notes; read it first. You do the part a person
would: write the notes, then run the workflow. The workflow does all the git work of a release (the
version commit, the merge into main, the minor raised after), so never do it by hand.

A preview or a release cannot be taken back once published: show the notes and get the user's go
before step 5, every time, even when they asked for the release.

## 1. Find what is new

- On `dev`, up to date: `git fetch origin dev main --tags` and work on `origin/dev`.
- The latest release, as the workflow reads it: `gh release view --json tagName --jq .tagName`, or
  the GitHub tools' latest release. Its tag is on main's merge; the range is `<tag>..origin/dev`.
- Read everything in it: `git log --first-parent <tag>..origin/dev` for the merges and their lists,
  `git log <tag>..origin/dev` for the commits, the diff of `gmd/doc/help.md` and `README.md`, and
  the code wherever a message does not say what a user sees.
- Read the `## [Unreleased]` section already in `CHANGELOG.md` (`scripts/changelog unreleased`):
  someone may have written notes there, which are kept unless they are wrong.

## 2. Write the notes

Rewrite `## [Unreleased]` as a whole, from the whole range, rather than append to it, in the style of
docs/RELEASING.md: grouped `### New`, `### Improved`, `### Changed`, `### Fixed`; one item per change
a user notices; internal work left out; a later change folded into its feature's item; keys and menu
items as the help writes them; spelling checked. Compare with the latest releases in `CHANGELOG.md`
for tone and length. Do not invent: a change you cannot place goes to the user as a question.

## 3. Check before asking

- For a release: the version on `dev` (`MajorVersion`, `MinorVersion` in `gmd/Program.cs`) is above
  the latest release's major.minor; if not, say so rather than raise it yourself.
- For a release: re-record the README's pictures with `scripts/demo` on `dev` and commit them if they
  changed (`.claude/rules/workflow.md`). If the demo fails, say so and ask.
- CI on `dev`'s tip is green (the Build workflow), or say what is red.

## 4. Ask

Show the user the notes as they will be published, the kind (preview or release) and the version
it will have (`<major>.<minor>.<days>.<minutes>`, the last two from the build time). Wait for a go.
Change the notes as they ask, and show them again if the change is more than a word.

## 5. Publish

- Commit the notes on `dev`, `Write the notes of the next release` (or `... of v<major>.<minor>`), and
  push `dev`. That is the one push to `dev` this makes; the user's go covers it.
- Run the workflow on `dev`: the GitHub tools' workflow run (`release.yml`, inputs `kind`, `publish`)
  or `gh workflow run release.yml --ref dev -f kind=<preview|release>`.
- Follow the run to its end. When it fails, read the failed step's log, say what failed, and fix
  what is yours (the notes, the version); a release that failed before its push changed nothing and
  can simply be run again.
- Report the release's link and its version. After a release, `dev` is at the next minor, and
  `origin/dev` has the workflow's two commits: pull before working on `dev` again.
