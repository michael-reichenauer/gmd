# Workflow rules and decisions

How gmd is worked on, beyond CLAUDE.md. Always loaded, in local and cloud sessions alike. The traps
of one area are in the files beside this one, which load with the files they are about.

## Merges and releases

- **Review before merging into `dev`.** Have the commits to be merged (`git log dev..<branch>` and
  their diffs) reviewed in a fresh context, one that did not write them, since the author's own
  context hides what a reviewer finds: the `code-review` skill on the branch against `dev`, or a new
  agent given the branch, the range and CLAUDE.md's bar (correctness first, the conventions, a test
  with each fix). Skip it only when those commits were reviewed already. Fix the findings that hold
  up on the branch, as commits of their own, run `scripts/test` again, and only then merge. Say what
  the review found and what was fixed, and ask the user about the findings that are judgment calls.
- **A branch reaches `dev` through a pull request, merged when the user asks**, and the merge is
  Claude's own: `git merge --no-ff` of the branch into `dev`, pushed, which GitHub takes as merging
  the pull request; not GitHub's merge button.
- **Merge messages describe the change; they are no longer the release notes.** A merge into `dev`
  (`git merge --no-ff <branch> -F <file>`, the message written to the file first, since
  `git merge -F -` does not read stdin) has a body of `- ` lines saying what the branch changes,
  for users where it does, which the `release` skill reads as raw material. The `Co-Authored-By`
  line stays last.
- **Releases are made by the Release workflow only, when the user asks** (the `release` skill,
  docs/RELEASING.md), the user's ask being the go: it writes the notes in `CHANGELOG.md`'s
  `## [Unreleased]` on `dev` and runs the workflow at once, which makes the version commits and the
  `--no-ff` merge into `main`. Never merge into `main` or push it by hand, and never merge `main` into `dev`: the
  workflow raises dev's minor itself, so there is nothing to bring back.
- **A release re-records the README's pictures first**, unasked: `scripts/demo` on `dev` writes
  `gmd/doc/Animation.gif`, `GitLog.gif` and `GmdLog.gif`, committed on `dev` before the workflow
  runs so that the release carries them, and only if they changed (a recording is the same byte for
  byte while the UI is). If `DemoTest` fails, say so rather than release without them, and keep it
  passing when a change breaks its script. Never re-record per step, at a merge into dev or for a
  preview (it is slow, and once timed out flakily); say in the report when a change alters what the
  demo shows.

## The architecture document

`docs/ARCHITECTURE.md` is the overview of how gmd is built, in numbered views, and its diagrams are
written by `docs/architecture/generate.py` (Python 3 alone), never drawn by hand. It drifts unless
it moves with the code, so:

- **A change that alters what a view shows updates the prose, the generator and the regenerated
  SVGs in the same pull request**, each diagram rendered and looked at before it is committed (the
  last section of the document says how). What counts: a folder of `gmd/` (a layer, a `Cui/`
  feature folder) added, removed or renamed; a dependency between them in a new direction, above
  all an upward one or one past a layer; a new process gmd starts, external program, network
  service, file or ref gmd writes, or setting it stores (keep the README's "What Gmd Stores and
  Sends" in step too); a change to the read, refresh or command flow (`RepoView`, `Server`,
  `AugmentedService`, the inference pipeline's stages); a change to `build.yml`, `release.yml` or
  another workflow, or to how a release reaches users; a design decision made or reversed; a risk
  that appeared or went away. A bug fix, or a change inside one module that no view shows, needs
  nothing.
- **Before opening a pull request**, check the change against the views and say in it whether the
  document needed a change.
- **When a release is made, or when the user asks whether the document is current**, run
  `python3 docs/architecture/generate.py --count`, put the numbers into the prose and the table at
  the top, regenerate the SVGs, and check each view against the code; commit it on `dev` with the
  release's other preparations.

## Showing UI work

Screenshots of finished UI work are wanted only while the user is away from a computer: in a cloud
session started from a phone or a tablet, or when they say so. At a computer they try gmd
themselves, and a picture is only overhead. How to render one: `.claude/rules/ui.md`.

## Conventions decided

- **Results are C# 15 unions matched on the case type, with no `Try` helper** (2026-09). The custom
  `Try(out value, out e, result)` went because a newcomer cannot read it at a glance, and a plan that
  kept `Try` to save churn was rejected as missing the point. When proposing a refactor, optimize for
  what a newcomer recognizes rather than for the smallest diff, conventional C# over a clever shim.
  The idioms are in CLAUDE.md, "Errors".

## Running the tests

- `scripts/test` prints each failure where it happens, with its message, and the summary at the end
  only counts them, so the tail of the output names no failed test. Keep the whole output,
  `scripts/test 2>&1 | tee <scratchpad>/test.log`, and find the failures with
  `grep -A12 "^  Failed " <scratchpad>/test.log`: a failure seen once and not named cannot be told
  from a flake.

## Shell traps

- `pkill -f` and `pgrep -f` match the Bash tool's own command line, so a pattern that also appears in
  that command kills the tool's shell and silently skips the rest. Kill by pid: loop over `pgrep`
  and skip `$$`.

## Decided or parked: don't re-propose unprompted

- **How gmd is installed** (as of 1.8, 2026-10-15): the Homebrew tap, `install.sh`, `install.ps1`
  (the way on Windows) and the dev container feature; `gmdSetup.exe` stays a release asset but is not
  the README's way. Declined: winget, whose validation sandbox had Defender block the submission, and
  signing `gmd.exe`, which would not remove SmartScreen's warning anyway. gmd still knows a winget
  install (`PackageManager.Winget`), on purpose. Postponed: Scoop (two commands to install) and a
  dotnet tool (it needs the .NET SDK). Still open: a build for Intel Macs.
- **A logo** (explored 2026-10-20, postponed until it adds value). If it is taken up, start from the
  candidate that worked, a "g" drawn in the graph's own glyphs, 5 columns by 4 rows:

  ```
  ╭───┨     the bowl (╭ ─ │ ╰) in branch blue #42A5F5: a branch that leaves main and merges back
  │   ┃     the stem and descender (┨ ┃ ┛ ━ ╺) in bright magenta #B4009E, main's line, which rises
  ╰───┨     half a row above the bowl, like history going on above
  ╺━━━┛
  ```

  Dropped: pure graph marks (read as a "P" or "þ"), box-drawing "gmd" wordmarks (just a font), and
  commit ticks or hidden-branch marks on the g (read as glitches). A vector trace of the same geometry
  (stroke 6 in a ~50x83 viewBox, corner radius 9, round caps) still reads as a g at 48 px on GitHub's
  dark and light. Where it pays off: GitHub's social preview card and the README header, and beside
  the version lines of the About box; on the start page only with the name and a tagline, since alone
  at 16 px it reads half as a bracket. Not the Windows exe icon (the installer is prebuilt, rebuilt
  only on Windows). Judge drafts rendered with agg, as `scripts/demo` renders.
