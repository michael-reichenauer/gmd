# Modernization notes

Gmd was modernized in a series of small, reviewed steps between 2026-07-26 and 2026-09-02, before
serious feature work resumed. This file is the summary: what was done, briefly, and the findings
and open issues worth keeping. The full step-by-step record — every finding and every
verification, some 3000 lines of it — is in this file's git history up to commit `7705b0c`.

Add new open issues and findings here as work lands; keep them short and drop them when they close.

---

## What was done

**Toolchain and build**

- .NET 8 → .NET 10, Terminal.Gui 1.17.1 → 1.19.0 (which fixed the 100% CPU spin on Linux and
  macOS), Autofac 9, DiffPlex 1.9, MSTest 4. Four unused packages and all stale .NET 7 references
  removed.
- CSharpier is the single formatter: on save, on build, on commit and in CI. `.editorconfig` holds
  only naming and non-layout rules. `.git-blame-ignore-revs` hides the bulk reformat from blame.
- CI runs on every branch: a fast test job for feature branches and pull requests, the full
  multi-platform build and release for `main`/`dev`. `./build` now fails when a publish fails, and
  `build.bat` mirrors it.

**Tests: from 2 to about 900**

- The fixtures described in `CLAUDE.md`: `FakeCmd` (canned git output), `RepoBuilder` (a repo
  declared in a few lines and run through the real inference pipeline), `GraphText` / `DiffText` /
  `ScreenText` (drawn output as a picture), `TempRepo` (real git in a throwaway folder) and
  `TmuxSession` / `E2eRepo` (the built binary in a real pty).
- What they cover: every git output parser; the whole augmentation pipeline, as characterization
  tests; graph rendering and its colors; the utilities; the view logic that was pulled out of the
  views (hoover, scrolling, selection, menus, blame and conflict math); the diff body; the log row
  writer; the commit filter; the menu predicates; and about 65 end-to-end tests over the log view,
  menus, dialogs, diff, blame, the conflict resolver and every repo-mutating key.
- The test process and every gmd it starts run under a throwaway `$HOME`, so `./test` no longer
  truncates the developer's `~/gmd.log`, rewrites their config or overwrites their clipboard.

**Structure**

- The layering is clean: nothing below `Cui/` references Terminal.Gui or `gmd.Cui`. `FileMonitor`
  reaches the main loop through `IMainThread`; the clipboard no longer goes through Terminal.Gui.
- Large files were split along their seams so the deciding half is testable without a terminal:
  `BranchStructureService` (989 lines → the pipeline plus six stage and rule classes), `RepoView`
  (→ `RepoViewInput`, `Hoover`), `ContentView` (→ `ContentScroll`, `ContentSelection`), `Menu`
  (→ `MenuDimensions`, `MenuRows`), `UIDialog` (→ its six views), `AugmentedService`
  (→ `BranchWriteService`, `Uncommitted`) and `BranchCommands` (→ push/pull and create
  commands). Each split was verified as pure movement, and the inference split by a before/after
  dump of a real repo of about 1750 commits.
- Collection expressions adopted (107 mechanical sites), the two `Converter`s renamed
  `WorkRepoConverter` / `ViewRepoConverter`, and dead `NoWarn` entries removed.

**The result type is a C# 15 union** (2026-09-11)

- `Result<T>` is a union of the value and an `Error`, and `Result` one of `Success` and `Error`, in the shape
  the C# 15 compiler recognizes (`[Union]`, a constructor per case type, `object? Value`; the
  optional non-boxing `HasValue` / `TryGetValue` members are left out, since the contents are one
  boxed object anyway). Every `Try(out value, out e, …)` site — about 480 across both projects
  — became a pattern on the case type (`if (result is not Status status) return result.Error;`,
  `is Error e`, an exhaustive `switch`), the `R.Error(…)` factory became `new Error(…)`, the tests
  assert with `AssertOk` / `AssertError`, and `Try` is gone. So are the mutable static `Ok` field
  (`static readonly` now), the checked-flag state machine behind `GetResultValue`, the reflection
  into `Exception._remoteStackTraceString` and the implicit conversion to `bool`. The style is in
  CLAUDE.md under Conventions.
- Renamed `R` / `R<T>` to `Result` / `Result<T>` (2026-09-12), so the type reads without
  explanation in another project, and `Result.cs` no longer calls gmd's `Asserter`: misuse throws
  `ResultException`, an `InvalidOperationException` that `Result.Catch` lets through and gmd's
  unhandled-exception path logs and shows. So `Result.cs`, `UnionPolyfill.cs` and the test helper
  `ResultAssert.cs` depend on nothing but the BCL and can be copied to another project as they are
  (one namespace line each).
- The compiler is the .NET 11 RC1 SDK's (`global.json`; `LangVersion preview` in
  `Directory.Build.props`), the target framework is still net10.0, and `UnionAttribute` / `IUnion`
  are polyfilled in `gmd/Utils/UnionPolyfill.cs`. CSharpier 1.3.0 parses no C# 15 syntax, which is
  why the union is a hand-written struct rather than a `union` declaration. A missing switch arm
  (CS8509) is a build error.
- Found on the way: `Task<Result>.RunInBackground()` bound to the `Task` overload and dropped an error
  result silently (the fetch sites in `RepoView`, which now log their expected failure at Debug
  themselves, since the new overload warned on every refresh of a repo with no `origin`);
  `IsUpdateAvailableAsync` and `CloneDlg.Show` returned tuples, which a union pattern cannot bind
  by name, so they return records; `UpdateChangeLog` wrote an empty `CHANGELOG.md` when reading
  the log failed.

**Features added on the way** (each documented in `gmd/doc/help.md`)

- Merge the current branch *into* another branch (`E`, and `Merge to` in the branch menu).
- Blame a file: runs of one commit bracketed in the gutter, age heat, drill-down with `P`.
- Adjustable diff context per file (`+` / `-`: 6 → 15 → the whole file).
- Merge conflict resolution: detection of merge, rebase, cherry-pick, revert and am; abort,
  continue and skip from the repo menu; a two or three pane resolver with the common ancestor
  recovered on demand; hand-edited hunks; file-level choices for delete, add and binary conflicts;
  and commit gating that refuses unresolved files and leftover markers.
- Worktrees: gmd opens inside a linked worktree; a branch checked out in another worktree carries
  `⌂` and `S` opens that folder instead of a checkout git would refuse; `⌂N` in the top bar counts
  the other worktrees and turns yellow when one has uncommitted changes (read after the repo is
  shown, so a slow status in a large worktree never delays it, and re-read every thirty seconds,
  since their folders are not watched); a dialog (`W`) lists them with changes, in use
  (locked), missing (prunable) and merged, and adds (beside the repo, in Claude Code's
  `.claude/worktrees/` or in `.worktrees/`, the two inside the repo added to `.gitignore`),
  removes (with the branch, force for uncommitted changes) and prunes them. One `.gmdconfig` per
  repository, in the common git dir, shared by every worktree.

**Bugs fixed** (the ones a user could hit; all have regression tests)

- The log failed entirely under `ar-SA` and showed years hundreds off under `th-TH` / `fa-IR`:
  dates were parsed and formatted with the current culture. Invariant culture everywhere now.
- Copy: macOS had none at all, Linux required `xsel`, and `xclip`'s forked helper hung the UI for
  as long as the text stayed on the clipboard. Rewritten as a chain of writers ending in OSC 52.
- Opening a repo deleted local tags that were not on the remote (`fetch --prune-tags`).
- The fix for that stopped the fetch from fetching branches at all: a refspec on the command line
  replaces `remote.origin.fetch` rather than adding to it, so `r` and the five-minute fetch updated
  tags only, and a new remote commit showed up only when something else happened to fetch.
- Opening the diff view during a `rebase --apply` or `am` conflict staged the markers and destroyed
  the conflict; `commit -a` committed markers into history. Both are now guarded on any operation.
  The diff has since stopped touching the index at all: it stages into a copy (`GIT_INDEX_FILE`),
  since its `git add .` then `git reset` also wiped whatever the user had staged with other tools.
- `Continue Rebase`, and `./test`, hung for anyone with `GIT_EDITOR` set. `Cmd.NeverOpenAnEditor`.
- A repo change within half a second of a read was taken as seen by it, so a `git fetch` in another
  terminal landing just after gmd read stayed unseen until something else changed; one made while a
  read ran, or while the search was up, was dropped outright. The log view now skips only a change
  told of before the read started (`ChangeEvent.IsSeenBy`), and one that comes during a read or a
  search is looked at again once the next repo is shown. Dating a change by the file's modification
  time instead was tried and lost renames: a moved file keeps its old time, so it looked seen.
- Pulling a diverged branch failed, with git's dozen lines of hints as the error, for anyone who
  has not set `pull.rebase`, which recent git refuses to guess. gmd asks once and saves the answer.
- Pull all stopped at the first diverged branch, leaving every branch after it unpulled; the branch
  menu's `Pull/Update` on the current branch ran a fetch git refuses outright.
- Squash refused unpushed commits and allowed pushed ones; Uncommit was offered with a dirty tree
  on an unpushed branch; push all tried to push diverged branches.
- Inference: with no `main` / `master` / `trunk` the root branch was whichever git listed first; a
  commit below a branch point went to the wrong child when the other child had a merge-subject
  name; four merge-subject forms lost their `into` name; a stopped rebase lost the current branch;
  a local branch behind its remote at the root commit crashed the log view.
- Smaller: Shift+Up selected two rows per press; `MoveToTop` stopped short when scrolled; the filter
  dialog covered the first result and discarded its own "no matches" row; `Q` did not quit;
  refreshing a stash, range or file-history diff fetched the wrong thing; `Build.Version()` threw
  for a local build made just after midnight (build time is local, the base time was UTC); a
  user-assigned commit's `Φ` lost its white; `diff3`-style conflicts drew the ancestor as part of
  "ours"; the resolver's `]` / `[` skipped the first conflict and its upper-case shortcuts fell
  through to the log view, where `P` is push all.
- Every deleted branch was named `branch(n)` instead of the name recovered from its merge subject
  (`Merge branch 'x' into dev` no longer gave `x`): the split of `BranchStructureService` gave three
  stages a dependency on the stateful `BranchNameService`, and the container, resolving per
  dependency, handed each stage its own empty cache. It is `[SingleInstance]` now, and one pipeline
  test resolves from the real container.
- Reading a repo never ended when a GitHub pull request merge named its head `owner/dev` and dev's
  tip had a third candidate, e.g. a feature just started there: the name was matched by its ending,
  to the local `dev` listed before `origin/dev`, and the local branch became the parent of its own
  parent, since the remote branch then owned nothing. That was the cycle the commented-out guard in
  `DetermineAncestors` was written for. A name now goes to the remote branch when both match, an
  ending only matches after a `/`, and the guard is back (a cycle is logged and the branches in it
  left out of the view, since `Sorter.Sort` would not end on them either).
- A commit merged by id (`git merge <sha>`, subject `Merge commit '<sha>' into dev`) was recovered
  as a deleted branch named after the 40-character id. `commit` is not a branch keyword any more:
  the subject still says which branch the merge is on, but nothing about where the merged commit
  was. Verified on this repo's history: one branch renamed to `branch`, nothing else moved.
- Safety, from the usability review (`USABILITY.md`, Tier 1, 2026-09-25):
  - Any key the diff, blame or conflict view did not use fell through to the log view: `P` in a
    diff pushed every branch, and `c` in the resolver closed it with its decisions unsaved.
  - Esc in the log view quit on the spot, and so did a click on the top bar's `X`. Both ask now,
    with Yes the default.
  - `p` force-pushed (`--force-with-lease`) every branch that had a remote, not only after
    *Force Push* was chosen.
  - Enter on *Binary Files Detected* discarded the binary changes, and Esc on *Unsaved Decisions*
    discarded the decisions.
  - Discarding all changes or a file, dropping a stash and removing a tag (on origin too) never
    asked first.
  - A click on ▲ / ▼ pushed or pulled every shown branch, and a middle click merged with no
    question.
  - A failed clone or init from the start menu left a blank screen, and a click beside the start
    menu quit gmd.

---

## Open issues

**Product**

- `USABILITY.md` holds the usability review's proposals not yet done: Tiers 2 to 4, and the
  small bugs found along the way.
- F5 does nothing inside the diff, blame and conflict views, where it only ever worked by falling
  through to the log view; `r` refreshes a diff. (`?` and F1 are registered there now.)
- *Force Push* is `--force-with-lease` with no expected value, so the lease is the remote-tracking
  ref, which gmd's background fetch keeps moving. A fetch that lands between the screen being drawn
  and the push makes the lease pass over commits the user never saw. Pass the tip the user saw
  (`--force-with-lease=<branch>:<sha>`).
- `DeleteTag` deletes a tag on origin whenever the branch of the row's commit has a remote, not
  when origin actually has the tag, so the question may say 'on origin as well' for a tag that was
  never pushed.
- `CopyCommitId` / `CopyCommitMessage` are implemented on `IRepoCommands` but no key or menu item
  calls them. A commit-menu entry would also give macOS users a copy without Ctrl+C. Cmd+C cannot
  reach a terminal program at all: the terminal keeps it, the classic key protocol cannot express
  it, and Terminal.Gui 1.x has no Command modifier. An iTerm2 profile binding of Cmd+C to hex
  `0x03` is the workaround.
- `BlameView.ScrollToCommit` calls `SetCurrentIndex` before `ScrollToShowIndex`, which puts the
  cursor on the last visible row rather than the target. Scroll first, then set the cursor.
- `UILabel.Text`'s setter sizes the label from the text it is *replacing*, so a header that grows is
  clipped. `BlameView` works around it with `Width = Dim.Fill()`; the setter is the fix.
- Pull all only considers shown branches; a hidden branch that is behind is neither pulled nor
  counted in `▼`. `help.md` says "all displayed branches", so arguably right.
- A repo with no commits still offers Uncommit (git refuses the reset). Ctrl+O is documented as
  activating OK but is bound nowhere; dialogs are accepted with Tab then Enter. The merge-from menu
  lists only shown branches, so with only `main` shown it is an empty box.
- Cosmetic and pinned by tests: a cut sid, author or time column carries no `┅` marker (the
  subject column does); a binary file is headed `Modified:`; `FileSize` never shows a fraction; a
  stash message is cut at its first `:`. (A staged added file used to be counted as modified, which
  stopped being cosmetic once discarding a file asked whether it was new; it is added now.)
- Worktrees, deliberately left out of v1: no unlock of a locked worktree (a second `--force`),
  no bulk clean-up, no auto-prune; the stash list is the repository's and so shows stashes made
  in other worktrees; other worktrees' folders are not watched, so their change counts are up to
  thirty seconds behind; a submodule's `.git` file now makes it a root of its own too, which is right
  but new; the worktree dialog needs about 80 columns.
- `FileStore` caches each file per process and never re-reads it, so with two gmd instances open
  (one per worktree, say) every write of `~/.gmdconfig` — the recent folders on each repo open, the
  git version on start, the update check, a word added to the spelling dictionary — is the writer's
  stale copy plus its own change, and whichever instance writes last drops what the others saved.
  `<repo>/.git/.gmdconfig` goes through the same store, so the same holds for a repo opened twice.
  Re-read the file before writing, or merge the lists.
- The clipboard on Windows (Win32, then `clip.exe`) and macOS (`pbcopy`) is not verified on
  hardware. Linux with no display is covered end to end; the tool path was checked with a stand-in
  `xclip` that forks a child holding the pipes, as the real one does.

**Inference pipeline**

- Adding the uncommitted commit sets its parent but does not add it to that parent's children,
  while removing it filters the child lists. Invisible today; worth knowing before relying on
  a commit's children.

**The union result, at .NET 11 GA (November 2026)** — the steps are in `UPGRADING.md`

- Set `LangVersion` to `15`. If the target framework moves to net11.0 (an STS release, where
  net10.0 is LTS), delete `gmd/Utils/UnionPolyfill.cs` and the explicit `LangVersion` too. When
  CSharpier parses C# 15, `union Result<T>(T, Error) { … }` is optional sugar; the struct already has
  what the keyword form would not (non-boxing access, the helpers in its body).
- The VS Code C# extension bundles its own Roslyn and may flag union patterns until it catches up;
  `dotnet build` is the truth. CI installs the SDK from `global.json` plus `10.0.x` for the runtime
  the net10.0 tests need; unverified until the first CI run on this branch.

**Refactoring roadmap** (from the 2026-09-11 review; A before B before C)

- A, correctness, each with a regression test: `FileStore` (the lost-update bug above, an
  unsynchronized cache, `FailFast` on a malformed config file); `Threading.AssertIsMainThread`
  compares thread ids with `>` and so passes on any lower id; 13 `async void` handlers
  (`DiffView` ×6, `Menu` ×3, `MainView` ×2, `BlameView` ×2) whose exceptions bypass the result path;
  `IsCircularAncestors` is write-only (below); `IDiffService` / `IBlameService` are declared in both
  `gmd.Cui` and `gmd.Git`, the silent DI takeover CLAUDE.md warns about — rename the Cui pair
  `*RowService`; `[SingleInstance]` is matched by the attribute's name string and `FileStore` uses
  `Activator.CreateInstance`; the vulnerability grep in `./build` never fails the build, `run.bat`
  drops its arguments, `log.bat` hard-codes one user's home, `installtools` sets `safe.directory`
  to `/workspaces/gmd`, `updatepackages` expands an undefined `$projectFile`, `gmd_linux` and
  `gmd_osx` are missing from `.gitignore`.
- B, build and CI: grow `Directory.Build.props` to the shared properties (`TargetFramework`,
  `Nullable`, `ImplicitUsings`, `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`) and add
  `Directory.Packages.props`, since warnings never fail a build today and the `warning`-severity
  rules in `.editorconfig` are IDE-only; `SourceRevisionId` is recomputed from `UtcNow` on every
  build; the workflow has no `concurrency` group, `timeout-minutes`, NuGet cache, `.trx` upload,
  coverage step, `NuGetAuditLevel` gate or Dependabot, and the fast tier never runs on Windows or
  macOS.
- C, structure: the six `*Commands` classes repeat four fields, a constructor and a `Do` forwarder,
  and 25 of 62 `Do(` bodies end in `Refresh(); return Result.Ok;` — a `CommandContext` and a
  `DoAndRefresh` would also give `RepoCommands`, `BranchCommands` and `BranchCreateCommands` their
  first unit tests; `Commit.IsInView` / `ViewIndex` / `More` and `Branch.X` / `IsIn` / `IsOut` are UI
  layout state inside the server model; `ConfigService` syncs a second `Config` by a reflective
  property copy and `Config` has two constructors; static leftovers (`Build.IsWindows` assignable,
  `BranchService.remotePrefix`, `MessageDlg.Clicked`, `Updater`'s static task cache, the single
  `UI.onActivated` slot); names (`Hoover` → `Hover`, `Creater` → `Creator` plus its cSpell entry,
  `ResentParentFolders`, `brandName`, `SetBranchManuallyAsync` is void, `GetViewRepoAsync` is
  synchronous, `TipID` vs `TipId`, `RepoConfigImpl`); dead code (`exampleRunes.cs`, the commented
  blocks in `UnicodeSetsDlg`, `ConfigDlg`, `MainView`, `ExceptionHandling`, `Cmd`, `TagService`,
  `BranchService`, `FileMonitor`, `WindowsClipboard`); 23 type names declared in both `gmd/Git` and
  `gmd/Server/Repo.cs`, some identical — share the leaf records only; no tests for
  `ProgramCommands` and `Updater`, and `FakeGit` implements 12 of 76 members.
- Not recommended: removing the one-implementation interfaces (they are the DI and test-double
  seams); renaming `Cui`; AOT (the assembly scan blocks it, and nothing needs it).

**Deferred, with the reasoning so it is not redone**

- **Terminal.Gui 1.x → 2.x.** When, not if. For: v1 is frozen (last commit June 2025); true color
  would lift the five-color branch palette, which a tool built on showing many branches runs out of;
  2.x's input injection reaches the views directly. Against: it is one branch that does not compile
  until finished — 32 files, ~11.7k lines, 9 custom views, with `ColorScheme`, `Toplevel`, the
  whole `Key` API, `Redraw`, `ScrollBarView`, `RootKeyEvent` and the tree view all replaced — and
  2.x still ships breaking changes inside minor releases. The end-to-end tier names no Terminal.Gui
  type and is the port's acceptance suite. Order when it starts: `Color.cs` + `ColorSchemes.cs`,
  `UI.cs`, `ContentView`, `UIDialog`, the two browse dialogs; `MessageDlg` and `BorderView` are
  deleted rather than ported. Trap: the migration guide describes `v2_develop`, not the released
  package; check the shipped assembly before believing any specific API.
- **Theme, the parts that wait for the 2.x port** (USABILITY.md, Tier 4 item 7):
  - *Light terminals.* Every color is on a forced black background (`Color.Make` pairs each with
    `Terminal.Gui.Color.Black`), so a light-themed terminal shows gmd as a black box: readable, but
    not the user's theme. The fix is the terminal's own default background, which 1.x has no color
    for and 2.x has. A second, light palette in 1.x is the alternative, and every screen's colors
    would need checking on it.
  - *More branch colors.* Five, picked by a hash of the name (`BranchColorService`), since the
    other 16-color entries already mean something (main, deleted, ahead, behind). Two of four
    shown branches often share one; `g` recolors by hand. Needs 256 or true color. Consider
    replacing red or green then too, for red-green color blindness.
  - *`NO_COLOR`.* Not read. Low value for gmd, whose graph tells branches apart by color alone;
    revisit if asked.
- **Inline conflict editing** (typing in the result pane with both sides in view). The modal `E`
  box covers the need. The gate is whether `SetFocus()` gives a `UITextView` the keyboard when it
  shares a bare `Toplevel` with `ContentView`s — a configuration nothing in the codebase has run,
  and a half-hour probe settles it. The cheaper change is to show both sides read-only inside the
  edit dialog (`UIDialog.AddContentView`, as `HelpDlg` does).
- **Combined diffs (`diff --cc`)** are skipped with a warning. Relaxing the `@@ ` check alone is
  harmful: `ParseSectionDiff` calls a bare `int.Parse` on `-1,1 -1,1 +1,1` and throws outside the
  `Result` handling. Full support needs n+1 `@` hunk headers, two-column line prefixes and a three-sided
  view; worth it as a feature, since it is the only way to show what a merge resolved by hand.
- Not built, by choice: blame `-w` / `-M` / `-C` toggles; a diff context below 6; word-level
  highlighting inside a conflict; submodule conflicts; `rerere`; marking local-only tags in the log
  view (the tag mirror makes it knowable); tag pruning for remotes other than `origin`.
- Coverage: `coverlet.collector` is referenced but nothing reports it in CI. Report it from the
  pull-request job first, then consider a floor as a ratchet.
- IDE0305 (`.ToList()` → `[.. x]`, 13 sites) by hand, since it can change the concrete type behind
  an `IReadOnlyList<T>`. Target-typed `new()` is an open style question.
- `gmdSetup.exe` is a committed prebuilt binary; Intel macOS is unreleased (`install.sh` now says so
  rather than downloading a `gmd_osx` that does not exist); `MajorVersion` / `MinorVersion` are
  hand-edited; there is no `.runsettings`.
- `gmdTest` cannot run in parallel: 4 of 15 parallel runs of it failed. `LogServiceTest` and
  `TimeDateExtensionsTest` change the default culture and `GitIntegrationTest` sets `GIT_EDITOR`,
  and there may be more. Nothing to gain either, since it takes about 3 s; only the end-to-end
  tests, in `gmdE2eTest`, run in parallel.
- Mouse interaction has no end-to-end test; it needs raw SGR sequences via `send-keys -H`.

**Test suite**

- Two end-to-end flake modes were seen and neither reproduced when chased: a `WaitFor` satisfied
  by text already on screen, so the key just sent is not actually waited for; and, in the
  devcontainer only, a blank pane with no `gmd.log` at all — the binary never started. Nine
  consecutive full runs were green afterwards. Seen once more on 2026-09-09
  (`TestRemoveWorktreeAndItsBranchFromTheDialog`, the `Kind    Branch` wait after `w`), and again
  not reproduced: a full rerun and 21 runs of the worktree tests were green. Once more on
  2026-09-24 (`TestStashPopBringsTheChangesBack`, message not captured), and green alone three
  times, in an E2e rerun and in a full rerun. Recorded so nobody hunts a flake that is not biting.
- tmux cannot report the exit code of a directly exec'd binary; a crash shows as a `WaitFor`
  timeout with the screen and the log tail in the message.
- The end-to-end tests were about four of `./test`'s four and a half minutes, nearly all waiting:
  every wait needs four identical captures 100 ms apart, so it costs at least about 330 ms after the
  screen is already right, and an idle gmd uses about 6 ms of CPU a second. So they run eight at a
  time, in a project of their own since MSTest sets parallelism per assembly: about 30 s, bounded
  by the thirty-second worktree re-read test. Measured clean with 8 workers on 2, 4 and 9 cores.
- Parallel tests that block starve the thread pool. A running end-to-end test holds a pool thread
  for all of its run (TmuxSession polls with `Thread.Sleep`), and each `Proc.Run` needs more pool
  threads for its output callbacks before `WaitForExit()` returns. With more workers than cores
  every tmux call waited for the pool to grow: six workers on two cores took ten minutes, with two
  tests failing on their 30 s timeouts. `gmdE2eTest/TestSetup.cs` raises the pool's minimum.
- Several `Down`s sent in one `send-keys` lose keys, in the log view and the diff view alike and
  with no git command running: in the log view two moved the cursor one row, five moved it three,
  ten moved it five. Sent one per call, all arrived. So one key per `Send` with a wait after it is a
  real constraint, and it is what makes `TestDiffContextIsSteppedPerFile` (36 single `Down`s) the
  second slowest test.
- The throwaway `$HOME` is Unix only; a Windows test run still truncates `~/gmd.log`. The terminal
  tests report `Inconclusive` there.
- **Headless drawing is possible on 1.x**: `Application.Init(new FakeDriver(), null)` renders with
  no terminal and `FakeDriver.Contents` holds every rune and attribute (`FakeMainLoop` is internal,
  hence the `null`). Proven and deliberately not adopted: the tmux tier asserts the same runes and
  colors on the real binary and survives the 2.x port, which `FakeDriver` tests would not. Reach for
  it only for a drawing bug the running app cannot expose.

---

## Findings worth remembering

**Git**

- `git fetch origin <b>:<b>`, which is how a branch that is not checked out is pulled, only
  fast-forwards, and is refused outright for the checked-out branch. Git has no porcelain that
  merges into a branch without a working folder — hence `Merge to` checks out, merges, commits and
  switches back.
- Tags have no remote-tracking namespace, so `--prune-tags` cannot tell "never pushed" from
  "deleted on the remote". `refs/gmdtags/origin/*` is gmd's own record of what the remote had, and
  a local tag is deleted only if it was there and is gone. `git log --all` includes the mirror,
  harmlessly. `--prune` does prune a namespace fetched by an explicit refspec, which the whole
  design rests on — and an explicit refspec *replaces* `remote.<name>.fetch` rather than adding to
  it, so the configured refspecs are read and passed along with the mirror, or nothing but tags
  is fetched. A canned test cannot catch either; `GitIntegrationTest` has one for each.
- `git add` and `commit -a` on an unmerged path *resolve* it with whatever is in the working tree.
  `rebase --apply` and `am` write no `MERGE_MSG`, and `MERGE_MSG` alone never meant "merging" — a
  stopped rebase and cherry-pick write it too. Detect the operation in the order git's own
  `wt_status_get_state()` does. Gmd's cherry-pick runs `--no-commit`, which writes no
  `CHERRY_PICK_HEAD`, so it correctly reports as a merge with nothing to continue or abort.
- `git diff --cached --check` exits non-zero for trailing whitespace as well; filter its lines for
  `leftover conflict marker` and ignore the exit code.
- `merge-file --diff3` groups conflicts differently from `git merge` — one shared line splits a
  conflict in two — so a recovered ancestor must be mapped by content, never by position.
- `checkout --theirs` fails on a path the other side deleted. Decide what a file can be resolved
  to from index stages 2 and 3, not from the porcelain kind; `rename/rename` produces `AU`, `UA`
  and `DD` at once. `checkout-index --temp` writes into the worktree root whatever the cwd, while
  `unpack-file` respects it. `:(literal)` works on `add` / `checkout` / `rm` but not on
  `checkout-index`.
- Git omits `into <branch>` from a merge message made on `main` / `master`, so the inference has
  less to work with on the trunk. An amend keeps the author date and rewrites only the committer
  date. `git log --all --date-order` has no tie-breaker, so fixture commits need distinct dates.
- `git blame` is fatal, not lenient, on a missing `blame.ignoreRevsFile`; the service retries once
  without it. A pathspec-filtered diff loses rename detection, so a per-file re-diff must fetch the
  whole commit or name both paths. `GIT_EDITOR` beats `-c core.editor`, and a dev shell that
  already sets `GIT_EDITOR=true` hides exactly that.
- Worktrees. A linked worktree's `.git` is a *file* (`gitdir: <main>/.git/worktrees/<name>`);
  HEAD, the index and a stopped operation live there, refs and objects in the common dir that its
  `commondir` file points at (a submodule has the same file and no `commondir`). Resolve it in one
  place (`GitDir`); joining `.git` blindly wrote the repo config into a file and FailFasted. `git
  branch -vv` prefixes a branch checked out in another worktree with `+` — a regex anchored on
  `\*?\s+` did not match the line, and the branch *vanished* rather than losing its marker — and
  prints the worktree path in parenthesis before the upstream, which has to be stepped over or the
  upstream is lost. `worktree list --porcelain -z` ends each attribute with NUL and each record
  with a second one, and prints lock reasons verbatim where the non-`z` form C-quotes them; `Cmd`
  joins lines with `\n` and trims, neither of which touches a NUL. `check-ignore` answers for a
  folder that does not exist only when the path has a trailing `/`, since a folder-only pattern
  (`x/`) cannot otherwise know it is asking about a folder. Git refuses to check out, delete or
  `fetch b:b` a branch held by any worktree, a prunable one included, until it is pruned. A plain
  `git status` run in another worktree rewrites that worktree's `index`, holding its `index.lock`
  on the way, which a `git add` or `commit` run there at that moment fails on; `--no-optional-locks`
  reads without either, and is what gmd's reads of the other worktrees use. So the `index` is not a
  signal of a change in a worktree, and a plain edit does not touch it.

**Inference**

- Match a merge-subject name against a remote branch's nice name, but never against a deleted
  branch recovered from a subject (`<name>:<sid>`): tried on this repo's own history it moved 104
  commits off `dev`. A merge commit's first parent is not evidence either — wrong for any branch
  whose first commit is a merge, and it moved the same 104 commits. The root branch fallback is the
  oldest bottom commit, not the most commits (an orphan `gh-pages` often has more).
- Characterization tests record behavior, not intent. The pull-all test pinned the diverged-branch
  bug with a comment rationalizing it. The bar for the pipeline is a before/after dump over a real
  repo, not a green suite.
- A before/after dump through the test wiring cannot see a wiring bug: `RepoBuilder` shares one
  `BranchNameService` between the stages, so the split that lost every deleted branch's name was
  verified as pure movement and still landed broken. Anything stateful the stages share must be
  `[SingleInstance]`, and the pipeline keeps one test that resolves from the container.

**Terminal.Gui 1.x and the UI**

- Blocking the main loop (`.Result`, `.Wait()`) deadlocks: the `SynchronizationContext` posts every
  continuation to the loop being blocked. Load before the view opens and pass the data in.
- `SetFocus()` does not move the keyboard, and `ContentView.ProcessHotKey` returns early without
  focus, so a pane that looks focused receives nothing. Forward keys by hand from the view that has
  them, as `FilterDlg` and `BlameView` do.
- Keys are matched by exact value and nothing folds case (`p` / `P`, `u` / `U` are different
  commands in the log view). The side views register their letters in both cases
  (`ContentView.RegisterLetterHandler`), since their menus write shortcuts in upper case.
- A key goes to every toplevel on the stack until one handles it, stopping only at a modal one, and
  a plain `Toplevel` is not modal. The diff, blame and conflict views were plain toplevels, so a key
  they did not use reached the log view below (`P` in a diff pushed every branch) or the commit
  dialog's text field. `UI.RunDialog` makes whatever it runs modal. A modal toplevel is not made
  `Application.Top`, so the log view goes on redrawing underneath, followed by a redraw of the view
  over it; that is by design and is what a `Dialog` has always done.
- `UI.EnableInput` captures and restores `RootKeyEvent`. If progress reaches zero while a dialog is
  open, the restore puts back "swallow everything" and input is dead for good — keep the dialog
  inside the command's `Do`.
- `Application.Begin` brings the subview holding the focus to the front of the toplevel
  (`EnsuresTopOnFront`), so a view added to `Application.Top` after the main view — the progress
  marquee — is behind it once any dialog has run. `Progress.Activated` fronts it again, and only once
  the marquee's own toplevel is current, since the activate hook fires for a dialog over a dialog
  too. Before that, a commit showed no progress where a push, which opens no dialog, did.
- `new Label(x, y, text)` fixes an absolute frame and ignores a later `Pos.AnchorEnd`; use the
  initializer form. `Text.ToLine(width)` repeats the first character,
  `Subtext(…, isFillRest: true)` pads. There is no `Key` for `+` / `-` / `=`; cast the ascii code.
  `Y = -1` on a dialog means "as high as possible", and a `Dialog` given no position is centered.
- `ContentScroll` never clamps a cursor the content shrank past, so a reload restores it. Calling
  `SetCurrentIndex` before `ScrollToShowIndex` applies the scroll delta to the cursor twice.
- The highlight and selection are drawn on the columns after the graph only, and a highlighted row
  colors its spaces where an ordinary one does not. Read color snapshots with that in mind.
- `Program.Main` resolves the DI graph before `Application.Init()`, so no constructor may touch
  the main loop. The container is a runtime dependency with no test: after touching registration,
  start the app, because `--version` returns before the UI half of the graph is built.
- The union result (C# 15, RC1 compiler): a pattern whose type is a type parameter cannot declare a
  variable (CS8780), so generic helpers match `Value` directly; a tuple cannot be bound by name either,
  so results carry small records; a pattern naming the `gmd.Server` twin of a `gmd.Git` type
  compiles and never matches; a pattern variable in an `if` condition is scoped to the enclosing
  block, so several guards in one method need distinct names; `not` applies to the union itself
  and every other pattern to its contents, which is what makes `is not T value` bind on the
  fall-through. A type named `Error` cannot be referred to inside a type that has a method named
  `Error`, hence `new Error(...)` rather than a factory on `Result`.
- `Cmd.Command` trims the whole output, so a final empty line disappears, and it waits for the
  child's pipes, which a forking helper such as `xclip` inherits — hence `CommandWithStdin`.
- `FileMonitor`'s debounce is a sliding window: a folder written to continuously never raises.
- Coloring inside a text input: `TextView.SetNormalColor(List<Rune> line, int idx)` is called per
  rune per redraw with the live line object (no row index — cache by reference), but not for the
  caret or a selection, and `ContentsChanged`, not `TextChanged`, is what fires as the user types.
  `TextField` has no such hook: overdraw after `base.Redraw`, which is synchronous, from
  `ScrollOffset`, then `PositionCursor()`. `Attribute` is foreground and background only, so a
  misspelling is red rather than underlined. `Menu.Show` takes screen coordinates and
  `ViewToScreen` is internal; `ScreenToView(0, 0)` negated is a view's screen origin. The 2.x port
  has its own `IAutocomplete` and text-run attributes, which is where `UITextView`/`UITextField`'s
  spell coloring goes then.
- A `TextView` or `TextField` has a context menu of its own (Select All, Copy, Paste, Undo …),
  opened from inside `MouseEvent` on `Button3Clicked` and from `ProcessKey` on Shift+F10, so replacing
  it means catching both before `base` and swallowing `Button3Pressed`/`Released` too. The edit
  actions behind it are not public: an item runs one by its bound key (`GetKeyFromCommand` +
  `ProcessKey`), and the bindings differ between the two views (Alt+C copies in a `TextView`,
  Ctrl+C in a `TextField`). Ctrl+G is `DeleteAll` in a `TextView`, which is why the context menu
  leaves that one out. `TextContextMenu` is where this lives.
- A mouse event goes to the last-added subview whose frame holds the point, and nowhere else: a
  `BorderView` added over a text view or a list (it covers the whole rectangle, drawing only its
  edges) took every click, so the commit message body and the bordered lists never saw the mouse.
  `UIDialog.AddBorderView(view, …)` now inserts the border under the view it frames.
- Only a view that is flagged gets redrawn, so a label drawn from another view's state (the spell
  hint under the message body, from the inputs' red words) has to be flagged by that view: the
  inputs raise `Redrawn` at the end of `Redraw`, and the label, added later so it comes later in
  the subview order, is then drawn in the same pass. A label flagged before the view it follows
  would draw one redraw behind.
- WeCantSpell.Hunspell's suggestion time budgets (`QueryOptions.TimeLimit*`, a quarter of a second
  in all) are measured on the wall clock, where Hunspell's own are CPU time, and a budget that runs
  out returns the suggestions found so far rather than failing. So a loaded machine gets a shorter
  list, or none, and which one depends on timing: on CI with the end-to-end tests running eight at
  a time, 'Sumerize' got no suggestions and 'brnach' only 'breach'. `SpellChecker.SuggestOptions`
  gives it eight times the defaults.
- Terminal.Gui 1.17.1 pinned a core from launch on Linux and macOS: `UnixMainLoop` drained the
  wrong end of its wakeup pipe, so `poll()` reported readable forever. Fixed upstream in 1.18.0
  under an unrelated title; measured 100% → 0%. The one-second `FileMonitor` timer is not a spin.
- The .NET 10 SDK's terminal logger swallows VSTest output entirely, so `-tl:false` is passed
  everywhere. `Build.IsDevInstance()` is false for the built binary, which therefore really does
  call the GitHub releases API unless `CheckUpdates` is off.
- A raw string literal keeps the line endings of its source file, so a CRLF checkout on Windows
  turned every multi-line expected value in the tests into `\r\n` and failed 80 of them, the tag
  parser's included (`TrimSuffix("^{}")` no longer matched). `.gitattributes` now checks out LF on
  every platform and CSharpier writes LF, which a Debug build applies before compiling.
