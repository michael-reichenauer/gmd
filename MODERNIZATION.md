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

**Features added on the way** (each documented in `gmd/doc/help.md`)

- Merge the current branch *into* another branch (`E`, and `Merge to` in the branch menu).
- Blame a file: runs of one commit bracketed in the gutter, age heat, drill-down with `P`.
- Adjustable diff context per file (`+` / `-`: 6 → 15 → the whole file).
- Merge conflict resolution: detection of merge, rebase, cherry-pick, revert and am; abort,
  continue and skip from the repo menu; a two or three pane resolver with the common ancestor
  recovered on demand; hand-edited hunks; file-level choices for delete, add and binary conflicts;
  and commit gating that refuses unresolved files and leftover markers.

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
- `Continue Rebase`, and `./test`, hung for anyone with `GIT_EDITOR` set. `Cmd.NeverOpenAnEditor`.
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

---

## Open issues

**Product**

- **The diff and blame views register lower-case letters only.** An unhandled upper-case key falls
  through to the log view: `P` in either view is *push all branches*, `U` in the diff view is *pull
  all*. One line each, the way the resolver's `RegisterLetter` does it.
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
  subject column does); a binary file is headed `Modified:`; a staged added file is counted as
  modified (only the sum is ever shown, and a `--no-commit` merge stages its files, so every merge
  shows it); `FileSize` never shows a fraction; a stash message is cut at its first `:`.
- The clipboard on Windows (Win32, then `clip.exe`) and macOS (`pbcopy`) is not verified on
  hardware. Linux with no display is covered end to end; the tool path was checked with a stand-in
  `xclip` that forks a child holding the pipes, as the real one does.

**Inference pipeline**

- The circular-ancestor guard in `BranchHierarchyService.DetermineAncestors` is commented out, so
  `IsCircularAncestors` is never set and the three `ViewRepoCreater` filters on it are dead. A real
  cycle would loop forever, and `Sorter.Sort` also never terminates on a cyclic comparer. Find out
  what produced the cycle before restoring the guard or deleting both. No test: a cycle could not
  be produced through the public pipeline.
- Adding the uncommitted commit sets its parent but does not add it to that parent's children,
  while removing it filters the child lists. Invisible today; worth knowing before relying on
  a commit's children.

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
- **Inline conflict editing** (typing in the result pane with both sides in view). The modal `E`
  box covers the need. The gate is whether `SetFocus()` gives a `UITextView` the keyboard when it
  shares a bare `Toplevel` with `ContentView`s — a configuration nothing in the codebase has run,
  and a half-hour probe settles it. The cheaper change is to show both sides read-only inside the
  edit dialog (`UIDialog.AddContentView`, as `HelpDlg` does).
- **Combined diffs (`diff --cc`)** are skipped with a warning. Relaxing the `@@ ` check alone is
  harmful: `ParseSectionDiff` calls a bare `int.Parse` on `-1,1 -1,1 +1,1` and throws outside the
  `R` handling. Full support needs n+1 `@` hunk headers, two-column line prefixes and a three-sided
  view; worth it as a feature, since it is the only way to show what a merge resolved by hand.
- Not built, by choice: blame `-w` / `-M` / `-C` toggles; a diff context below 6; word-level
  highlighting inside a conflict; submodule conflicts; `rerere`; marking local-only tags in the log
  view (the tag mirror makes it knowable); tag pruning for remotes other than `origin`.
- Coverage: `coverlet.collector` is referenced but nothing reports it in CI. Report it from the
  pull-request job first, then consider a floor as a ratchet.
- IDE0305 (`.ToList()` → `[.. x]`, 13 sites) by hand, since it can change the concrete type behind
  an `IReadOnlyList<T>`. Target-typed `new()` is an open style question.
- `gmdSetup.exe` is a committed prebuilt binary; Intel macOS is unreleased though `install.sh`
  looks for `gmd_osx`; `MajorVersion` / `MinorVersion` are hand-edited; there is no `.runsettings`,
  and `LogServiceTest` mutates the default culture, so parallel tests would need care.
- Mouse interaction has no end-to-end test; it needs raw SGR sequences via `send-keys -H`.

**Test suite**

- Two end-to-end flake modes were seen and neither reproduced when chased: a `WaitFor` satisfied
  by text already on screen, so the key just sent is not actually waited for; and, in the
  devcontainer only, a blank pane with no `gmd.log` at all — the binary never started. Nine
  consecutive full runs were green afterwards. Recorded so nobody hunts a flake that is not biting.
- tmux cannot report the exit code of a directly exec'd binary; a crash shows as a `WaitFor`
  timeout with the screen and the log tail in the message.
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

**Inference**

- Match a merge-subject name against a remote branch's nice name, but never against a deleted
  branch recovered from a subject (`<name>:<sid>`): tried on this repo's own history it moved 104
  commits off `dev`. A merge commit's first parent is not evidence either — wrong for any branch
  whose first commit is a merge, and it moved the same 104 commits. The root branch fallback is the
  oldest bottom commit, not the most commits (an orphan `gh-pages` often has more).
- Characterization tests record behavior, not intent. The pull-all test pinned the diverged-branch
  bug with a comment rationalizing it. The bar for the pipeline is a before/after dump over a real
  repo, not a green suite.

**Terminal.Gui 1.x and the UI**

- Blocking the main loop (`.Result`, `.Wait()`) deadlocks: the `SynchronizationContext` posts every
  continuation to the loop being blocked. Load before the view opens and pass the data in.
- `SetFocus()` does not move the keyboard, and `ContentView.ProcessHotKey` returns early without
  focus, so a pane that looks focused receives nothing. Forward keys by hand from the view that has
  them, as `FilterDlg` and `BlameView` do.
- Keys are matched by exact value and nothing folds case (`p` / `P`, `u` / `U` are different
  commands). Every letter a view over the log view handles must be registered in both cases.
- `UI.EnableInput` captures and restores `RootKeyEvent`. If progress reaches zero while a dialog is
  open, the restore puts back "swallow everything" and input is dead for good — keep the dialog
  inside the command's `Do`.
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
- `R<bool>` is a trap: `R<T>` converts implicitly both to `bool` (`IsOk`) and from `T`, so for
  `T = bool` a `bool b = result` yields `IsOk`. Use an enum.
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
- Terminal.Gui 1.17.1 pinned a core from launch on Linux and macOS: `UnixMainLoop` drained the
  wrong end of its wakeup pipe, so `poll()` reported readable forever. Fixed upstream in 1.18.0
  under an unrelated title; measured 100% → 0%. The one-second `FileMonitor` timer is not a spin.
- The .NET 10 SDK's terminal logger swallows VSTest output entirely, so `-tl:false` is passed
  everywhere. `Build.IsDevInstance()` is false for the built binary, which therefore really does
  call the GitHub releases API unless `CheckUpdates` is off.
