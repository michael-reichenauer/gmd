# Releasing Gmd

A preview is built from `dev` and a release from `main`, and both are published only when someone
asks for one, with the **Release** workflow (`.github/workflows/release.yml`). Merging into `dev` or
`main` only runs the tests. Anyone with write access can release, from a browser if need be:

1. **Write the notes.** Edit the `## [Unreleased]` section at the top of `CHANGELOG.md` on `dev`, in
   an editor or GitHub's web editor, and commit it to `dev`. The style is below.
2. **Run the workflow.** On GitHub: Actions, **Release**, **Run workflow**, with **Use workflow
   from** set to `dev`, and pick `preview` or `release`. Or from a terminal:
   `gh workflow run release.yml --ref dev -f kind=preview` (or `kind=release`).

With Claude, ask it to "make a preview" or "make a release": it writes the notes from what changed
since the latest release, shows them to you, commits them and runs the workflow (the `release`
skill, `.claude/skills/release/SKILL.md`).

## What the workflow does

**A preview** builds and tests `dev` as it is and publishes a GitHub pre-release tagged
`v<version>`, its notes the `Unreleased` section so far. It makes no commit. A preview with no notes
yet is allowed, and says so in a warning.

**A release**:

1. Refuses to run when `Unreleased` has no `- ` item, or when the version on `dev` is not above the
   latest release in major.minor.
2. On `dev`: renames `## [Unreleased]` to `## [v<version>] - <date>`, starts a new empty
   `## [Unreleased]` above it, and commits `Release v<version>`.
3. Merges `dev` into `main` with `--no-ff`, the notes as the merge message, and builds and tests that.
4. On `dev` again: raises `MinorVersion` in `gmd/Program.cs` by one, `Start v<major>.<minor>`, so
   the previews that follow carry the number of the next release.
5. Only when the tests have passed: pushes `dev` and `main` together, publishes the release on the
   merge, and updates the Homebrew tap. If either branch moved meanwhile, the push fails and
   nothing is released; run it again.

So `dev` never needs `main` merged back. The push is made with the workflow's own token, so a branch
protection rule on `dev` or `main` that requires pull requests needs a bypass for it.

If the run fails after its push, in publishing or the Homebrew step, the branches have moved but the
release is not out, and running it again is refused (the notes are used up). Publish it by hand from
the run's log, which prints the version and the merge's sha:
`gh release create v<version> --target <main's merge sha> --title "Version <version>" --notes-file <notes>`
with the assets that `scripts/build` makes, or ask Claude to. Leave **Publish** unticked for a dry run: it builds all four
platforms, tests, and prints the notes and the commits it would make, and changes nothing.

Before a release, re-record the README's pictures on `dev` with `scripts/demo` and commit them if
they changed, so the release carries them (`.claude/rules/workflow.md`).

## Versions

A version is `major.minor.days.minutes`, e.g. `1.15.1460.300`: `MajorVersion` and `MinorVersion`
from `gmd/Program.cs`, then the days since 2022-10-30 and the minutes since midnight of the build
time, in UTC (`gmd/Build.cs`, which the workflow injects the build time into). Gmd shows its build
time from it, and the updater compares versions as numbers. `dev` always holds the major.minor of
the next release; a new major is set by hand on `dev`, e.g. `MajorVersion = 2`, `MinorVersion = 0`.

## How the notes are written

The notes are for people who use Gmd, not for its developers:

- Group the items under `### New`, `### Improved`, `### Changed` and `### Fixed`, in that order,
  leaving out the groups with nothing in them.
- One item per change a user notices, as one line starting with a `- `.
- Describe how Gmd behaves now, in the present tense, never as an instruction to the reader: the
  group already says what kind of change it is. Under New, say what Gmd has or does ("**Publish** in
  a branch's menu publishes a branch that is not on origin yet"); under Improved and Changed, what is
  different with "now" or "no longer" ("`+` and `-` now step the context on the first file"); under
  Fixed, how it works now ("A click on a menu's border no longer runs the item next to it"). Not
  "Show ...", "Push ...", which read as commands.
- Leave out what users do not see: refactoring, tests, build and CI changes, and the README.
- Fold a later change to a feature into its item, rather than listing both.
- Write keys as `` `Shift-P` `` and menu items in bold, as the help (`gmd/doc/help.md`) names them.
- A mistake in the notes of a version already released can be fixed in `CHANGELOG.md`; its GitHub
  release keeps the notes it was published with, unless they are edited there too.
