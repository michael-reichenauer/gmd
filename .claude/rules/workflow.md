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
- **Merge messages are release notes**, since CI makes the notes from them: a preview's are the lists
  of dev's first-parent merges since the latest release, and a release's, with its section of
  `CHANGELOG.md` (`Server/Private/ChangeLog.cs`), the lists of the merges on main's first-parent line.
  A merge into main with no list (562b9ddf) made a release with no notes. So every merge into `dev`
  and into `main` has a body of `- ` lines, one per change a user notices, written for users: each
  starts with a verb, as `CHANGELOG.md` reads ("Publish a branch not on origin from its branch menu
  ..."), with no class or file names and no test or refactoring items, and keys and menu items
  written as the help writes them. The `Co-Authored-By` line stays last.
  - Into dev: `git merge --no-ff <branch> -F <file>`, with the message written to the file first,
    since `git merge -F -` does not read stdin. The list is what the branch changes for users.
  - dev into main: the items of every dev merge since the latest release
    (`git log --first-parent --merges <last tag>..dev`), as one list without duplicates, a later
    change to a feature folded into its item, verb first even where a dev item was not.
- **Merges between `dev` and `main` are `--no-ff` both ways**, main into dev after a release too
  (CLAUDE.md, the version gotcha). A fast-forward once put dev on main's first-parent line, and gmd,
  which follows those lines, then showed dev deleted at its old tip and made anew from the release
  commit. Check that `git log --graph --first-parent dev` keeps dev's own line before pushing.
- **A merge into main re-records the README's pictures first**, unasked: `scripts/demo` on `dev`
  writes `gmd/doc/Animation.gif`, `GitLog.gif` and `GmdLog.gif`, committed on `dev` so that the
  release carries them, and only if they changed (a recording is the same byte for byte while the UI
  is). If `DemoTest` fails, say so rather than merge without them, and keep it passing when a change
  breaks its script. Never re-record per step or at a merge into dev (it is slow, and once timed out
  flakily); say in the report when a change alters what the demo shows.

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
  dotnet tool (it needs the .NET 10 SDK). Still open: a build for Intel Macs.
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
