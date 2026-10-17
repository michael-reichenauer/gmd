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
- CSharpier is the single formatter: on save, on build and in CI. `.editorconfig` holds
  only naming and non-layout rules. (A `.git-blame-ignore-revs` hid the bulk reformat from blame
  for a while; it was dropped as not worth keeping, since old lines are seldom blamed.)
- CI runs on every branch: a fast test job for feature branches and pull requests, the full
  multi-platform build and release for `main`/`dev`. `scripts/build` now fails when a publish fails, and
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
- The test process and every gmd it starts run under a throwaway `$HOME`, so `scripts/test` no longer
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
- `gmd/Server` reviewed (2026-10-13): `AugmentedService` (1,035 lines) is the reading only, and the
  writes that need the inferred model are services of their own beside `BranchWriteService`
  (`CommitRewriteService`, `UndoService`, `DeletedBranchService`), called by `Server` directly; every
  write the user asks for pauses the file monitor in one place (`Server.Paused`), where half did,
  by the path they took; git's records are converted in one place (`GitConverter`), where five
  held them, three conversions twice; a repo's lookups follow its lists in a copy too.
  `FileMonitor` moved out of `Augmented/Private`. `IServer` was left one facade, by choice: every
  member is used, and the command classes span the areas, `CommitCommands` 27 members over five.
- `gmd/Installation` reviewed (2026-10-13): `Updater` (one class, untested) is split into `Updater`
  (when to check and update, one update at a time), `GitHubReleases` (the release list, with its
  ETag), `ReleaseChoice` (the release and the binary for a platform, the platform handed in) and
  `BinaryInstaller` (download, check, swap, clean up), with `IHttp` as the seam the tests answer
  through (`FakeHttp`, and `LoopbackServer` for what HttpClient does with a slow or broken
  connection) and `RunningGmd` handing in the gmd to update, since the test process is a dotnet host.
  From no tests to 49. A real update of a running gmd was tried by hand on Linux arm64.
- A Homebrew tap (2026-10-14), `michael-reichenauer/homebrew-tap`, which the release workflow
  writes for every release from main (`scripts/brewformula`, a deploy key as `HOMEBREW_TAP_KEY`).
  A gmd installed from it is known by its binary's folder in the Cellar (`PackageManager`) and
  leaves updating to Homebrew: no install, no auto update, no previews, and ⇓ and `--update` say
  `brew update && brew upgrade gmd`. The path is the binary's, not a link's that started it. A
  folder the user cannot write to is said before the download. Tried with a real Homebrew on Linux
  arm64 from a local tap, and on macOS 27 from a local tap of a preview: installed, known as Homebrew's,
  and ⇓ says the brew command.
- Two more ways to install (2026-10-14): `install.ps1`, `irm … | iex` on Windows, to
  `%LOCALAPPDATA%\Programs\gmd` and the user's PATH, kept as written in the registry; and the dev
  container feature `gmd` (`devcontainer-feature/`), whose binary is in the container user's
  `~/.local/share/gmd` with a link on the PATH, so gmd updates itself there too. The feature is
  tested by CI in containers built with it, on a full and a bare image, x64 and arm64. Its first
  test failed: on a Linux host the devcontainer CLI changes the container user's id to the host
  user's as the container starts (vscode was 1000 as the feature ran, 1001 in the container), and
  gives only the home the new id, so a folder the feature gave the user elsewhere was no one's.
- winget prepared (2026-10-14): `PackageManager.Winget` knows a gmd in winget's
  `WinGet\Packages\MichaelReichenauer.gmd_<source>` and says `winget upgrade --id
  MichaelReichenauer.gmd`, once gmd is closed, since Windows cannot replace a program that runs.
  The release workflow proposes each release with Komac. A dry run of `komac new` made the
  manifests: a portable x64 installer, tags from the repo's topics, and the release notes from
  GitHub's, which showed a Co-Authored-By trailer in 1.7's change log, now left out. Then left out
  (2026-10-15): the submission of 1.8 was blocked by Defender in winget's validation sandbox, though
  Defender on a PC found nothing in the file, and getting past that for every release was judged not
  worth it (`.notes/USABILITY.md`, item 24). The detection stays; the workflow's Komac step is gone.
- `gmd/Cui` reviewed (2026-10-13), by five reviewers in parallel and every finding checked against
  the code before it was acted on: 32 commits, the bugs under Bugs fixed. Structure: the key-hint
  line is `Cui/Common`'s (`KeyHint`, `KeyHintBar`, `KeyHintLine`) and each view's hints are beside
  it (`DiffHints`, `BlameHints`, `ConflictHints`, `StartHints`), which ends the diff, blame,
  conflict and start views' dependency on `RepoView/` (the folders depended on each other); the
  Cui `DiffService` / `BlameService` are `DiffRowService` / `BlameRowService`; rules that the
  menus, the keys and the hints each had a copy of are one function each (`CommitRewrite.
  WhyNotUncommit` / `WhyNotSquash`, `BranchCommands.WhyNotRebaseAndPush`, `BranchPushPullCommands.
  CanPushBranch` / `CanPull`, `Selection.IsRows`, `RefName.IsValid`); and the index math that was
  in views went beside them (`MenuDimensions.ItemAt`, `DiffRows.AnchorAt` / `IndexOf`,
  `DiffRowService.CopyText`, `KeyGate`, `ContentView.ShowCurrentIndex`). Some 60 members nothing
  used are gone. What is left, the structure included, is under Open issues.

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
- The compiler is the .NET 11 RC2 SDK's since 2026-10-17, RC1 before (`global.json`; `LangVersion preview` in
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

**Refresh performance on large repos** (2026-09-26)

- Measured first, with an opt-in test (`RefreshTimingTest`) that times each stage of a refresh on a
  real repo, and only what was worth it changed, each change shown to change nothing: the git log
  parse (parsed as spans, the same fields for each of 210,000 commits), and the view repo once all branches
  are shown (lookups by name, and a sort to Sorter's very order without comparing every pair). The
  numbers and what was left alone are under Findings and Open issues.

**Features added on the way** (each documented in `gmd/doc/help.md`)

- Merge the current branch *into* another branch (`E`, and `Merge to` in the branch menu).
- A start page in place of the start menu (2026-10-13): what gmd shows outside a repository is a
  page in the place of the log, listing the recent repositories and those in the folder gmd was
  started in by name and `~/...` path, each opened by its number, with the actions under a line, on
  a letter each and explained to someone new. Init, clone and browse start in that folder, and the
  open menu (`o`) lists and numbers the repositories the same way.
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
- A way back, from the reflog (2026-10-06):
  - *Undo* of the last change of a branch, named after what the branch's own reflog says it was
    (`ReflogSteps`, `Repo.UndoSteps`): the current branch's at the top of the commit menu's Undo,
    any branch's in its branch menu. The current branch is reset so that nothing uncommitted is lost
    (`UndoStep.Mode`), any other moved with `update-ref` from where it was read. Undo again redoes
    it, and a squash is undone whole, both from a record gmd keeps (`RepoConfig.UndoSteps`).
  - *Recover Lost Commits*: the lines of work no ref reaches, which the reflogs still mention
    (`LostWorkFinder`), with the branch each was made on, what took it out of the history, a diff and
    a branch to bring it back.
  - *Restore Deleted Branch* (2026-10-07): git deletes a branch's reflog with the branch (and its
    upstream config), so the reflog cannot undo a delete; HEAD's reflog only has where the branch was
    the last time it was checked out. gmd records each side it deletes instead (`RepoConfig.
    DeletedBranches`, `DeletedBranchRecords`), and restores from the record: the branch at its tip,
    tracking what it tracked, and origin's pushed back with an empty lease, so a branch of the name
    pushed since is not overwritten. A record is the two sides of one pair, the local branch and the
    remote branch it tracked, and deletes are joined by that pair rather than by name, since two
    deletes of a name can be branches that have nothing to do with each other: joined by name, a
    restore pushed an old `origin/tmp` back with a new local `tmp`. A side is forgotten once it is
    restored, a branch of its name is back or its tip is gone (`rev-list --no-walk
    --ignore-missing`). The upstream goes back as config (`branch.<name>.remote` / `.merge`), not with
    `branch --set-upstream-to`, which refuses a remote branch with no remote-tracking ref, deleted too
    and not restored yet, or left out by a narrowed fetch refspec. Deletes outside gmd are not recorded.
  - A force push on origin told from new commits on both sides, by the remote branch's reflog and the
    fork point (`RemoteRewrites`); pulled by moving the branch's own commits onto the new version
    rather than merging the two; and origin put back from before it, with a lease. One that only
    dropped commits leaves the branch just ahead, which `merge-base --fork-point` cannot tell (the
    commit origin went back to is in the local branch too), so it is found by origin's old tip being
    in the local branch, and a plain push of it asks first.

- The commit details reworked (2026-10-15): the fields are four rows (`CommitDetailsRows`, with no
  view), so the message has most of the ten; parents and children are named with their branch where
  it is another, so a merge says what it merged; and the files the commit changed are listed below
  the message (`git show --raw --numstat`, `GetChangedFilesAsync`), read once the cursor has rested
  on it for 150 ms and kept for the last 200 commits, and those of the uncommitted changes from the
  status. Left open, see Product: long message lines are cut rather than wrapped, the committer is
  not shown, and the pane is ten rows whatever the terminal's height.

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
- `Continue Rebase`, and `scripts/test`, hung for anyone with `GIT_EDITOR` set. `Cmd.NeverOpenAnEditor`.
- A repo change within half a second of a read was taken as seen by it, so a `git fetch` in another
  terminal landing just after gmd read stayed unseen until something else changed; one made while a
  read ran, or while the search was up, was dropped outright. The log view now skips only a change
  told of before the read started (`ChangeEvent.IsSeenBy`), and one that comes during a read or a
  search is looked at again once the next repo is shown. Dating a change by the file's modification
  time instead was tried and lost renames: a moved file keeps its old time, so it looked seen.
  Showing or hiding a branch then still moved the repo time stamp up to when the view was made,
  after the read; it keeps the read's now (`ViewRepoConverter.ToViewRepo`), which mattered once the
  current branch came to be shown straight after a read (`CurrentBranchShown`).
- Pulling a diverged branch failed, with git's dozen lines of hints as the error, for anyone who
  has not set `pull.rebase`, which recent git refuses to guess. gmd asks once and saves the answer.
- Pull all stopped at the first diverged branch, leaving every branch after it unpulled; the branch
  menu's `Pull/Update` on the current branch ran a fetch git refuses outright.
- Squash refused unpushed commits and allowed pushed ones; Uncommit was offered with a dirty tree
  on an unpushed branch; push all tried to push diverged branches.
- Squashing pushed commits with commits not pushed yet on top squashed those in too: the commits to
  pick back were walked from the tip of the branch the squashed ones are on, which for pushed
  commits is the remote branch, below the local one. They are walked from the local branch's tip,
  and commits not on its line, e.g. ones only origin has when the two have diverged, are refused
  before anything changes, where the branch used to be reset onto them (2026-10-07).
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
- The update check missed a newer release, or kept offering the running one, depending on the time
  zone. The version is the build time, and a release is tagged with the version CI computes, in UTC,
  but every gmd computes its own where it runs, and read the build time as local time: the fix for
  `Build.Version()` throwing just after midnight (above) counted the minutes from local midnight
  instead, so east of UTC a gmd took itself for newer than a release built within the offset of it
  (0.91.1429.398 ran as 0.91.1429.518 in UTC+2, above the stable 0.91.1429.412), and west of UTC it
  took its own release for newer than itself. The times are read as UTC now and shown as local time.
  A binary installed before that keeps its skew until it has updated once.
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
- Inference, from the review of the rules (2026-09-26), each measured over the corpus below:
  - A branch point where a feature was merged back and kept went to the feature, with all of the
    branch it came from below it, whenever that branch had a commit of its own after the fork: the
    feature's tip counted as likely, named by the merge subject, and the plain commit did not.
    Decided now by which branch is senior (`TryHasSeniorBranch`).
  - `git checkout -b` at a published branch's tip, outside gmd, made that branch ambiguous from its
    tip to its fork, and so did gmd's own *create branch from branch*, which wrote its metadata under
    the full id while every lookup is by sid (the old entries are found now too). Since 2026-10-15
    every entry is written and read by the full id, and those under a sid are no longer read: a sid
    can be another commit's too.
  - `git merge origin/main` on main, a pull merge by hand whose subject git writes without `into
    main`, was not swapped, so others' pushed commits became a side branch named main.
  - GitHub's `Merge branch 'owner:branch' into x` was cut at the owner: the branch was named after the
    owner and the target lost.
  - A foxtrot merge (`Merge branch 'main' into feature`, then main fast-forwarded to it) drew main's
    line through the feature and main's own commits as a side branch named main.
  - A commit where branches of one name meet (a deleted branch recovered once per merge) asked
    whether it was on dev or on dev.
  - *Revert Commit* on a pull merge, whose parents gmd swaps, reverted what others had pushed rather
    than the local side the graph shows merged in; the details view listed the parents swapped.
- Inference, the second round (2026-09-26): the reflog is read and what it witnessed decides a
  commit's branch (`ReflogWitness`, `TryIsWitnessed`): where a commit was made, from a branch's
  reflog or from HEAD's through its checkouts, and which branch a branch was started from. A fact is
  only taken among the commit's candidates, never above main, never marks a commit likely, and is
  kept in the metadata when it decided between branches (keyed by full id and marked `~`, so older
  gmd versions never read it and it never makes up a branch; since 2026-10-15 in a map of its own,
  `WitnessedById`, as the choices are keyed by full id too and the two would replace each other, and
  `MetaData.Parse` moves the `~` entries there). The tail of the rule chain is one
  branch point decision (`TryDecideBranchPoint`): integration name › git-flow release name › one name
  for all › clearly most merged in; the likely-child rules are gone, and an undecided branch point is
  drawn on its most senior name. A repo can name its own integration branches in Config.
- The review of the second round before its merge to dev (2026-09-26):
  - A foxtrot merge was swapped on the first read only: the name service's parse cache, a single
    instance, outlived the read, and the swap had rewritten it. It is cleared as each read starts.
  - The reflog gave a feature fast-forwarded into dev (`git merge feature` on dev) dev's line from the
    feature's commit down, and drew dev as started from it. Where the branch a commit was made on was
    started from a branch that runs through the commit too, the commit is that branch's now, and a
    kept fact comes before the reflog's, since a branch's creation entry expires first.
  - Unnamed recovered branches, all called `branch`, counted as one at a branch point, which was
    then decided arbitrarily rather than left ambiguous.
  - Keeping the reflog's facts in the background could lose a choice the user made meanwhile, and
    resumed the file monitor under a running command. The metadata is changed under one lock
    (`UpdateMetaDataAsync`), and file monitor pauses are counted.
  - Only the local branches' and the HEADs' reflogs are read, not the remote branches'; `Created
    from HEAD` is paired with the checkout that made the branch, in one pass.
  - A local branch whose remote branch was left out of a truncated log kept it as its primary, and a
    merge into it failed the read of the repo.
  - A trunk's merge with no target (`Merge remote-tracking branch 'origin/master'`) is a pull merge
    only in a repo with one trunk name; with both it is as often one merged into the other.
  - Dumps before and after, of this repo, gitflow-avh, gmd's clone and Terminal.Gui: one change, a
    branch point of two unnamed branches in Terminal.Gui ambiguous again, as before the second round.
- A commit merged by id (`git merge <sha>`, subject `Merge commit '<sha>' into dev`) was recovered
  as a deleted branch named after the 40-character id. `commit` is not a branch keyword any more:
  the subject still says which branch the merge is on, but nothing about where the merged commit
  was. Verified on this repo's history: one branch renamed to `branch`, nothing else moved.
- Inference, the third round (2026-10-15), each change measured over the corpus, with git/git added
  to it (topic branches merged by name, maintenance lines, subprojects pulled in):
  - `Merge branch 'master' of https://github.com/j6t/gitk`, another project pulled in, reads as a
    pull merge and was swapped, and so was `Merge tag 'x' of <url>`: git's master line ran into
    gitk's history and the translators' branch, and 37 of the 6960 commits of master's first-parent
    line in the log were on master. A pull merge's two lines must meet in the log now, or it is
    another repository's branch, named after it (`gitk/master`); a merged tag is never one.
  - A tag merged in, or the word after "Merge" that a subject neither quotes nor puts after a
    keyword ("Merged conflicts", "Merge latest x into y"), is a weak name (`IsWeakName`): it names a
    merged commit only when no other merge has, and is no evidence of a branch.
  - A name a subject gives a commit, of a branch no candidate is, now recovers that deleted branch
    there (`TryIsDeletedBranchNamedInSubject`), ahead of the one branch rule: a topic started at the
    tip of another merged first ran down over the other's commits (git: 380 tips, 3088 commits),
    and a deleted maintenance line the topics were started from was ambiguous between them. See the
    findings for why it is held to recovered candidates and, for a commit's own name, branch points.
  - Branches of one name at a branch point match with an owner in front of either (1.2.x and a
    fork's timfel/1.2.x).
  - `InferenceDumpTest` scores ordinary commits too, by the merge that brought them in, lists the
    candidates of each ambiguous tip, marks swapped merges, and writes 8 character ids.
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
- A git command that wanted a passphrase, a password or whether to trust a host asked on gmd's own
  terminal: the fetch on opening drew the question over the bottom row and scrolled the screen a
  row, the keys typed for gmd went to it as the answer, and a push waited behind it for an Enter
  the raw terminal never sends (product review, `USABILITY.md`, 2026-10-07). Git now has nothing to
  ask on (`Cmd.NeverAskOnTheTerminal`), ssh asks gmd itself as its askpass (`Askpass`), which
  answers nothing, and the failure says what to do instead (`LoginError`).
- A crash ended gmd without a word, as if it had simply quit, and with exit code 0
  (`ExceptionHandling.Shutdown`, whose dialog was commented out). Once Terminal.Gui has given the
  terminal back, `Main` now says what failed, where the log is, that the next start begins it
  anew, and where to report it (`Program.CrashMessage`), and exits with 1; About has the project's
  links and the log's path. Tried with a throw on the main loop and in a background task.
- User branch orders that contradicted each other, each branch after the other as a rename could
  leave them, or a after b after c after a, made `Sorter.Sort` swap forever, and so gmd hang on
  every refresh of the repo. Both sorts now leave a place they cannot settle as it is after it has
  been passed once per item, which a comparer with no cycle never needs, and a rename keeps one
  order per pair of branches (`BranchOrder.OnePerPair`). (2026-10-07)
- A force push could overwrite commits on origin that no one here had seen. *Force Push* and
  *Rebase and Push onto* ran a bare `--force-with-lease`, whose lease is the remote-tracking branch,
  and the background fetch moves that: a fetch landing between the screen being drawn and the push
  let the push through over what it brought in. Both now lease on the remote tip the repo shown had
  (`PushForceAsync`, `--force-with-lease=refs/heads/<branch>:<sha>`), as Restore origin already did,
  and a refusal says origin changed since gmd showed it; the unused `PushRefForceAsync`, a plain
  `--force`, is gone. (2026-10-08)
- A `~/.gmdconfig` cut short, by a crash in the middle of a write or two gmd instances writing at
  once, stopped gmd on every start, with a stack trace, until it was found and deleted by hand
  (`FileStore`, `FailFast` on a file that was not json). A file that cannot be read is now set aside
  as `.gmdconfig.unreadable` and started anew, a file is written beside itself and renamed over, so
  it is never half written, and a write reads the file afresh, so two instances no longer drop each
  other's changes (the recent folders, a word added to the dictionary, the shown branches of a repo
  open twice). (2026-10-08)
- The user config held its defaults until the UI was ready (`Config.Init`, in `MainView.OnReady`) or
  something was set, so whatever read it earlier read the defaults, unnoticed: `gmd --update` checked
  with the default `CheckUpdates` and no cached ETag. `Config` now loads the stored values when it is
  created (`IConfigService.Load`, no more `Lazy` cycle between the two), and `CheckUpdates` turns off
  only the checks gmd makes by itself, so `--update` and the update dialog look for a release even
  so; the dialog used to say there was none. The reflective copy into the one `Config` instance
  stays, as every class holds that instance. (2026-10-13)
- Found by the review of `gmd/Utils` (2026-10-13):
  - A `~/gmd.log` that gmd cannot write, as one a `sudo gmd` leaves owned by root, hung gmd at start
    on a blank screen, for good: the log's static constructor asserted, the assert's handler waited
    for the log to close, and the writing thread waited for the static constructor. The file is now
    replaced when it cannot be emptied, and gmd logs nowhere rather than fail (`LogFile.Open`).
  - `TrimPrefix` / `TrimSuffix`, which trim branch names in the inference, compared with the user's
    culture, which ignores a soft hyphen or a zero width joiner, so trimming `origin/` off
    `orig\u00ADin/dev` left `/dev`. And the log wrote its times in the culture's calendar, the year
    1448 under ar-SA.
  - `Files.IsText` read characters, so UTF-16 with a byte order mark read as text, which git merges
    as binary: a conflicted UTF-16 file was refused with an error rather than resolved whole. It
    reads bytes now, as git does.
- Found by the review of `gmd/Server` (2026-10-13):
  - gmd could not open a repo in the middle of merging a commit no branch reaches, as a
    `git pull <url> <branch>` or a merge of FETCH_HEAD leaves on a conflict: the uncommitted commit
    looked the merge head up in the log, and gmd stopped on every start until the merge was finished
    outside it (`Uncommitted`).
  - A branch choice made here was undone by the next background fetch, every five minutes, until it
    was pushed: the metadata was fetched over the local value, and the merge took origin's for every
    key both had. That covers a branch set by hand, an ambiguity resolved again, and Rename Branch's
    rewrite of the old name, which brought the old name back as a branch. Origin's value is fetched
    into a record of its own, and merged three ways against the record from before the fetch.
  - Merge, Merge Into and Rebase onto chose between a branch and its remote branch by the author times
    of their tips, which a cherry pick, a rebase or another machine's clock gets wrong, and so merged
    the remote branch without the local commits. They go by git's ahead and behind counts now
    (`BranchWriteService.MergeTipName`).
- Found by the review of `gmd/Cui` (2026-10-13), each with a regression test unless said:
  - *Rebase and Push onto* pushed with force with no question, on an Enter in its sub menu, and its
    lease, origin's tip as shown, let it drop commits someone else pushed once a fetch brought them.
    It asks now, and refuses a branch origin has commits for that it has not
    (`BranchCommands.WhyNotRebaseAndPush`).
  - Saving the resolver with conflicts undecided staged the file, markers and all: git no longer
    listed it as conflicted, so the diff did not offer it, and the reopened resolver had lost the
    ancestor and the sides. A file is staged once nothing in it is undecided (`ConflictService.
    ResolveAsync`). A file resolved by hand in an editor could only be overwritten with a side; it can
    be marked resolved as it is.
  - Gmd stopped on: a click in an empty menu; `m` (and clicks) after the log shrank under the cursor;
    the key hints of a highlighted local branch once a fetch pruned origin's copy, with no key
    pressed; *Uncommit … and Newer* on the first commit; a range *Cherry Pick* of rows of two
    branches; Ctrl+C after a drag in the empty rows under a short log. A pool-thread race in the
    progress timer could end the process as well (not reproduced).
  - A click on a menu's title or bottom border ran the item next to it (the push menu's *Push All*,
    say), and the pointer picked an item scrolled out of view. Esc on a question answered Yes when
    a message box had been shown over it (`MessageDlg.Clicked`, a static).
  - The keyboard stayed dead after a menu opened by a click while a background read ran was closed
    once the read was done (`UI.EnableInput` put back what it found; `KeyGate` now).
  - Create Branch, Add Tag and Add Worktree took a name with a space, which git took as two
    arguments: `fix dev` made a branch `fix` at dev's tip and checked it out (`RefName.IsValid`).
  - Keys and menus that disagreed with their hints or with each other: the branch menu's Push of the
    current branch skipped the force push question `p` asks; Pull All was greyed out with any change
    while Shift-U pulled the other branches; `b` on the uncommitted row, `d` and `m` with rows
    selected and a branch highlighted, Shift-E on a branch held by another worktree, `d` on a branch
    with changes, `c` in the diff of a commit, and Squash, Uncommit and a diverged push offered where
    they then failed.
  - Amend could not reword the last commit while there were changes; the commit dialog named the
    newest commit's branch rather than the current one; a copy of an added file's lines kept the line
    number gutter; Scroll to a file left the cursor in the file before; `+` / `-` in a file history
    went back to the newest commit; Back in blame put the cursor off the screen; a click in the
    resolver landed two rows high; the diff and the resolver scrolled sideways into blank space; the
    log squeezed branch tips and tags into 6 columns, and dropped a branch rune from a graph cut to
    fit; Clone into a browsed folder was undone by the next key in the uri; `gmd -d -m` took `-m`
    for the folder; the large file question said 0 B. (The last three have no test.)
  - Cancelled create and rename dialogs refreshed and fetched; Continue and Skip refreshed twice at
    once on a new stop. The graph's column placement tested every pair twice, which was most of the
    time of a refresh with all branches shown (3.2 of git's 4 s, not measured again).
- Found by the review of `gmd/Installation` (2026-10-13), the updater:
  - An update over a link slower than about 8 Mbit/s always failed: the binary, some 100 MB, was read
    whole within HttpClient's 100 second timeout. It is streamed now, given up on only when nothing
    has come for a minute. A failed download was kept in a static field and handed back, faulted, to
    every later update of that version until gmd was restarted.
  - Nothing checked the new binary before it replaced gmd, and a file left at
    `/tmp/gmd.<version>.RTXZERT` was installed unseen as "already downloaded". A download cut short
    could leave a gmd that does not start, which no update can repair. The download is new each
    time, beside the binary, and has to match the release's `SHA256SUMS` and to say its version when
    run (`gmd --version`, as CI runs it) before the swap.
  - The swap renamed the running binary aside and the new one in, and nothing renamed it back when
    the second rename failed, leaving no gmd at all; a second gmd starting at that moment did that,
    by deleting the new one, whose name was the one the clean-up deletes. Linux and macOS replace it
    in one atomic rename now, and Windows renames the old one back.
  - With Auto update on, a gmd left open installed the same release again every hour and opened
    another "Restart for New Version" box each time, since the running gmd is still the old version.
  - A folder with a space in its path failed every update (`chmod +x <path>`, unquoted). An update
    with nothing newer made the dialog say gmd was updated, and quit it. A release with no binary
    for this platform was offered. A release list with no ETag was dropped for the cached one.

---

## Open issues

**Product**

- `USABILITY.md` holds the usability review's proposals not yet done: Tiers 2 to 4, and the
  small bugs found along the way; and the product review's (2026-10-07), A to D.
- The commit details pane: a message line longer than the pane is cut at its edge, where wrapping
  it would need the rows built for the width (`OnGetContent` is given it); the committer, when not
  the author (a rebase, a cherry pick, a commit GitHub made), is not shown, since the log reads no
  committer name (`%cn`), only its time, which `GitConverter` drops; and the pane is ten rows on any
  terminal, nearly half of a 24-row one. The files are read when shown, and the email and committer
  could be too, rather than with the log for every commit.
- Git's questions are asked in gmd's login dialog (product review step 17): ssh, and git, which asks
  `SSH_ASKPASS` when it has no `GIT_ASKPASS`, run gmd as the askpass, which asks the gmd that ran the
  git over a named pipe (`Askpass`, `AskpassServer`, `LoginDlg`). What is left:
  - What already works without asking is untouched: any credential helper, since a helper is not a
    prompt (Git Credential Manager included, which on Windows and macOS opens a window of its own or
    the browser); an ssh key in the ssh agent; and the user's own `GIT_ASKPASS`, `core.askPass` or
    `SSH_ASKPASS`, e.g. VS Code's, which asks in a window of VS Code when gmd runs in its terminal.
  - Still asked on the terminal: an OpenSSH older than 8.4, which ignores `SSH_ASKPASS_REQUIRE`.
    Starting git in a session of its own, with no controlling terminal (`setsid`), would cover it
    on Linux and macOS, but .NET's `Process` has no option for that.
  - Not tried on Windows: Git for Windows' ssh running `gmd.exe` as the askpass, and the named pipe,
    which .NET makes a Windows pipe there rather than a socket.
  - The metadata sync never asks (`MetaDataService`, `Askpass.NeverAsk`), so that a fetch or a push
    asks once rather than twice; with neither an agent nor a credential helper, the shared branch
    structure is then not synced at all.
  - Push All runs one push per branch, each a command of its own, so each asks, and a Cancel stops
    only the one.
  - Git Credential Manager without a window (Linux over ssh, or `credential.guiPrompt` false) asks on
    the terminal, e.g. its device code flow, and reads `GIT_TERMINAL_PROMPT` like git does, so it
    fails instead; the advice then is to log in once with git in a terminal, after which GCM has the
    token stored. Showing GCM's own prompts in gmd would need GCM to ask an askpass, which as far as
    its documentation says it does not.
- Signing is git's, as its config says (`commit.gpgsign`), for every commit gmd makes; Squash, the
  one made by `commit-tree`, which does not read that setting, passes `-S` itself (2026-10-15). Not
  tried, and left until someone reports it: a gpg key with a passphrase and a terminal pinentry
  (`pinentry-curses`, `pinentry-tty`), which asks on the terminal gpg-agent is told of (`GPG_TTY`),
  i.e. over gmd's screen as ssh did before the askpass. A graphical pinentry or a cached passphrase
  does not touch the terminal, and ssh signing (`gpg.format ssh`) is ssh-keygen, which presumably
  asks through `SSH_ASKPASS` as ssh does.
- An exception on a thread of its own, rather than on the UI main loop or in a task, ends the
  process in `AppDomain.UnhandledException` before `Main` gets to say so (see the crash fix under
  Bugs fixed). Nothing in gmd starts such a thread today.
- F5 does nothing inside the diff, blame and conflict views, where it only ever worked by falling
  through to the log view; `r` refreshes a diff. (`?` and F1 are registered there now.)
- Cmd+C cannot reach a terminal program at all on macOS: the terminal keeps it, the classic key
  protocol cannot express it, and Terminal.Gui 1.x has no Command modifier. An iTerm2 profile
  binding of Cmd+C to hex `0x03` is the workaround for the copy menu. The commit id and message
  need no Ctrl+C: `i` and `Shift-I` copy them, of the selected rows too, and the commit menu has
  the copy menu as *Copy* (2026-10-15).
- macOS Terminal sends Shift+↑/↓ as a plain `\E[A`/`\E[B` (`cat -v` shows `^[[A`), so they move
  rather than select there; gmd cannot tell them from ↑/↓. A profile key sending `\033[1;2A` /
  `\033[1;2B` fixes it, as `help.md` (Keys) says; iTerm2, VS Code and the Linux terminals send those
  already. gmd itself is fine: replayed into it through tmux, `\E[1;2B` selected with ncurses
  decoding it (`TERM=xterm-256color`, `kind`) and with Terminal.Gui doing so (`screen-256color`,
  no `kind`), so the old ncurses macOS ships is not a factor (2026-10-14).
- `UILabel.Text`'s setter sizes the label from the text it is *replacing*, so a header that grows is
  clipped. `BlameView` works around it with `Width = Dim.Fill()`; the setter is the fix.
- Pull all only considers shown branches; a hidden branch that is behind is neither pulled nor
  counted in `▼`. `help.md` says "all displayed branches", so arguably right.
- Undo, Recover and the force push handling, as left (2026-10-06):
  - No key for Undo, nor a key hint; it is in the commit menu and the branch menus.
  - "Undo the last change in any branch" would need the times of the reflog entries, which are not
    read (`%gd` with a date would give them, and would change the selector the inference reads).
  - Merge to is undone from the target's branch menu, not from where it was run; Pull All is an undo
    per branch. A range cherry pick is one undo since 2026-10-17 (git's own `cherry-pick` of them
    all, recorded), unless it stopped on a conflict, when Undo names the last commit picked.
  - `RepoConfig.UndoSteps` is pruned of the branches that are gone only when a record is written.
  - A lost line of work's time is its tip's commit time, since the reflog entries carry none.
  - With no remote reflog (expired after 90 days, `core.logAllRefUpdates` off, a fresh clone), a
    force push is not told from new commits on both sides, and a pull merges as before.
  - A branch that merged the rewritten version into the old one (a `git pull` that merges) is not
    told, though its push puts the old version back; and the commits of the old version the local
    branch never had, fetched but not pulled, are not in the log, so they are not counted dropped.
  - A branch rewritten here after it was pushed (`LocalRewrites`, 2026-10-17) is told only while
    origin's tip is a commit the branch was at: commits pushed on the old version since (someone
    else's) make it diverged again, where a force push would drop them and a pull merges the old
    version back. Telling those apart (the old version's commits by the branch's reflog, the new ones
    not) and moving them onto the rewrite is left. Nor is a branch only behind told, a reset back of
    pushed commits, which a pull brings back as before, nor one whose reflog expired.
  - Restoring discarded changes and dropped stashes is not done: the reflog records neither, but
    git prints a dropped stash's id, and `git stash create` before a discard would keep one.
  - Restore Deleted Branch tells whether a branch of a record's name is back from the augmented
    repo, which leaves out a branch whose tip is outside the `maxCommitCount` log window. In a repo
    that large, such a branch counts as absent: the record is listed, and git refuses the restore.
- A repo with no commits still offers Uncommit (git refuses the reset). Ctrl+O is documented as
  activating OK but is bound nowhere; dialogs are accepted with Tab then Enter. The merge-from menu
  lists only shown branches, so with only `main` shown it is an empty box (a click in it no longer
  stops gmd).
- Cosmetic and pinned by tests: a cut sid, author or time column carries no `┅` marker (the
  subject column does); a binary file is headed `Modified:`; `FileSize` never shows a fraction. (A
  staged added file used to be counted as modified, which stopped being cosmetic once discarding a
  file asked whether it was new; it is added now. A stash message was cut at its first `:`.)
- Worktrees, deliberately left out of v1: no unlock of a locked worktree (a second `--force`),
  no bulk clean-up, no auto-prune; the stash list is the repository's and so shows stashes made
  in other worktrees; other worktrees' folders are not watched, so their change counts are up to
  thirty seconds behind; a submodule's `.git` file now makes it a root of its own too, which is right
  but new; the worktree dialog needs about 80 columns.
- `FileStore` reads a file afresh only to write it (see Bugs fixed), and two instances writing at the
  same moment can still each read before the other writes, so one change is lost; there is no lock.
  Two writes of one field, e.g. the shown branches of a repo open twice, are last one wins, as they
  always were. A write that fails, e.g. on a full disk or a read-only home, still stops gmd.
- The clipboard on Windows (Win32, then `clip.exe`) and macOS (`pbcopy`) is not verified on
  hardware (the platform checklist in `docs/CONTRIBUTING.md` has it, with the rest that only Windows and
  macOS show). Linux with no display is covered end to end; the tool path was checked with a stand-in
  `xclip` that forks a child holding the pipes, as the real one does.
- *Done 2026-10-14:* Squash reset the branch and cherry-picked the newer commits back one by one,
  so a failure stopped it half done, with a `squash-backup-…` branch left behind; a merge among the
  newer commits always did, cherry-pick refusing a merge; and the newer commits were committed again
  as the user, losing their authors and dates. The squash is `git commit-tree` now, the files of the
  newest commit on the parent of the oldest, moving nothing, and a `rebase --rebase-merges` of the
  newer commits onto it, which cannot conflict, being made on the very files they were made on, and
  is the one move of the branch. No hook runs for the squashed commit, as for the commits a rebase
  replays, where `git commit` ran `commit-msg` for it (a Gerrit Change-Id is then not added).
- The rewrites that are rebases are recorded for Undo only when they finish in one go: one stopped
  on a conflict and continued is named by the reflog, i.e. Undo Rebase, which leaves an Amend's
  `amend!` commit, a second Undo.

- Left by the review of `gmd/Git` (2026-10-13):
  - A hunk is read by the prefix of its lines rather than by the counts in its header, so with
    `diff.suppressBlankEmpty` set an empty context line, written as an empty line, ends the hunk.
    Reading by the counts would also be what a combined diff (`diff --cc`) needs.
  - Branch and tag names go to git unquoted. Git refuses a space and a backslash in a ref name,
    but not a `"`, which would split the argument. The dialogs that ask for a name refuse both
    since the review of `gmd/Cui` (`RefName.IsValid`); a name git has from elsewhere is not checked.

- Left by the review of `gmd/Utils` (2026-10-13):
  - 78 more `StartsWith` / `EndsWith` / `IndexOf` with a string and no `StringComparison` outside
    Utils, 44 in `Server/Private` and 21 in `Git/Private`, culture-sensitive as `TrimPrefix` was.
    CA1310 / CA1307 as warnings would list them; the inference ones want an `InferenceDumpTest`
    before and after.
  - A failure before the UI runs, e.g. a `~/.gmdconfig` that cannot be written, ends gmd with the
    runtime's "Unhandled exception" and a stack trace rather than `Program.CrashMessage`, which only
    follows the main loop.
  - *Done 2026-10-14:* `Cmd` read output by lines (`BeginOutputReadLine`), which also broke at a lone
    `\r`, and dropped every `\r`, so a diff line with a lone `\r` was split there and its rest lost,
    and a change of line ends alone showed lines the same on both sides. It reads the bytes whole now:
    a `\r\n` is a `\n`, a lone `\r` is kept (stderr's are line breaks, git's progress), and a diff is
    read with its `\r\n` (`isKeepCr`), whose `\r` the diff view shows as `␍` where it is the change.
    `TextWidth.Drawn` draws a control character as its picture, `␍` for a lone `\r`.
  - *Done 2026-10-14:* the timers of `Progress` and `MessageDlg` were `System.Threading.Timer`s
    that pulsed a view and dereferenced `Application.MainLoop` on a thread pool thread, where an
    exception ended the process with the terminal left as Terminal.Gui set it. They are main loop
    timeouts now (`UI.AddTimeout`), which also need no `Wakeup`. `MessageDlg.ShowWhile` is only
    reached by an update, which a dev build never offers, so its marquee was not seen running.
  - A failure stops gmd with `Application.RequestStop()`, posted to the main loop
    (`UI.ShutdownFromAnyThread`), which ends the current toplevel: with a modal dialog open, maybe
    only the dialog. Not verified.

- Left by the review of `gmd/Server` (2026-10-13):
  - The inference keeps one read's state in a singleton (`BranchNameService`, whose parse cache
    three stages share), which is why all inference runs under a static lock
    (`BranchStructureService`). A context made per read and passed through the stages would remove
    both; high-risk code, so only with `InferenceDumpTest` before and after.
  - `Repo` is three things in one type, the augmented repo, the view and the filtered view, told apart
    by whether `ViewCommits` is filled, and with the view state in `Commit` and `Branch` (roadmap C)
    every Show and Hide copies every commit of the log, up to 30,000.
  - *Done 2026-10-14:* the metadata was pushed with `--force` over origin's, so a choice someone
    pushed since the last fetch here was lost. The push is leased on the record of origin's value
    (`--force-with-lease=<ref>:<recorded id>`, or on there being none), and refused, origin's is
    fetched and merged and the merge pushed, three goes at most.
  - A diverged branch pair is still merged or rebased onto by the younger tip (`MergeTipName`).
  - An empty repo's branch is shown as `main` whatever its unborn HEAD is named
    (`AugmentedService.EmptyGitRepo`).

- Left by the review of `gmd/Cui` (2026-10-13). The structure first:
  - *Done 2026-10-14:* the command classes and menus were rebuilt with every snapshot of the repo
    (every refresh, show or hide, and key typed in the search), eight command classes with their
    dialogs through Autofac, and a command held the repo of when it was made, which is why
    `CommandRunner`, `MergeToBranch` and `FindBranch` read `repoView.ViewRepo` again after a
    refresh, and `IViewRepo` doubled as a service locator (`repo.Cmds`). The command classes are made
    once now, with the view, and share a `CommandContext` (the repo shown now, the view, the server,
    the status line, `Do`, and the command classes, `BranchCommands`' four forwarded ones included);
    `Do(async repo => …)` hands a command the snapshot as it starts. `IViewRepo` is the snapshot
    only, and the menus, still made with each one, take the commands from the context.
  - *Done 2026-10-13:* `gmd/Cui` held 41 files in one namespace, and is grouped by feature now, a
    namespace per folder (`Graphs/`, `Commits/`, `Branches/`, `Tags/`, `Recovery/`, `Worktrees/`,
    `Login/`, Clone and Init in `Start/`, FilterDlg in `RepoView/`, RefNameValidation in `Common/`),
    the tests with them; see CLAUDE.md. The folders depend on each other one way only since: the
    shell's shared parts are in `App/`, `MainView` is alone in `Cui/`, and `CommitDlg`,
    `FindBranchDlg` and `ApplicationBar` no longer reach into `RepoView`.
  - `Cui/Common` is three things: the widget toolkit (ContentView, UIDialog and its views, Menu,
    ListDlg, MessageDlg, the browse dialogs, Text, Color), app services (UI, Progress, StatusLine,
    Confirm, BranchColorService, which needs the server's repo) and the spelling input helpers.
    Splitting it is for the Terminal.Gui 2.x port, which it would make smaller: 19 of its 36 files
    touch Terminal.Gui, 18 more outside it, nearly all of those through `Key`, `Dim` / `Pos` and
    `Application.RequestStop`.
  - *Done 2026-10-14:* `RepoView.cs` schedules refreshes, and a read that finished after a newer one
    showed the older repo again: a status read started before a refresh and done after it, two
    refreshes at once (a command's is not held back by the flag a change's is), or a show or hide by
    the user during a refresh, which the refresh, read with the branches from before, undid; and the
    first of two reads to finish cleared the flags the other still needed. `RepoReads` counts the
    reads running and every repo shown, and a read that something was shown since it started is
    overtaken: a refresh reads again (three reads at most, then the last is shown), a status read
    looks at its change again against the repo now shown. Not reproduced end to end: the reads of a
    small repo are too quick for a show to land in one; a stress run (commits from outside while
    showing and hiding) ended right, with no read overtaken.
  - *Done 2026-10-14:* keys and hints each decided what is possible, and drifted: most of what was
    fixed above. The keys whose meaning depends on the cursor (`e`, `⇧e`, `s`, `p`, `u`, `d`, `b`)
    are decided in `LogKeys`, which `RepoViewInput` and `KeyHints` both ask. Left: the menus decide
    apart from both, through the shared rules (`CanPushBranch`, `CommitRewrite`, …) where there are
    some.
  - *Done 2026-10-14:* `IDiffView` could not be told what its source supports: line history (`git
    log -L`, which takes no context) offered `+` / `-` and said the context changed; the diff of an
    uncommitted line from blame offered commit and discard, and a discard left the blame stale; a
    range ending in the uncommitted changes offered them too. It is given a `DiffSource` now, the
    reload and whether the diff has context and can be committed.
  - *Done 2026-10-14:* `Text` measured columns in UTF-16 units, so a CJK character in a subject moved
    the sid, author and time columns, a cut could split a surrogate pair, and an emoji was drawn as
    `~I`, since Terminal.Gui 1.x's curses driver cannot draw a character outside the basic plane.
    `TextWidth` measures in columns, with Terminal.Gui's own table, slices without splitting a
    character, and draws one outside the basic plane as `�`, one column. Left: strings measured in
    chars outside `Text` (`Max`, which is `gmd.Utils`', and the menu widths), so a menu item in CJK
    is cut short rather than drawn wider.
  - `BranchColorService.GetColor` reads the repo config (a `Directory.Exists`, a hash) for every
    branch on every redraw of the log, i.e. every key, from `RepoWriter.GetBranchTips`; the graph has
    the colors already. Not measured.
  - *Done 2026-10-14:* the smaller ones, each with a test: the diff's right side numbered 3 low
    after conflict markers; the resolver's result not scrollable, its tabs not expanded, and `b`
    moving its cursor to another conflict; `g` on main silent; Merge silent with nothing to merge;
    Add Tag saying the tag failed when its push did; a drag starting a cell late; the 100 ms timeouts
    of Commit and Amend from the menu, which a menu closing before its item runs made needless; Init
    proposing the home folder; the combo field's context menu and handlers; `MoveToEndOfContent`
    showing 3 rows; `UnicodeSetsDlg`. The commit diff and blame were opened outside the command's
    `Do`, and the other diffs inside it: outside is right, since a view run under `Do`'s progress
    has none of its own, no marquee and no keys held while git works, so they all are, but the diff
    of lost commits, which its list comes back after.
- Left by the review of `gmd/Installation` (2026-10-13):
  - The Windows swap (rename the running exe aside, the new one in, back on failure, each rename
    tried five times) runs in the tests on Linux only, where any file can be renamed. Windows allows
    renaming a running exe but not replacing it; that part is only known to work from the users who
    updated with the old code, which did the same renames. New there and untried: running the new
    binary to check it, named `<binary>.update_<random>.exe` for that, and renaming it right after
    it ran, which the retries are for. Try an updater change through two dev previews, with a real
    update on Linux, macOS and Windows, before it goes to `main`.
  - The update dialog shows "Updating ..." with no progress for the minutes a slow download takes;
    the download itself could report its progress. *Done 2026-10-14:* it can be cancelled, Cancel
    or Esc, asked first with Continue the default (`UpdateDlg.IsCancelConfirmed`), which stops the
    install (`BinaryInstaller`, a token through the download, the checksum and the run of the new
    binary) up to the moment the new binary is put in place, never during it, since on Windows that
    is two renames. Seen in a harness only: a dev build is never offered an update.
  - The release list is the first page of `/releases`, 30 by creation date, which holds the latest
    stable release only while fewer than about 25 previews are kept after it (five are).
  - On Windows the run of the new binary to check it cannot be given a home folder of its own
    (the user profile is not `HOME`); it writes only a missing config file, and there is one.

**Inference pipeline**

- Left open by the second round of the rules review (2026-09-26):
  - Stacked pull requests seen from a clone (a branch started from another branch's commit, both
    pushed, or the base since merged and deleted) are most of cli/cli's and Terminal.Gui's remaining
    ambiguity: two deleted branches, or one deleted and one live, meeting where nothing in the graph,
    the names or the subjects says which was first. Only a creation fact settles them, which the
    reflog gives the one who made them and the metadata keeps; for anyone else they stay white.
  - `branch: Created from HEAD` is followed to the branch HEAD was on only through the checkout that
    created the branch, i.e. for `checkout -b` and `switch -c`. (`git branch x` writes the branch's
    name, `Created from dev`.) `git branch x HEAD` makes no checkout, `git worktree add -b` starts the
    new worktree's HEAD reflog with an empty entry, and the reflog entries carry no time to line up
    with HEAD's (`%gd` with a date would), so which branch those came from is not known.
  - A repo's own integration branch names are per user (`RepoConfig`), not shared with the metadata.
    Shared would suit a team convention better, and needs a merge rule for two users' lists.
  - Not taken as evidence, after measuring: a live branch over a deleted one at a branch point (the
    base of a stacked pull request is the deleted one as often), a published branch over a local-only
    one (a local-only branch is the user's own, whose reflog says it), and a deleted branch recovered
    as main or master for an old trunk (it put Terminal.Gui's v1 main back on main, and split cli's
    imported ghcs/main into three branches). A name ending in 'release' is not a release branch: a
    'v1_release' is a trunk of its own, and 'prepare-release' a feature.
- Left open by the review before merging to dev (2026-09-26):
  - Every reflog fact that decided between branches is kept in the metadata for good, never pruned,
    so the metadata grows with the user's own commits, and opening a repo with a reflog now writes
    `refs/gmd-metadata-key-value/data` into it (on a read-only checkout, a failed write logged on
    every read). Keeping facts only in repos that already use the metadata, or pruning the ones for
    commits no branch still passes, is to decide.
  - `ReflogWitness.BranchOrNull` rejects a name with `@`, which is a valid branch name, while a
    detached checkout of `origin/dev` counts as being on dev and a checkout of a tag as a branch.
  - A fast-forward read for the first time after the fast-forwarded branch's creation entry expired
    still gives the commit to the branch it was made on. A more senior name running through it (dev
    over a feature) would be a fallback.
  - With both trunk names in a repo, a pull merge of the trunk by hand is not recognized any more.
  - `RemoteFirst` is re-implemented in `TryIsPublishedTipOfLocalBranches`; `BranchOrNull` and
    `IsMatchPullMergeOfTrunk` repeat the prefix and trunk lists of `TrimBranchName` and
    `WellKnownBranches` (without `trunk`); `WorkCommit.IsParentsSwapped`'s comment says a pull merge
    only.
- Left open by the third round (2026-10-15):
  - A tag still names a line nothing else names, which is right for git's translation rounds and
    wrong for gitflow-avh's master line after master was fast-forwarded to develop (`1.11.0`). That
    line is lost to develop anyway: only swapping the `Merge tag 'x' into develop` on it would keep
    master's commits on master, as a foxtrot merge's swap does.
  - A merge naming nothing (`Sync with Git 2.51.2`, `Merge commit '<sha>'`) still sets the merged
    commit's name to "", over a name another merge child gave it, when parsed after it.
  - A pull merge names its local side after the local branch, which may only have pointed at
    another branch's commit: in Terminal.Gui a branch made at v2_develop's tip and then pulled
    named a v2_develop pull request merge (5 commits moved there in this round).
  - A pull request built on a local branch of another name, with catch-up merges on it ("Merge
    branch 'v2_develop' into x"), is now drawn as branch x below the pull request's branch, where
    the commits are at a branch point. Right by the subjects; one pull request in two names.
  - Stacked pull requests seen from a clone stay most of cli's ambiguity (above). Which merge first
    brought the shared commit into the trunk would decide them when the base was merged first, and
    wrongly when the stack was merged top first; not measured, and the merged-in score cannot judge
    it, since it counts by that very merge.
  - The parser takes `Merged in x (pull request #n)`, Bitbucket's, as the name "in" (weak now), and
    a name stops at a `#`, which git allows (`issue#1243-x` is `issue`).
  - The graph of all branches grows with every branch recovered: git's 5,889 branches became 6,289,
    and drawing them all 3.2 s to 3.8 s (see Performance).
- `origin/` is hard-coded in the name matching and in `NiceName`, so a repo whose remote is named
  otherwise matches names less often.
- Adding the uncommitted commit sets its parent but does not add it to that parent's children,
  while removing it filters the child lists. Invisible today; worth knowing before relying on
  a commit's children.

**Performance** (measured 2026-09-26, see Findings)

- The graph with all branches shown is now nearly all of such a refresh: 4 s for git's 5,900
  branches, 9 s for kubernetes' 11,000. Of git's 4 s, `GraphCreater.SetBranchesXLocation` was 3.2 s
  (each branch tested for overlap against every branch, twice: since 2026-10-13 once, not measured
  again; testing only the branches placed before it is the next step, nearly equivalent), the sort by column 0.4 s (Sorter, whose
  order of equal columns is the drawing), `SetGraph` 0.3 s. Only after the user asks for all branches
  (asked to confirm above 20), but then every refresh pays it.
- Not measured, same shape as what was fixed: `ShowBranches.AllRecent` checks every branch against
  the shown names with a list `Contains`, thousands times thousands once all are shown.
- *Done 2026-10-15:* the commit limit, `AugmentedService.MaxCommitCount`, is 50,000 rather than
  30,000 (measured, see Findings), with `System.GC.ConserveMemory` 7 in `gmd.csproj` paying for the
  memory: after six refreshes of 50,000 commits of git gmd held 0.55 to 0.65 GB with it, 0.8 to
  0.9 GB without, and 30,000 without had held 0.6 to 0.7 GB. Its cost is the first refreshes after
  opening, up to a second longer while it compacts. A repo whose log is longer than is read and
  that has no commit-graph file gets one written in the background (`WriteCommitGraphIfMissing`),
  since without one `git log --date-order` reads the whole history whatever the limit: rust 1.7 s,
  Linux 8.2 s for 30,000 commits, 0.36 s and 1.2 s with one; git's own repository 540 ms, 240 ms.
- A refresh allocates about 9 KB per commit, six times the log's text: the output is read whole into
  one string, which the GC has to compact off the large object heap, and then split. Each commit
  keeps 2 to 3 KB between reads, nearly all its full message, which only the details pane and the
  search use. A streamed parse, or messages read when needed, would let the limit grow, and would
  spare the GC setting and its slower first refreshes.
- Bors' subjects, `Auto merge of #123 - user:branch, r=reviewer` and `Rollup merge of #123 - ...`,
  are read as a branch named 'of' (weak), so in rust every pull request is an 'of' branch and its
  subprojects' lines are long ambiguous stretches with hundreds of candidates per commit.

**The union result, at .NET 11 GA (November 2026)** — the steps are in `docs/UPGRADING.md`

- Set `LangVersion` to `15`. If the target framework moves to net11.0 (an STS release, where
  net10.0 is LTS), delete `gmd/Utils/UnionPolyfill.cs` and the explicit `LangVersion` too. When
  CSharpier parses C# 15, `union Result<T>(T, Error) { … }` is optional sugar; the struct already has
  what the keyword form would not (non-boxing access, the helpers in its body).
- The VS Code C# extension bundles its own Roslyn and may flag union patterns until it catches up;
  `dotnet build` is the truth. CI installs the SDK from `global.json` plus `10.0.x` for the runtime
  the net10.0 tests need; unverified until the first CI run on this branch.

**Refactoring roadmap** (from the 2026-09-11 review; A before B before C)

- A, correctness, each with a regression test: `FileStore` (*done 2026-10-08:* the lost update, the
  unsynchronized cache, a malformed file); 13 `async void` handlers
  (`DiffView` ×6, `Menu` ×4, `BlameView` ×3, and `RepoView.RefreshAndCommit` posts an async lambda;
  `MainView`'s are gone) whose exceptions bypass the result path (*done 2026-10-13:* the Cui
  `IDiffService` / `IBlameService` twins of `gmd.Git`'s are `*RowService`); the vulnerability grep in `scripts/build` never fails the build,
  `scripts/log.bat` hard-codes one user's home, `scripts/installtools` sets `safe.directory` to
  `/workspaces/gmd`, `scripts/updatepackages` expands an undefined `$projectFile`. (`run.bat`
  dropping its arguments was fixed when the scripts moved into `scripts/`, and `gmd_linux` and
  `gmd_osx` missing from `.gitignore` when the build moved its output into `artifacts/`, which is
  ignored.)
- B, build and CI: grow `Directory.Build.props` to the shared properties (`TargetFramework`,
  `Nullable`, `ImplicitUsings`, `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`) and add
  `Directory.Packages.props`, since warnings never fail a build today and the `warning`-severity
  rules in `.editorconfig` are IDE-only; `SourceRevisionId` is recomputed from `UtcNow` on every
  build; the workflow has no `concurrency` group, `timeout-minutes`, NuGet cache, `.trx` upload,
  coverage step or `NuGetAuditLevel` gate, and the fast tier never runs on Windows or
  macOS.
- C, structure: (*done 2026-10-14:* the `*Commands` classes take a `CommandContext` in place of the
  four fields they repeated, and `FakeRepoView.Context` makes them constructible in a test; about 25
  `Do` bodies still end in `Refresh(); return Result.Ok;`, which a `DoAndRefresh` could take); `Commit.IsInView` / `ViewIndex` / `More` and `Branch.X` / `IsIn` / `IsOut` are UI
  layout state inside the server model; static leftovers (`Build.IsWindows` assignable,
  `Menu.StatusLine`, set by MainView, the single
  `UI.onActivated` slot; `MessageDlg.Clicked` is gone); names (`Hoover` → `Hover`, `Creater` → `Creator` plus its cSpell entry,
  `brandName`, `SetBranchManuallyAsync` is void, `GetViewRepoAsync` is
  synchronous, `TipID` vs `TipId`, in `Git.Branch` and the augmented `WorkBranch` alike, and
  `Status.IsMerging`, which is any operation in progress); dead code (the
  commented blocks in `UnicodeSetsDlg`, `ExceptionHandling`, `Cmd`, `FileMonitor`,
  `WindowsClipboard`; `exampleRunes.cs` and `ConfigDlg`'s and `MainView`'s are gone); and `FakeGit` implements 12 of 76 members.
- D, the diff and the resolver (from the 2026-10-16 review of `mr/diff-look`, left for after it):
  - `DiffView.cs` (about 1580 lines) and `ConflictView.cs` (about 1190) hold logic that needs a
    driver to test. The diff's top row (`TopRowText`, `TitleAt`) as a `DiffTopRow` beside
    `DiffRows`; its layout (`IsOneColumnFor`, `ListWidth`, `FollowWidth`, `isListWanted`) as a
    `DiffLayout`; drawing a row (`SideText`, `Focused`, `WithCursor`, `WithFill`) as a
    `DiffRowWriter`, as `GraphWriter` is. In the resolver, the typing in the result pane
    (`EditCurrentHunk` to `EndTyping`), and the questions of a file with no text to merge
    (`AskWholeFile` and the four after it), which are the workspace's rather than the view's.
  - `DiffFileList` and `ConflictFileList` repeat `Separated`, `Highlighted` and `Cut`, and count
    the scrollbar column in their width alike: one writer of a list row beside a view.
  - `WhyNotDiscardLines` and `WhyNotLeaveOut` repeat their chain of reasons.
  - The list beside the diff never narrows it into one column: the list needs a terminal of 150 and
    takes a quarter of it at most, which leaves the diff 112 columns, above the 110 one column starts
    below. `DiffView.Show` makes the rows again with the list beside them anyway (2026-10-17), for the
    files it then leaves out of the diff's top, so its one-column check there is only a guard.
  - `ConflictWorkspace` calls `UI.InfoMessage` and `UI.ErrorMessage` itself, so its loop is tested
    end to end only; what it opens next (`ConflictFileList.After`) and what finishes
    (`FinishOf`) are unit tested. `RepoCommands.ResolveConflicts` is one more `async void`, run
    outside `Do` as the views are (see the note in `CommitCommands.ShowDiff`).
  - `DiffRows.MaxLength` counts rows that never scroll sideways, the headers and a history's commit
    rows, which widens the sideways scroll into blank columns.
  - Speed, as measured then (Release): a diff of 10,000 changed lines lays out in about 110 ms, a
    minified line of 200 KB in 26 ms, a conflict of 1,000 lines a side opens in 0.6 s and decides
    in a millisecond. `LineWords` chunking by text element costs about 20% over a char loop, an
    ASCII fast path if it ever matters. `SideChanges` looks only 50 lines past where the alignment
    puts a removed line, so more lines than that added above changed ones are shown added and
    removed rather than paired.
- Not recommended: splitting `IServer` by area (decided 2026-10-13: every member is used and the
  command classes span the areas, so each would take several interfaces and couple no less);
  removing the one-implementation interfaces (they are the DI and test-double seams); renaming `Cui`; AOT (the assembly scan blocks it, and nothing needs it); sharing the
  records `gmd/Git` and `gmd/Server/Repo.cs` both declare, 14 of them copies (decided 2026-10-13:
  `gmd.Git` is to stay a general git library, and the server's converters are its boundary, see
  CLAUDE.md under Layering).

**Deferred, with the reasoning so it is not redone**

- **The inference measured and published** (product review step 22, postponed 2026-10-08 to the
  task that improves the inference, so that it is measured before and after). The plan:
  `InferenceDumpTest` scores only the reflog, which a clone has none of, and merge subjects that
  name the branch merged into, which check merges alone. Add a score for the ordinary commits: a
  subject that names the branch merged in ('Merge branch 'x'', 'Merge pull request #n from a/x')
  says the commits that came in only through that merge are x's. It is partly circular, since the
  inference uses those subjects as evidence; independent truth is the commits of each pull request
  from the GitHub API, for a sample. Run it on `--filter=blob:none` clones of a repository per
  workflow (gitflow-avh, cli/cli and rails for pull requests merged with a merge commit, git/git
  for topic branches merged by name, Terminal.Gui, sinatra) and on a frozen copy of this repo for
  the reflog; sort the disagreements into wrong, naming (a branch renamed since) and ambiguous by
  design; and publish in the README the share of commits on the right branch and the share honestly
  ambiguous per workflow, with how it was measured, and that squash or rebase merged repositories
  have nothing to infer. Whether to publish is decided once the numbers are seen.

- **A second remote** (product review step 19, postponed 2026-10-08 to meet the need in use
  first). Why a user has more than one remote, most common first: a fork, `origin` being the
  user's own and `upstream` the original, pushed to never, only fetched to keep `main` up to date
  (the daily loop: fetch upstream, bring its `main` into one's own, push that to the fork); another
  contributor's fork, added for a while to try a pull request's branch; a move or a mirror of the
  repository on another host; a deploy remote, push only (`git push heroku main`); a vendor's
  repository, to merge its releases into a patched copy; and a clone on another machine, or an
  agent's own clone, to fetch its branches from. The fork loop is the one for gmd's audiences, and
  it needs no push to the second remote: fetch every remote, show another remote's branches as the
  remote copies of the local ones ('main is 5 behind upstream'), and merge `upstream/main` with the
  merges there are. What makes it large is the inference, which assumes `origin/` where it names
  branches and pairs them with their remote copies (`BranchNameService`, `WorkRepo`, `ReflogWitness`,
  `RemoteService.TrimRemotePrefix`, and `main` detection, which would see `upstream/main` as a third
  line), and is held to before/after dumps on real repositories. Fetch and push of a chosen remote,
  for the other cases, is a separate and smaller change. Today gmd fetches and pushes `origin` only.

- **Terminal.Gui 1.x → 2.x.** When, not if, and a spike (2026-10-17) found it feasible now: branch
  `spike/tgui2`, whose `.notes/tgui2-spike/` has the pictures and how to run it. For: v1 is frozen
  (last commit June 2025); 2.x is released (2.5.0 on 2026-09-11, 2.4.x stable before it), and though
  its minor releases still break the API, they announce it a release ahead with `[Obsolete]` (2.5
  broke three small things); true color lifts the five branch colors; it draws emoji, which 1.x's
  curses could not. Against: one branch that does not compile until finished — 43 files, ~12.1k
  lines, 11 custom views. The end-to-end tier names no Terminal.Gui type and is the port's
  acceptance suite. What the spike found, with the log and the diff ported over the real server
  and row writers:
  - *The core ports mechanically.* `ContentView` changed 187 of 520 lines, nearly all of them `Key`,
    a class with a `KeyCode` now, so a `switch` on keys became `if` chains and Shift a property
    (`Key.A.WithShift`); `Redraw` became `OnDrawingContent`, `ProcessHotKey` `OnKeyDown`, and
    `MouseEvent` `OnMouseEvent(Mouse)`, with flags renamed (`LeftButtonPressed`, `PositionReport`).
    `Text.Draw` changed 3 lines. The application is an instance (`IApplication`), kept in `UI`;
    `RootKeyEvent` became the application's `KeyDown` with `key.Handled`; a `Runnable` run with
    `app.Run` is modal as `UI.RunDialog` needs. `await` resumes on the main loop as in 1.x (a
    `MainLoopSyncContext` while `Run` runs), so the trap of blocking it is the same. Inside a `View`
    subclass `Text` is the view's string property, which hides gmd's `Text` (`Common.Text`). The
    declarations alone name `MouseEvent`, `Rect`, `KeyEvent`, `Toplevel`, NStack's `Rune`,
    `ColorScheme` and `MainLoop`, all gone, and 2.x's own `Menu` and `View.Border` clash with gmd's.
    The bulk of the work is past the spike: `UIDialog` and the text inputs with spell checking,
    `Menu`, and the keys of the log, diff, blame and resolver views.
  - *Keys and the mouse arrive*, in tmux: Shift letters, Ctrl-C, Alt and Alt-Shift, F-keys,
    Ctrl-arrows, Shift-Tab, the wheel. A resize no longer eats the next key (so the F12
    `TmuxSession.Resize` sends could go), and quitting gives the terminal back clean.
  - *The colors are gmd's own.* 2.x writes every color as 24-bit RGB, its named ones too, at W3C
    values (`Color.Blue` is 0,0,255), so gmd's look no longer follows the terminal's 16-color theme
    and needs a palette of its own; the spike's is Campbell, as the README's pictures are. Twelve
    branch colors (Material 300–400 hues) told apart the neighbours five did not; a hash still lets
    two neighbours collide, so a port should pick one unlike its parent's and neighbours'. A light
    terminal works: `Color.None` is the terminal's own background (`CSI 49m`), and 2.x asks the
    terminal for its colors at start (OSC 10/11, `IDriver.DefaultAttribute`), which can pick the
    light palette or the dark. With 16 colors forced (`Driver.Force16Colors`, set for `TERM=linux`,
    `TERM=dumb` and `NO_COLOR`) the RGB is mapped to the nearest of 16; drawn with 2.x's named
    colors that is exactly today's look, a setting for users who want their theme's colors.
  - *No 256-color output.* 2.x detects a 256-color terminal (`TERM=*-256color` and no `COLORTERM`)
    but writes 24-bit colors to it anyway (`OutputBase.AppendOrWriteAttribute` knows RGB and 16
    only), which a terminal without true color garbles (Terminal.app before macOS 26, as far as is
    known). The 16-color setting is the way out. Worth an issue upstream.
  - *Syntax colors work*: 2.x's `TextMateSyntaxHighlighter` (TextMateSharp, VS Code's grammars,
    picked by the file's extension) colors a line in `DiffRowService.LineText`, and the changed
    words of `ChangedSides` keep its colors on the change's background. Its roles are coarse
    (`public`, a `storage.modifier`, has none), so a port would call TextMateSharp itself with
    gmd's own scope colors. It costs ~0.14 ms a row, 280 ms for a diff of 2,000 rows against
    12–23 ms without, and gmd makes every row up front: color only the rows drawn, cached, with a
    highlighter per side of a hunk, so that a multi-line comment is colored (the spike resets it
    per line).
  - *Packaging.* The single-file linux-arm64 build grows from 112 to 126 MB (2.5 brings
    TextMateSharp's grammars, Markdig and Microsoft.Extensions.Configuration). The highlighter's
    native `libonigwrap` is put beside the binary, where the updater, which swaps one file, would
    leave it behind: `IncludeNativeLibrariesForSelfExtract=true` bundles it, extracted to
    `~/.net/gmd/` at the first start. Without it highlighting is silently off. It is built for
    linux, osx and win, x64 and arm64.
  - *Costs.* Start to a drawn log 340 ms against 270 ms (release builds in tmux, the spike drawing
    less than 1.x). Idle CPU about 3% of a core against 0.4%: 2.x polls, its input thread every
    20 ms (`InputImpl`, not configurable) and the main loop 25 times a second
    (`Application.MaximumIterationsPerSecond`, lowering which changed nothing), where 1.x waits.
    Worth an issue upstream too.
  - *Tests.* The end-to-end tier drives the spike as it is, but `ScreenText`'s color letters read
    the 16 SGR codes, in 24 tests: run them with 16 colors forced, or teach it the palette's RGB.
    `gmdTest`'s view tests (`ContentViewTest` and the like) are ported with the views.
  - *Untested*: real terminals (iTerm2, Terminal.app, Windows Terminal, Ghostty, all of which 2.x
    sends queries to at start), the Windows driver, dialogs, text input, paste.

  Order when it starts: `Color.cs` with the palettes and the choice between them, `UI.cs`,
  `ContentView` (the spike's three are a start), `UIDialog` and the text inputs, `Menu`, then the
  views; `MessageDlg` and `BorderView` are deleted rather than ported. Trap: the migration guide
  describes the development branch, not the released package; check the shipped assembly (its XML
  docs, or the source at the release tag) before believing any specific API. The source has
  `ai-v2-primer.md`, the 1.x habits to drop.

  **The port, branch `mr/tgui2` (from 2026-10-17)**, kept off dev until it works well (the user's
  call: dev goes on to main meanwhile). The whole suite passes on 2.5.0: 2106 unit and integration
  tests and the 272 end-to-end tests, which run gmd in the 16 colors. The look is kept: dialogs are
  gmd's own `DialogView` (frame, title, `UIButton`, `UICheckBox`), not 2.x's `Dialog`; `MessageDlg`
  and `BorderView` were kept, on it, rather than deleted as planned. Colors are names drawn in a
  `Palette` (dark, light, or the 16 of the terminal's theme), twelve branch colors in RGB. The traps
  2.5 had, each with what was done about it:
  - *`Invoke` on the main loop runs the action at once*; 1.x queued it. `UI.Post` queues (the
    menu's first move off a greyed out item recursed until the stack overflowed).
  - *Posted work runs as a timer, holding the timers' lock while it runs* (`TimedEvents.RunTimers`).
    A dialog opened after an await runs its main loop inside that lock, and every thread that posts
    meanwhile (an await's continuation, through 2.x's `MainLoopSyncContext` and `Invoke`) waits for
    the lock until the dialog closes: Ctrl-D in the commit dialog never showed its diff. gmd's own
    `MainLoopQueue` is the synchronization context, set and drained as each iteration starts
    (`IApplication.Iteration`). A bug worth reporting, with the two below.
  - *A view with no scheme of its own asks its superview for its colors* (`GetAttributeForRole`),
    never raising `GettingScheme`; `WithScheme` sets the scheme too.
  - *Every view is marked drawn once a draw is done*, a peer marked from another's draw too, and the
    last added is drawn first: a view changed from another's draw (the key-hint line from the log's,
    the spell hint from the input's) is changed in a post.
  - *A `TextField` selects its whole text as the keys move into it*, so the first key replaced it;
    `UITextField` takes the selection back. A `TextView` moves the caret without drawing, and a
    word's spell color depends on the caret, so the inputs draw anew when it moved.
  - *`KeyUp` is raised only for a release* (the kitty keyboard protocol), not as a key is done:
    `TextChanged` where it meant the text changed.
  - *A runnable's position is not clamped to the screen* (1.x put Y -1 on row 0): the search
    dialog is at X -2, Y 0. *Ctrl-A, -C, -X, -V, -Z and -Y* are the edit keys of both text inputs,
    where 1.x's TextView had Emacs keys. *An emoji is drawn as itself*, two columns.
  - *Idle CPU about 3% of a core* (the input thread polls every 20 ms): not fixable from gmd.
  - *`TextView` is obsolete in 2.5*, "superseded by tui-cs/Editor's EditorView", a package of its own.
    `UITextView` (the commit message, the resolver's typing) is built on it, with spell checking in its
    `OnDrawNormalColor`; a later 2.x may drop it, and then the port is to EditorView.

  Left to do before it goes to dev: try it in real terminals (iTerm2, Terminal.app, Windows
  Terminal, Ghostty) and on Windows and macOS, where 2.x's startup queries and its Windows driver are
  untried; the setting `Colors` in the Config dialog; help.md and the README where they describe
  colors; re-record the demo and the README's pictures (the user does, `scripts/demo`). Syntax colors
  in the diff (the spike's `SyntaxColors`) come after, and need the highlighter's native library
  bundled into the single file (`IncludeNativeLibrariesForSelfExtract`), which is left out of the
  release now, unused.
- **Theme, the parts that wait for the 2.x port** (USABILITY.md, Tier 4 item 7), the first two of
  which the spike above shows working:
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
- **Combined diffs (`diff --cc`)** are skipped with a warning. Relaxing the `@@ ` check alone is
  harmful: `ParseSectionDiff` calls a bare `int.Parse` on `-1,1 -1,1 +1,1` and throws outside the
  `Result` handling. Full support needs n+1 `@` hunk headers, two-column line prefixes and a three-sided
  view; worth it as a feature, since it is the only way to show what a merge resolved by hand.
- Not built, by choice: blame `-w` / `-M` / `-C` toggles; a diff context below 6; submodule
  conflicts; `rerere`; marking local-only tags in the log view (the tag mirror makes it knowable);
  tag pruning for remotes other than `origin`. Word-level highlighting inside a conflict, once on
  this list as one side against the other, is built as each side against the ancestor
  (`SideChanges`, 2026-10-16).
- Coverage: `coverlet.collector` is referenced but nothing reports it in CI. Report it from the
  pull-request job first, then consider a floor as a ratchet.
- IDE0305 (`.ToList()` → `[.. x]`, 13 sites) by hand, since it can change the concrete type behind
  an `IReadOnlyList<T>`. Target-typed `new()` is an open style question.
- `gmdSetup.exe` is a committed prebuilt binary; Intel macOS is unreleased (`install.sh` now says so
  rather than downloading a `gmd_osx` that does not exist); there is no `.runsettings`.
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
  times, in an E2e rerun and in a full rerun. Once more on 2026-10-13 (`TestPushATagFromTheList`, a
  menu walk one item short right after a push, i.e. a key dropped while the refresh ran), green
  alone three times. Recorded so nobody hunts a flake that is not biting.
- `AskpassServerTest.TestQuestionsAskedAtOnceAreEachAnswered` failed once in a full run on
  2026-10-15, while timing runs had the machine busy, and passed alone five times and in a full rerun.
- tmux cannot report the exit code of a directly exec'd binary; a crash shows as a `WaitFor`
  timeout with the screen and the log tail in the message.
- The end-to-end tests were about four of `scripts/test`'s four and a half minutes, nearly all waiting:
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

- The user's config changes the output of porcelain commands in a pipe too, so every command gmd
  parses pins its format (2026-10-13): `diff.noprefix` and `diff.mnemonicPrefix` drop or change the
  `a/` and `b/` (`--src-prefix=a/ --dst-prefix=b/`), `diff.external` runs another program for `git
  diff` (`--no-ext-diff`), `color.ui=always` colors a pipe (`--no-color`), `log.decorate` and
  `log.abbrevCommit` change the `commit <id>` line, and `log.showSignature` writes the check of a
  signed commit's signature on stdout before it, even in front of a `--pretty=format:` record and a
  reflog entry (`--no-show-signature`). `git status --porcelain` is the one meant to be immune.
- Git quotes a path that has a `"`, a `\`, a control character or, by default (`core.quotePath`),
  any character that is not ASCII, as a C string with the bytes of that character as octal
  escapes (`"\303\244"` for `ä`). `status` and `clean -n` quote a path with a space as well, a diff's
  `diff --git` line does not, which leaves it ambiguous when the two paths differ (a rename's
  `rename from` / `rename to` lines say them again), and `---` / `+++` end such a path with a tab.
  `diff --check` does not quote. `status`, `ls-tree` and `worktree list` take `-z`, which gives the
  paths as they are; `QuotedPath` unquotes the rest.
- A command line is split into arguments again by .NET on Linux and macOS and by the program itself
  on Windows, both by the same rule: backslashes count only before a `"`, where they are halved.
  `Cmd.Quote` writes a text as one argument; `git rev-parse --sq-quote <args>` shows what git got.
- A repository of SHA-256 ids (`git init --object-format=sha256`) has ids of 64 hex characters, and
  marks the lines blame finds uncommitted with 64 zeros; it cannot push to or fetch from one of SHA-1
  ids (`rev-parse --show-object-format` tells which). gmd reads one since 2026-10-13: an id is 40 or
  64 characters (`ObjectId`), and blame gives the uncommitted lines the 40 zeros gmd's own
  `UncommittedId` has. `TempRepo.CreateSha256Async` makes one.
- In C#, `\x` takes up to four hex digits, so `"\x1fb"` is U+01FB, not U+001F and `b`; write `\u001f`.

- The reflog, as Undo, Recover and the force push detection read it (2026-10-06):
  - git writes no entry to a branch's reflog for an update that leaves it where it was, so the
    `reset --hard` of Discard All Changes, `reset: moving to HEAD`, is in HEAD's reflog only (this
    repository's had 59 and its branches' none). Only `Branch: renamed`/`copied` repeat an id.
  - A rebase or a pull is one entry in the branch's reflog (`rebase (finish): …`), and one per
    commit in HEAD's, which is why Undo reads the branch's. Options given to a command are part of
    its message (`pull --rebase (finish): …`), so only the leading word is matched.
  - `GIT_REFLOG_ACTION` replaces the `commit` of a commit's entry, which would blind
    `ReflogWitness.IsCommitMade`; Squash records its moves instead. `update-ref -m` writes the
    message given, which is how an undo of a branch not checked out says `undo: moving to …`.
  - Deleting a branch deletes its reflog; HEAD's still says what was made on it.
  - `log --ignore-missing <ids> --not --all` lists exactly the commits nothing reaches and skips ids
    gc has removed, given on the command line too, not only with `--stdin`. `ICmd.CommandWithStdin`
    cannot run git (no working folder, stdout never read), so the ids go in chunks of 400, about
    16K characters, under the 32K a Windows command line holds.
  - A remote branch's reflog says `fetch …: fast-forward` or `forced-update`, and `update by push`
    for a push from here, forced or not. It is read only for the branches with commits not pushed.
    `git reflog show a b` reads several refs at once, and a ref with no reflog is left out silently.
  - `merge-base --fork-point`, which `pull --rebase` uses, is the newest entry of the remote
    branch's reflog that the local branch has; `RemoteRewrites` finds it from the log in memory.
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
- `REBASE_HEAD` outlives the rebase (2026-10-16, git 2.55): it is left behind once a rebase that
  stopped on a conflict is continued (an abort removes it), so it names the commit being applied
  only while a rebase is in progress, and is read only then (`GetConflictSidesAsync`). A revert
  writes its other side into the markers as `parent of <id> (<subject>)`, a rebase and a cherry
  pick as `<id> (<subject>)`.

**Inference**

- Match a merge-subject name against a remote branch's nice name, but never against a deleted
  branch recovered from a subject (`<name>:<sid>`): tried on this repo's own history it moved 104
  commits off `dev`. A merge commit's first parent is not evidence either — wrong for any branch
  whose first commit is a merge, and it moved the same 104 commits. The root branch fallback is the
  oldest bottom commit, not the most commits (an orphan `gh-pages` often has more).
- Characterization tests record behavior, not intent. The pull-all test pinned the diverged-branch
  bug with a comment rationalizing it. The bar for the pipeline is a before/after dump over a real
  repo, not a green suite.
- The rules review (2026-09-26) measured every change over a corpus: a frozen copy of this repo
  (with its reflog) and `--filter=blob:none` clones of gitflow-avh (git-flow), sinatra (older
  history with local pull merges and merged-in sub-projects), Terminal.Gui (develop branches per
  major version, forks) and cli/cli (pull requests, 255 branches). `InferenceDumpTest` scores each
  dump against the reflog and against merge subjects that name the branch merged into. Before and
  after the round, ambiguous commits and merges on the branch their subject names:

  | repo | commits | ambiguous | subject agrees |
  |---|---|---|---|
  | gmd | 1948 | 0 → 0 | 319 of 319 → 319 of 319 (reflog 76 of 79 both) |
  | gitflow-avh | 1184 | 36 → 10 | 8 of 129 → 82 of 129 |
  | sinatra | 5316 | 445 → 277 | 37 of 127 → 36 of 127 |
  | Terminal.Gui | 10276 | 549 → 389 | 950 of 1093 → 994 of 1146 |
  | cli | 13336 | 597 → 471 | 592 of 628 → 609 of 646 |

  (More subjects name a target after the owner-name fix. The one sinatra merge lost is a naming
  artifact: its 'master' is a merged-in sub-project's, drawn under that sub-project's name.)
- Merge subjects and the graph cannot tell a feature merged back into dev from dev merged into the
  feature to bring it up to date: in both, one child of the branch point is merged into the other.
  Extending the merged-back rule to live branches fixes the first and breaks the second (pinned by
  `TestBranchPointOfABackMergeGoesToTheBranchMergedFrom`). What tells them apart is seniority, and
  counting it needs care: the trunk merged into a branch (`main`, `upstream/main`, a bare `origin`
  or `upstream`) is catching up and does not count, a pull request always does, even from a fork's
  own trunk, and one merged branch is never enough. Each of those was found by a corpus dump
  deciding wrongly, not by a test.
- The second round moved no commit in the corpus except where names say more than the graph:
  Terminal.Gui 389 → 383 ambiguous commits, its old `develop` history now under `v1_develop`, the
  live branch it was renamed to (which the subject score counts as disagreeing); cli 471 → 475, the
  commits only the likely-child rule decided. The reflog's effect cannot show in clones, which have
  none: on a copy of this repo with branches stacked by `checkout -b`, the commit the second started
  from went from ambiguous to the first, and three of this repo's commits are decided by the reflog,
  as before. Every candidate rule of this round was first measured, and three were dropped for what
  the dump showed (listed under Open issues).
- A rule that decides a commit early stops `TrySetBranch` from repairing it from below, since the
  repair only walks up through ambiguous commits. A new early rule can so take a line away from a
  better, later subject match; the dump shows it as decided commits moving, which is why the
  merged-back rule moved ahead of the seniority rule.
- A before/after dump through the test wiring cannot see a wiring bug: `RepoBuilder` shares one
  `BranchNameService` between the stages, so the split that lost every deleted branch's name was
  verified as pure movement and still landed broken. Anything stateful the stages share must be
  `[SingleInstance]`, and the pipeline keeps one test that resolves from the container.
- The third round (2026-10-15) added git/git to the corpus (a `--filter=blob:none` clone, 30,000 of
  its 85,000 commits read) and a merged-in score. Before and after the round, ambiguous commits,
  merges on the branch their subject names, and merged-in commits on the branch named:

  | repo | commits | ambiguous | subject agrees | merged in agrees |
  |---|---|---|---|---|
  | gmd | 2354 | 0 → 0 | 388 of 388 both | 2334 of 2339 both (reflog 431 of 431 both) |
  | gitflow-avh | 1184 | 10 → 0 | 82 → 83 of 129 | 511 of 911 both |
  | sinatra | 5316 | 277 → 124 | 36 → 55 of 127 | 2723 → 2729 of 2806 |
  | Terminal.Gui | 10276 | 401 → 149 | 988 → 1021 of 1146 | 6683 → 6775 of 8059 |
  | cli | 13336 | 475 → 433 | 609 → 614 of 646 | 10185 → 10332 of 10568 |
  | git | 30000 | 2966 → 229 | 981 of 1885 → 1663 of 1845 | 18040 → 20379 of 20778 |

  (git's merges of a tag from a url read as naming their target before, hence the 40 fewer. Its
  master line went from 37 to all 6960 of its first-parent commits in the log.)
- The merged-in score counts the commits on the first-parent line of a merge's merged parent, down
  to where it meets the history merged into. It is partly circular, and it overreaches where a
  branch is merged into another than it was started from: git-flow merges a release into master and
  only its tag into develop, so the release's line runs on into develop's commits not yet released
  (gitflow-avh's 400 disagreements). One commit merged twice claims the shorter line, a trunk's name
  claims the trunk however it is called now, and a tag's merge claims nothing.
- A subject's name for a commit that no candidate has is taken only among recovered candidates: a
  live branch is left alone, since a name from before it was renamed would cut it in two, as the
  renames of integration branches did to recovered lines (Terminal.Gui's develop, now v1_develop,
  which is why an integration branch's name at one is left alone too). Leaving out the live
  branches' names altogether lost git's old maintenance lines, which gmd recovers under live names
  as it does dev once per merge. A commit's own "into x" is taken only where branches meet: on a
  line of one candidate it is as often a branch fast-forwarded into another, which says nothing of
  where it was started, and the split gave a sinatra subproject's history to a review branch. Each
  of these was found by the corpus dump, not by a test.
- Six character ids collide in a log of 30,000 commits (38 in git's), which made walks over a dump
  jump between histories; the dump writes 8.

**Performance**

- The commit limit, measured 2026-10-15 with `RefreshTimingTest` (`GMD_PERF_COUNT`, Release, median
  of five warm runs, commit-graph present) on git, kubernetes, rust and Linux, after the inference was
  made linear in ambiguous stretches. A refresh is the log read and parsed, the augmentation and the
  default view; it costs 1.2 to 2.6 s per 100,000 commits, and each commit keeps 2 to 3 KB:

  | repo | 30,000 | 50,000 | 100,000 | 200,000 | kept at 100,000 |
  |---|---|---|---|---|---|
  | git (85,792) | 0.51 s | 0.78 s | 1.19 s (all) | | 203 MB |
  | kubernetes (162,128) | 0.56 s | 0.82 s | 1.48 s | 2.26 s (all) | 201 MB |
  | rust (348,912) | 0.65 s | 0.95 s | 1.90 s | 4.39 s | 241 MB |
  | Linux (1,485,203) | 1.53 s | 1.85 s | 2.63 s | 4.18 s | 299 MB |

  How far back 30,000 commits reach differs by years: git to 2018, kubernetes 2023, rust December
  2025, Linux June 2026 (four months); 50,000 to 2013, 2021, May 2025 and March 2026. In the app on
  git, the first screen took 0.9 s at 30,000 and 1.8 s with all 85,593 commits, a refresh after a
  commit 2.0 s and 2.9 s (about 1.5 s of it the file monitor), and gmd held 0.6 GB and 1.7 GB after
  three refreshes, 0.4 to 0.5 GB and 0.6 to 1.0 GB with `DOTNET_GCConserveMemory` 7 to 9. A refresh is mostly
  latency, not a stop: the log shown stays until the new one is read, though the 8 commands that
  await their refresh keep the keys stopped that long. A search with no match reads every message,
  10 to 70 ms. Without a commit-graph none of this holds, see Open issues.
- Measured with `RefreshTimingTest` (Release, median of five warm runs) over the inference corpus
  and two repos past the 30,000-commit cap, from partial clones: git (5,890 branches, nearly all
  recovered from merge subjects) and kubernetes (11,008). A refresh of the default view of 30,000
  commits is about 600 ms, and the augmentation is not the problem:

  | stage (ms) | git | kubernetes |
  |---|---|---|
  | `git log` alone, from the shell | 213 | 247 |
  | `git log` through `Cmd`, read in full | 330 | 330 |
  | parsing it | 112 → 55–70 | 60 → 42–54 |
  | augmentation | 140 | 115 |
  | view repo, default view | 9 | 13 |
  | view repo, all branches shown | 854 → 35 | 1,566 → 38 |
  | view repo, refresh while all are shown | 892 → 29 | 1,854 → 32 |
  | graph, all branches shown (not changed) | 3,980 | 9,570 |

  End to end on git (gmd.log's `Showed`), the default view went from about 650 to 600 ms, and a
  refresh with all branches shown from 6.0 to 4.7 s. The parse also allocates 270 MB less per refresh
  of git, and the view repo of all of kubernetes' branches 41 MB rather than 1.9 GB.
- Of the 330 ms `git log` takes through `Cmd`, `Cmd`'s own reading is at most 40 ms, on git's 24 MB
  log, and nothing measurable on kubernetes' 10 MB. The rest is the pipe, about 45 ms over writing to
  `/dev/null` in the shell too, and not for its size (a 1 MB pipe, `F_SETPIPE_SZ`, changed nothing),
  and .NET starting the process, about 30 ms. Reading the bytes in bulk rather than line by line saved
  about 20 ms on git and nothing on kubernetes: not worth changing what every git call goes through,
  which would have to keep all the line reader does (pinned in `CmdTest`). What is left is the size of
  the log itself: `%B`, the full messages, is most of git's 24 MB.
- The augmentation is linear, about 4.5 µs a commit, and no stage stands out (assigning branches
  85 ms of git's, parents and children 25 ms, the rest a few): nothing worth risking the most subtle
  code for.
- Sorter's cost was the number of comparisons, not their price: 60 million for 11,000 branches, so
  cheaper comparisons could not have fixed it. A branch can only go after its parent, its ancestors
  and the branches the user ordered it against, and the overload told those makes the old sort's
  swaps exactly. The swaps have to be exact, since the comparer is partial and the order it leaves
  unrelated branches in is the graph's columns. Both randomized equivalence tests fail when the
  candidates or the swap order are wrong.
- `RefreshTimingTest` runs every stage in one process, so a stage's time depends on the stages before
  it. The graph of all of Terminal.Gui's branches looked 60% slower after the view repo got faster,
  and was the same both ways with `DOTNET_TieredCompilation=0`: the long view stage before it had
  given the tiering compiler time to finish the graph code. Before believing a change in a stage that
  did not change, time it in isolation.
- A `--filter=tree:0 --no-checkout` clone is enough for the log and the harness, but `git status` in it
  fetches trees one by one. The end-to-end run used a `--filter=blob:none --sparse` clone, with its
  remote pointed nowhere so that gmd's background fetch could not move the corpus under the dumps.

**Terminal.Gui 1.x and the UI**

- A text view can be typed in beside `ContentView`s in a bare `Toplevel` (2026-10-16, the
  resolver's result pane): `SetFocus()` on a `UITextView` added to it gives it every key, since a
  `ContentView` acts on its registered keys in `ProcessHotKey` only while it has the focus. Esc
  reaches the text view's `KeyPress` before its `ProcessKey`, which is where to catch it; `Leave`
  is raised for Tab and for a click on a focusable view beside it, but not for its own right-click
  menu, which runs over it and gives the keys back. A view laid over a `ContentView` is drawn after
  it when added after it, and a `ContentView` clears its whole frame, so the rows under it are
  drawn blank but for the gutter.
- A resize of the terminal eats the next key, whatever the view (2026-10-16): after
  `tmux resize-window`, `w` in the diff and `d` in the log did nothing the first time and worked the
  second. So does a user's resizing of the window, presumably. Not gmd's: the key never reaches
  `ContentView`. `TmuxSession.Resize` sends an unbound F12 for it to eat.
- A view drawn from another's draw (a header following a scroll, say) lags by a key: what it marks
  for redraw while a draw is under way waits for the main loop's next event. `ContentView.TopRow`
  draws such a row in the same draw instead.
- A `ContentView` whose rows come from its callback learns how many there are only as it draws
  (2026-10-16): rows made anew, and the cursor put back on its line before the next draw, scroll
  within the old count, which left the cursor out of sight in the diff laid out in one column, with
  more rows than side by side. `SetTotalCount` first. A list beside a view is drawn at its width
  less the scrollbar column, which its width has to count.
- Terminal.Gui 1.x defines a `System.Rune` (NStack's), which makes .NET's `System.Text.Rune`
  ambiguous wherever both namespaces are in scope: it is named in full (`TextWidth`, `LineWords`).

- A dialog with keys of its own (`UIDialog`'s `CustomDialog`) used to override `ProcessHotKey`
  without calling the base, so its views got no hot keys at all, and `ContentView` takes its keys as
  hot keys: a list in such a dialog never ticked or moved, however it was focused. It found nothing
  until the commit dialog got its file list (2026-10-07); the dialog's keys go first now, then its
  views'.
- Blocking the main loop (`.Result`, `.Wait()`) deadlocks: the `SynchronizationContext` posts every
  continuation to the loop being blocked. Load before the view opens and pass the data in.
- No program may be started while `Application.Init()` runs. Curses sets the locale as it starts
  (`setlocale(LC_ALL, "")` in Terminal.Gui's `initscr`), and on macOS 27 a `fork` in another thread
  meanwhile deadlocks both: `sample` of a gmd hung at its start showed the main thread in
  `setlocale` → `__collate_load_tables` waiting in `_xzm_fork_lock_wait` for malloc, which the fork
  had locked, and the fork (`SystemNative_ForkAndExecProcess`) waiting in `libSystem_atfork_prepare`
  for a lock setlocale held. `git version`, started just before `Init` for the log, hung two starts
  in three, with nothing drawn and no CPU; it is started after `Init` now (2026-10-14). The window
  is the loading of the locale's tables, so a `C` locale, as the end-to-end tests have, never shows it.
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
- Keys are dropped while git works (`UI.StopInput`), and a dialog takes them while it is open. A
  dialog that put back, when it closed, the `RootKeyEvent` it found left the keyboard dead for good
  once progress had ended while it was open, which a menu opened by a click during a background read
  did; whether keys are dropped is decided afresh at every change now (`KeyGate`, 2026-10-13).
- `Application.Begin` brings the subview holding the focus to the front of the toplevel
  (`EnsuresTopOnFront`), so a view added to `Application.Top` after the main view — the progress
  marquee — is behind it once any dialog has run. `Progress.Activated` fronts it again, and only once
  the marquee's own toplevel is current, since the activate hook fires for a dialog over a dialog
  too. Before that, a commit showed no progress where a push, which opens no dialog, did.
- A view covered by a sibling is still drawn, and `ContentView.Redraw` starts with `Clear()`, so
  the empty log under the start page wiped it whenever it was marked for drawing. Hide the one
  underneath (`Visible = false`) rather than cover it. And a hidden view cannot take the focus:
  hand over by making the next one visible, focusing it, and only then hiding the other.
- `UI.StopInput` drops keys while git works, but not mouse clicks, so a second click on the start
  page could start reading a second repository; it has a guard of its own.
- `new Label(x, y, text)` fixes an absolute frame and ignores a later `Pos.AnchorEnd`; use the
  initializer form. `Text.ToLine(width)` repeats the first character,
  `Subtext(…, isFillRest: true)` pads. There is no `Key` for `+` / `-` / `=`; cast the ascii code.
  `Y = -1` on a dialog means "as high as possible", and a `Dialog` given no position is centered.
- `ContentScroll` never clamps a cursor the content shrank past, so a reload restores it; read the
  row through `IViewRepo.CurrentIndex`, which is clamped, never the view's (`m` stopped gmd on it).
  Calling `SetCurrentIndex` before `ScrollToShowIndex` applies the scroll delta to the cursor twice,
  hence `ContentView.ShowCurrentIndex`, which does both in the right order.
- The highlight and selection are drawn on the columns after the graph only, and a highlighted row
  colors its spaces where an ordinary one does not. Read color snapshots with that in mind.
- `Program.Main` resolves the DI graph before `Application.Init()`, so no constructor may touch
  the main loop. `UI.AddTimeout` there does not fail, it is silently never run (`Typing` relies on
  the no-op): the application bar's timer for ⇓ was, so a release found after startup went unshown;
  it starts on the view's `Initialized` now. The container is a runtime dependency with no test: after touching registration,
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
  everywhere.
- `Build.IsDevInstance()` is *true* for the Debug build's apphost, since .NET puts the entry
  assembly, `gmd.dll`, on `Environment.CommandLine` however it is started; only a published
  single-file build is not one. So the end-to-end tests can never show ⇓ or the release menu items,
  yet the start page and saving the Config dialog still call the GitHub releases API
  (`Updater.CheckUpdateAvailableAsync` skips only the `dotnet` host) unless `CheckUpdates` is off.
  It was long written down the other way round; the log of a run says `Dev instance, no update check`.
- A raw string literal keeps the line endings of its source file, so a CRLF checkout on Windows
  turned every multi-line expected value in the tests into `\r\n` and failed 80 of them, the tag
  parser's included (`TrimSuffix("^{}")` no longer matched). `.gitattributes` now checks out LF on
  every platform and CSharpier writes LF, which a Debug build applies before compiling.

**The updater**

- `HttpClient.Timeout` covers the whole body for `GetByteArrayAsync` and `GetStringAsync`, which read
  it at once, but only the headers with `HttpCompletionOption.ResponseHeadersRead`; reading the
  stream after that has no limit but the token given to each read. A fake handler cannot show the
  difference, since the stream it hands back is the test's own; `LoopbackServer` can.
- `Cmd` sets `GMD_ASKPASS=1` for every program it starts, so that the ssh of a git it runs can ask gmd
  for a passphrase. A gmd started through it takes itself for an askpass: run as `gmd --version`, it
  answers "gmd did not ask: --version", and with the UI up it would ask the running gmd to show a
  login dialog. The check of a new binary overrides the variable.
- `File.Move(source, target, overwrite: true)` is `rename(2)` on Linux and macOS: atomic, and a
  process running from the old file keeps it (`/proc/<pid>/exe` then says `(deleted)`). Windows lets
  the file of a running program be renamed, not replaced or deleted. Writing into a running binary
  fails on Linux ("text file busy") and ends a signed program on macOS.
- GitHub's ETag for the release list is weak, `W/"…"`; `ETag.Tag` drops the `W/`, and GitHub answers
  304 to either form. Every release since 0.97 has `SHA256SUMS`, and each asset also has a `digest`
  (`sha256:…`) in the API, which the cached `Releases` does not keep.
- The gmd versions out there update with their own code, so what they rely on outlives any change
  here: the asset names, the `v<a.b.c.d>` tags, the `<binary>.tmp_` files they leave for the next
  version to delete, `~/.gmdconfig`'s `Releases`, and the askpass channel.

**Homebrew** (tried on Linux arm64 and macOS 27, 2026-10-14)

- A formula's download that is a plain file, not an archive, is staged under its URL's name
  (`gmd_linux_arm64`), as a file downloaded is, 0644, so the formula renames it and sets its mode.
  Homebrew then left the binary byte for byte as released (the .NET single-file bundle intact), and
  its cleaner made it 0555, read only, in a folder the user can write to.
- Homebrew's own build sandbox on Linux lets the build write to `/tmp`, and refuses to run when
  Homebrew itself is under it ("Inherited sandbox permits writes to …/bin/brew"), so a Homebrew to
  try things with goes elsewhere: `git clone --depth 1 https://github.com/Homebrew/brew` into
  `~/.cache/…`, with its own `HOME` and `HOMEBREW_CACHE`, works in a custom prefix with a warning.
- `brew upgrade` looks for new versions of a tap once a day (`HOMEBREW_AUTO_UPDATE_SECS`), so a
  release of today needs `brew update` first. `brew untap` refuses while a formula of the tap is
  installed, and `--force` uninstalls it, so a local tap tried after the real one is installed again. `brew info --json=v2 --variations` evaluates the formula on every OS and
  CPU, so the macOS half is checked from Linux too.
- A formula, not a cask: Homebrew stopped supporting casks that fail Gatekeeper on 2026-09-01, and
  a formula's download is never quarantined, so neither notarization nor a Developer ID is needed.
  homebrew-core builds from source, which the C# 15 compiler of a .NET 11 preview rules out for now.
