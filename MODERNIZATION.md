# Modernization plan

A living checklist for modernizing gmd: raising the test coverage, making the code easier to
maintain, and updating the toolchain. Organized into steps small enough to review one at a time.

Mark items `[x]` as they land. Add new findings under the step they belong to rather than at the
end, so related work stays together.

---

## Step 0 — Toolchain and formatting ✅ done

- [x] Remove unused dependencies: the four `Microsoft.Extensions.Configuration.*` packages were
      referenced but never used anywhere in the code, plus the orphaned root `config.json`
      (added Nov 2022 with the state-handling commit, superseded by `FileStore`/`~/.gmdconfig`).
      Dependencies went 7 → 3.
- [x] Fix stale .NET 7 references: `build.bat` (`net7.0` made every `copy` path invalid, so the
      Windows build script could not produce binaries), `.vscode/launch.json`, and CI's
      `dotnet-version` (CI was building net8.0 with the 7.x SDK installed — it only worked
      because the runner image ships .NET 8).
- [x] `build.bat` rewritten to mirror `./build`: same steps, same artifact names. Note it now
      writes `gmd_osx_arm64` rather than mislabelling the arm64 binary as `gmd_osx`, and the
      trailing `pause` was dropped.
- [x] Release workflow modernized: `checkout@v3 → v7`, `setup-dotnet@v3 → v6`, and the archived
      `create-release@v1` plus six `upload-release-asset@v1` steps collapsed into one
      `softprops/action-gh-release@v3`. All six assets preserved, including the `gmd_linux`
      duplicate that `Updater.cs` falls back to.
- [x] CSharpier adopted as the single source of truth for formatting: on save, on build
      (`CSharpier.MsBuild`), on commit (pre-commit hook), and in CI (`csharpier check`).
      `printWidth` 120. See the CSharpier section in `CLAUDE.md` for the details and the
      Debug-formats / Release-checks split.
- [x] `.editorconfig` for naming and non-layout code style only, so it cannot fight CSharpier.
- [x] `.csharpierignore` for the Windows installer directory — CSharpier reformats XML, which
      invalidated the `packages.config.md5sum` Cake restore marker.
- [x] `.git-blame-ignore-revs` so the bulk reformat does not pollute `git blame`.
- [x] Fixed a `curl curl` typo in `installtools` that broke its gmd install step.

---

## Step 1 — Test foundation, first slice ✅ done

- [x] `FakeCmd` (`gmdTest/Utils/`), a test double for `ICmd` — the seam between the git services
      and the git executable. Lets git output be canned so parsing is testable with no subprocess.
- [x] `RepoBuilder` (`gmdTest/Fixtures/`), a fluent fixture DSL that builds a `GitRepo` from
      short commit names and runs the real augmentation pipeline via `AugmentAsync()`.
- [x] **Bug fixed: culture-dependent date parsing.** `GitLog.ParseRow` called `DateTime.Parse`
      with no `CultureInfo`. Under `ar-SA` (Umm al-Qura calendar) it threw `FormatException`,
      and because `ParseRow` is not wrapped in `Try` the exception escaped the `R`-based error
      handling entirely — gmd could not show a log at all on that locale. Under `th-TH`
      (Buddhist) and `fa-IR` (Persian) it silently parsed the year hundreds of years off.
      Fixed at `GitLog.cs` (2 sites) and `DiffService.cs` (1 site) with
      `CultureInfo.InvariantCulture`. Regression test covers 6 locales.
- [x] 14 characterization tests for the augmentation pipeline (`AugmenterTest`) and 16 parser
      tests for `LogService`. Suite went from 2 tests (one of which asserted `true`) to 31.
- [x] Removed dead test files: `UnitTest1.cs`, the empty `StateTest.cs`, and `GitRepoTest.cs`
      (100% commented out — Step 5 replaces it properly).

---

## Step 2 — Finish the augmentation characterization tests ✅ done

The safety net that has to exist before `BranchStructureService` (~950 lines) can be refactored.
Extended `AugmenterTest` using `RepoBuilder`, plus two new test classes for the subjects that grew
their own: `BranchStructureServiceTest` (inference, ambiguity, hierarchy) and `MetaDataTest`.
Suite went from 31 tests to 57.

- [x] Ambiguous commits and branches: `IsAmbiguous`, `IsAmbiguousTip`, `AmbiguousTip`,
      `AmbiguousBranches`, including how ambiguity spreads down from the ambiguous tip.
- [x] Resolve / unresolve / set-manually round trips. All three write the same metadata, so they
      are covered from both ends: `MetaDataTest` for what gets stored, `BranchStructureServiceTest`
      for what the pipeline then infers (resolve, unresolve, a name that is not a git branch, and
      the `branched` entry `CreateBranchAsync` writes). `RepoBuilder` gained `UnsetBranch`.
- [x] 20 more merge-subject forms in `BranchNameService.ParseSubject` — remote-tracking prefixes,
      pull merges vs merges from a remote, GitHub and Azure DevOps pull requests, and the forms the
      parser gets wrong (below).
- [x] Branch hierarchy: `DetermineAncestors` over a three level hierarchy, and the truncated branch
      being replaced by the root branch.
- [x] Root branch selection when none of `main`/`master`/`trunk` exist.
- [x] Divergent local/remote branches — the branch structure part. See Step 3 for `ahead`/`behind`.
- [x] Empty repo (`Repo.EmptyRepoCommitId`, via the new `RepoBuilder.EmptyRepo`) and single-commit
      repo.
- [x] Status and merging state via `RepoBuilder.WithStatus`. See Step 3 for `Repo.UncommittedId`.
- [x] `RepoBuilder` stayed readable, so no compact text form was needed. Revisit in Step 3, where
      the fixtures get bigger.

### Findings

Pinned by tests as current behavior, not fixed — each is a behavior change that deserves its own
commit.

- [x] **Fixed: `BranchNameService.ParseSubject` misread four real subject forms.** All four lost
      the `into` name, i.e. the branch of the merge commit itself, and two of them invented a
      branch name out of a keyword:
      - `.` was not in the name character class, so `Merge branch 'release/1.0' into develop` gave
        the name `release/1` and then failed to match the rest. Dots in branch names are common
        (release branches). The name pattern now allows dots but is bounded by a non-dot character
        at both ends, since a git ref can neither start nor end with one — so a sentence ending
        like `Merged from dev.` still does not take the period into the name.
      - GitLab quotes the target too (`into 'main'`); the `into` group now accepts the quotes.
      - `Merge tag 'v1.2.3' into main` and `Merge branches 'a', 'b' and 'c' into main` were read as
        branches named `tag` and `branches`. `tag` and `branches` are now keywords, and an octopus
        merge's remaining names are skipped so the `into` name is still found.

      Two related changes came with it: the structural groups in the regex are non-capturing and
      the keyword group is named, so `ParseSubject` no longer reaches for `Groups[3]` — a hardcoded
      positional index that any new group would have silently shifted. Verified against all 86
      distinct merge subjects in this repo's history: identical results before and after.
- [ ] **Deliberately left as is for now, revisit later.** The circular-ancestor guard in
      `DetermineAncestors` is commented out, so `WorkBranch.IsCircularAncestors` is never set —
      while `ViewRepoCreater` still filters on `Branch.IsCircularAncestors` in three places, i.e.
      that filtering is dead code today. Without the guard a genuine cycle makes the
      `while (ancestor != null)` loop run forever and grow `Ancestors` until it runs out of memory.
      The commented-out block was a quick workaround for a problem hit at the time, so the right
      fix is to find out what produced the cycle before deciding whether to restore the guard or
      delete both it and the flag. No test — a cycle could not be produced through the public
      pipeline.
- [x] **Fixed: with no `main`/`master`/`trunk` branch, the root/main branch was whichever branch git
      happened to list first.** A repo with only `dev` and an orphan `docs` branch could get `docs`
      as its main branch, and that is not cosmetic: the main branch is always forced into the log
      view, is always magenta, and cannot be recolored or deleted. `DetermineRootBranch` now falls
      back to the root branch whose history reaches furthest back, i.e. the oldest bottom commit,
      since the other root branches were started later in the life of the repo. Deliberately not
      the number of commits — an orphan `gh-pages` branch often has more commits than the trunk.
      The branch name breaks a tie, so the choice never depends on branch order.

      Also fixed on the way: the virtual `<truncated-branch>` was itself a candidate root branch,
      and its commit carries `DateTime(1,1,1)`, so any ranking by age would always have picked it.
      It is now excluded — being the scaffold that the block right below deletes, selecting it
      would have left every branch pointing at a removed parent.
- [x] **Fixed: a commit below a branch point was claimed by whichever child branch had a name
      parsed from a merge subject**, even when the other child was the branch it really belonged
      to. A `dev` commit ended up on `feature`, and worse, `dev` then looked branched out of the
      `feature` branch it had merged in — the hierarchy came out inverted.

      Root cause was one comparison in `DetermineAllCommitsBranches`: the `IsLikely` flag was set
      by `branch.Name == name`, where `name` is a nice name parsed from a merge subject (`dev`)
      while `branch.Name` is the primary branch name, i.e. the *remote* name (`origin/dev`) for any
      branch that has a remote. So a merge commit on a tracked branch was never marked likely, and
      only the other child was — which is why the wrong child won. The comparison now also accepts
      a remote branch's nice name.

      Deliberately narrow: it matches by nice name only for remote branches, *not* for a deleted
      branch recovered from a merge subject. Those are named `<nice name>:<sid>`, so matching them
      by nice name makes every merge into a recovered branch look like a confirmation of a name
      that was only ever a guess — which moved 104 commits off `dev` onto a deleted feature branch
      when tried against this repo's real history.

      The branch point itself stays ambiguous, and that is the honest answer: git records nothing
      about which branch the commit a branch was started from belongs to. gmd now marks it and
      offers the choice instead of silently guessing wrong, and the commit that ends up as the
      "most likely" one is now the branch that was merged into.

      Verified against this repo's real history (1747 commits, 73 branches): identical result
      before and after, still zero ambiguous commits.
- [ ] Considered and rejected: using a merge commit's first parent to decide the branch of the
      commit below a branch point. It looks like solid evidence — git's first parent is the branch
      that was merged into — but it is wrong whenever a branch is created and its first commit is a
      merge, since the first parent is then on the *parent* branch. Against this repo's real
      history it moved the same 104 commits onto `branches/fixmergeline`, a branch created from
      `dev` that immediately merged `dev` back in.

## Step 3 — Snapshot tests for the graph rendering ✅ done

`Text.ToString()` flattens the styled output to a plain string, so the drawn graph is testable
without a Terminal.Gui driver. These read as pictures of the graph, which makes them reviewable.
Suite went from 57 tests to 100, in three new test classes: `GraphTest` (the drawn graph),
`ViewRepoCreaterTest` (what ends up in view) and `ServerTest` (show/hide), plus
`BranchColorServiceTest`.

- [x] `FakeRepoConfig` (`gmdTest/Fixtures/`), an in-memory `IRepoConfig`, so `BranchColorService`
      and `ViewRepoCreater` can be constructed in tests.
- [x] Pipeline helper: `RepoBuilder.ViewRepoAsync()` runs `GitRepo → Augmenter → Converter →
      AugmentedService → ViewRepoCreater`, and `GraphText` (`gmdTest/Fixtures/`) runs
      `GraphCreater → GraphWriter → string`. `GraphText.WithSubjects` writes the commit subject
      after the graph so the expected value is a self describing picture, `GraphText.ColorsOf`
      writes one letter per rune telling its color.
- [x] Snapshot tests over the Step 2 fixtures: linear history, branch out and merge, several
      concurrent branches, merges from deleted branches, truncated log, uncommitted changes,
      diverged local/remote, ambiguous and user assigned commits.
- [x] Branch show/hide: `ShowBranch`/`HideBranch` as pictures in `GraphTest` (including the dark
      `╮`/`╯` markers a hidden branch leaves behind) and as branch lists in `ServerTest`, plus all
      four `ShowBranches` modes in `ViewRepoCreaterTest`.
- [x] Colors via `Text.Fragments`: `BranchColorServiceTest` pins the derived colors (main always
      magenta, local and remote sharing the color of their primary name, a child stepped one color
      when it collides with its parent, detached white) and `GraphTest` asserts the colors of the
      drawn runes.
- [x] Inline expected strings, no approval files. C# raw string literals keep the picture readable
      in the source and reviewable in the diff, and CSharpier re-indenting one does not change its
      value.
- [x] Moved here from Step 2, since neither is produced by the augmenter:
      - `ahead`/`behind` and `HasLocalOnly`/`HasRemoteOnly`, set by `ViewRepoCreater`. See the
        finding below.
      - The uncommitted commit (`Repo.UncommittedId`), added by `AugmentedService`
        `AdjustUncommitted` after `Converter`. `RepoBuilder` now builds the real `AugmentedService`
        with `FakeGit`, `FakeFileMonitor` and `FakeMetaDataService` (`gmdTest/Fixtures/`), so
        `ViewRepoAsync` returns the same repo the UI gets. `FakeGit` implements only the members
        the pipeline reaches and throws on the rest, so a test that starts depending on git fails
        loudly.

### Findings

- [x] **Fixed: `ViewRepoCreater.SetAheadBehind` lost `HasRemoteOnly` on the local branch of a
      diverged pair.** `SetBehindCommits` runs first (branches are sorted remote before local) and
      wrote `HasRemoteOnly` on both branches of the pair. `SetAheadCommits` then wrote
      `HasLocalOnly` using the `localBranch` it was passed — a copy taken by the `foreach` in
      `SetAheadBehind` *before* that write — so the flag went back to false. The remote branch kept
      both, since `SetAheadCommits` re-read it from the list.

      Not cosmetic: `BranchCommands.CanPush()` and `PushAllBranches()` both filter on
      `HasLocalOnly && !HasRemoteOnly`, i.e. "ahead but not behind, so it is safe to push". A
      diverged branch passed that filter, so 'Push all branches' tried to push it and git rejected
      it as non-fast-forward. `PushCurrentBranch` was unaffected, it looks at the remote branch.

      Both branches are now read back from the list at the point they are written, in both
      functions — the same staleness would have hit the remote branch had the branches been sorted
      the other way round. Covered by
      `ViewRepoCreaterTest.TestDivergedBranchesHaveLocalOnlyAndRemoteOnlyCommits`.
- [x] **Fixed: the `Φ` of a commit the user assigned to a branch was drawn in the branch color
      rather than white whenever that commit was also a branch out point.** `DrawBranch` set the
      sign white, but `DrawBranchFromParent` then called `SetGraphBranch` for the same cell and
      `SetBranch` overwrites `BranchColor` unconditionally. Since a commit is normally ambiguous
      *because* it is a branch out point, the white almost never survived.

      The five drawing functions that can land on a commit's cell each had their own
      `if (c.IsAmbiguous) color = Color.White;`, and the user-set case was simply missing from all
      of them. They now share one `GraphCreater.CommitColor(commit, branchColor)`: white when git
      does not record the commit's branch, i.e. when it is ambiguous *or* the user resolved it.
      That also makes the line a resolved commit branches out on white, exactly as an ambiguous
      one already was. Covered by `GraphTest.TestCommitAssignedByUser`, which now asserts the
      colors as well as the runes.
- [ ] **Found later, and it reopens what this step assumed: the views themselves are testable, with
      no terminal and without waiting for Terminal.Gui 2.x.** This step was built on `Text.ToString()`
      because "anything that draws needs a driver" was taken to mean drawing could not be reached.
      It can — Terminal.Gui ships a public `FakeDriver`, and `Application.Init(new FakeDriver(), null)`
      initializes headlessly. `FakeDriver.Contents` is then the cell grid the app drew, with the rune
      at `[row, col, 0]` and the attribute at `[row, col, 1]`, so the drawn output *and* its colors
      are both assertable. Verified against 1.19.0 with a standalone probe that rendered two labels
      and read them back out of the buffer; `FakeMainLoop` is `internal`, so the main-loop driver has
      to be passed as `null` rather than constructed.

      Worth a step of its own, because it changes the shape of several things already written down:
      the `GraphText` snapshots test `GraphWriter`'s output rather than what reaches the screen, so a
      drawing bug like the `Φ` one above is still only caught by reading the code; `ContentViewTest`
      stops one method short of `Redraw`; and the "UI is untestable" premise is part of the argument
      for the 2.x port in Step 9. It does not remove that argument — 2.x's `InputInjector` drives
      *input*, which `FakeDriver` alone does not — but it does mean the drawing half is available now.
      Start with one test that renders a real `RepoView` graph through `FakeDriver` and compares it to
      the matching `GraphText` snapshot; if those agree, the existing snapshots gain a lot of weight.

## Step 4 — Remaining git output parsers ✅ done

All via `FakeCmd`, with fixtures captured once from a throwaway repo driven through real git and
pasted into the tests as raw string literals. Suite went from 100 tests to 232, in eight new test
classes under `gmdTest/Git/` plus `MetaDataServiceTest`.

- [x] `BranchService.ParseBranches` — a 16-group regex with positional indices hardcoded in
      `ToBranch` (`Groups[1], [3], [4], [5], [8], [11], [14]`), so adding a group anywhere silently
      shifted everything after it. Covered: detached HEAD, `ahead`/`behind`/both, multi-digit
      counts, a `gone` upstream, no upstream, the `->` pointer line `IsNormalBranch` filters, and a
      name with `/` and `.`. Then refactored to named groups, verified identical against this
      repo's real branch output. See the finding below for what the tests caught on the way.
- [x] `StatusService.Parse` — porcelain output, the `" -> "` rename split (including quoted paths
      with spaces), all seven conflict kinds, merge state read from `.git/MERGE_MSG` and
      `.git/MERGE_HEAD`, and merging with no `MERGE_HEAD`. The temp folder with a bare `.git` in it
      is all `GetMergeStatus` needs, so no repository is created.
- [x] `DiffService` (496 lines, the largest git service) — the commit header including the `Merge:`
      line, hunk headers with and without counts, modified/binary/added/deleted/renamed files, tab
      expansion, the `\ No newline at end of file` marker, BOM stripping, conflict markers, a
      combined (`diff --cc`) diff, and the multi-commit output of `log --patch --follow`. Plus the
      staging dance in `GetUncommittedDiff`: it stages, diffs, resets, skips staging while merging,
      falls back to `diff --staged` in an empty repo, and still resets when the diff fails.
- [x] `TagService`, `StashService`, `RemoteService`, `CommitService`, `KeyValueService` — parsing
      where they parse, and the git command line where they only build one. `RemoteService` is
      entirely the latter, so its tests pin where the `origin/` prefix is trimmed.
- [x] `MetaDataService` — the push/pull of branch choices through git key/value storage, including
      that sync is off unless the user turns it on, that the remote value wins a conflict, that
      local-only choices survive a fetch, and that a removed choice stays removed. `FakeGit` grew
      an in-memory key/value store (`Values`/`RemoteValues`/`ValueCalls`) for it, so those four
      members no longer throw.

### Findings

- [x] **Fixed: while a rebase was stopped on a conflict, gmd lost the current branch.** Git names
      the current pseudo branch `(no branch, rebasing <branch>)` while a rebase is in progress, and
      the regex only knew the `(HEAD detached at <ref>)` form. The line was therefore read by the
      plain `(\S+)` name alternative, giving a branch named `(no` with the tip id `branch,`. No
      commit has that id, so `SetGitBranchTipsOnCommits` dropped the branch and `Augmenter` found
      no current commit — the `*` marker and the detached row were gone until the rebase finished.
      Not a rare state: gmd's own 'Rebase branch' stops there on any conflict.

      The parser now recognizes every pseudo name git writes — `(HEAD detached at|from <ref>)`,
      `(no branch, rebasing …)`, `(no branch, bisect started on …)` and the bare `(no branch)` — and
      reports them all as the `DETACHED` branch, which the rest of gmd already handles. The forms
      are matched explicitly rather than as any `(…)`, since a git ref name may contain parenthesis.
      `(HEAD detached from <ref>)`, which git writes after HEAD is moved off a branch, was broken
      the same way and is fixed with it.
- [ ] **Pinned as current behavior, not fixed: a staged added file is counted as modified.**
      `StatusService.Parse` trims each line before comparing prefixes, which removes the leading
      status column, so `A  file.txt` no longer matches the ` A ` case and falls through to the
      `else`. Only untracked files (`?? `) reach `AddedFiles`. Harmless today — every consumer
      either concatenates the file lists (`RepoExtensions`, `CommitMenu`, `CommitCommands`) or uses
      `Status.ChangesCount`, which is their sum — so fixing it would change nothing a user sees.
      Worth doing if the counts ever become visible.
- [x] **Removed: the half-written combined diff (`diff --cc`) branch in `DiffService`.** It
      recognized the file and marked it `DiffConflicts`, but not its `@@@ -1,1 -1,1 +1,1 @@@` hunk
      headers (`ParseSectionDiff` only accepts `@@ `), so the file came out with no content — which
      reads as "nothing changed here". It was never reachable: `--first-parent` and this branch were
      written in the same commit (b5b7b7b, Nov 2022), and no gmd git command has ever passed `--cc`
      or `-m`.

      Deleting it alone would have made things worse — `ParseFileDiffs` stopped at the first
      unparsable `diff --` header, so a combined diff would have truncated every file after it.
      That loop now skips an unknown header (and `Log.Warn`s it) instead of stopping, so the cost is
      one file rather than the rest of the diff. Covered by `DiffServiceTest.TestCombinedDiffIsSkipped`
      and `TestUnknownDiffFormatDoesNotStopTheRemainingFiles`.

      Left as a landmine warning for whoever revisits this: relaxing the `@@ ` check is not enough
      to support combined diffs, and on its own is actively harmful. `ParseSectionDiff` splits the
      header on `'+'` and calls a bare `int.Parse` on the pieces, so `-1,1 -1,1 +1,1` gives
      `int.Parse("1 -1")` → `FormatException`, thrown outside the `R` error handling entirely (the
      same shape as the Step 1 date bug). Full support needs three things: n+1 `@` hunk headers with
      n ranges, two-column line prefixes (`++`, `+ `, ` +`, `--`, `- `, ` -`, `  `) instead of one,
      and a three-sided story for the two-column diff view. Worth it as its own feature — showing
      what a merge actually resolved by hand is not available in gmd any other way.
- [ ] Noted, no action: two small parser quirks that are invisible in the UI and pinned by tests so
      they cannot change unnoticed.
      - `AsConflictLine` trims two characters instead of one, so a conflict marker line comes out as
        `<<<<<< HEAD`. Never drawn — `Cui/Diff/DiffService` replaces the marker with
        `=== Start of conflict`.
      - A hunk header without a count (`@@ -1 +0,0 @@`, git's shorthand for one line) is read as
        count 0 rather than 1. `SectionDiff.LeftCount`/`RightCount` are not used by the diff view,
        which counts the line diffs it actually got.
      - An annotated tag is listed twice by `show-ref --dereference`, once for the tag object and
        once for the commit, and `ParseTags` keeps both. `Augmenter.AddAugTags` drops the first,
        since no commit has the tag object's id.
      - `StashService.ToStash` splits the subject on `:` and takes only the third part, so a stash
        message containing a colon is cut short at it.

## Step 5 — A thin layer of real-git integration tests ✅ done

Few and deliberately small — a canary for git version and output-format drift, which the
`FakeCmd` tests cannot catch. Suite went from 233 tests to 249, in two new test classes:
`GitIntegrationTest` (13 round trips through `IGit`) and `AugmentedServiceIntegrationTest` (real
git output through the whole inference pipeline). All 16 run real git in about one second.

- [x] Temp-repo helper: `TempRepo` (`gmdTest/Fixtures/`) creates a throwaway repository in the
      system temp folder and drives it through the real `IGit` services, wired by hand over the
      real `Cmd`. It names the initial branch explicitly and sets locally the config that would
      otherwise be the developer's (user, signing, hooks, line endings), so the fixture is the
      same on every machine. It never touches anything outside its own temp folder: every git
      command runs with that folder as its working directory, and `Dispose` refuses to delete a
      path it did not create.
- [x] Round trips through `IGit`: `git version`, root path found from a sub folder, log (order,
      ids, parents, author, times, `--max-count`), branches (tips, current, detached HEAD), the
      ahead/behind counts of a tracking branch against a real (bare, local) `origin` remote,
      status (modified/added/deleted), merge (staged merge → merging status → commit → a two
      parent merge commit), a conflicting merge, a commit diff, and the uncommitted diff —
      including that its stage/diff/reset dance leaves the working folder as it was.
- [x] Beyond the plan, since it is the strongest canary of the lot: the whole pipeline over a real
      repo, i.e. real git output → `AugmentedService` → `ViewRepoCreater` → the drawn graph,
      asserted as a `GraphText` picture. Every other pipeline test builds the git facts by hand
      with `RepoBuilder`, so this is the one place where real git output reaches the augmenter.
- [x] `[TestCategory("Integration")]` on both classes, and `./test` now passes its arguments on to
      `dotnet test`, so `./test --filter "TestCategory!=Integration"` runs only the fast tests.
- [x] Deliberately not covered, since the `FakeCmd` tests already pin the parsing and each would
      cost another repo to set up: tags, stashes, the key/value metadata storage, rebase and
      cherry-pick.

### Findings

No bugs. Every parser held up against real git 2.55 output, which is the answer this step was
asking for. Two things worth writing down:

- [ ] Noted, no action: git omits `into <branch>` from a merge message when the current branch is
      `main` or `master`, so `Merge branch 'dev'` and `Merge branch 'dev' into feature` are both
      shapes gmd has to handle. Only the second records which branch the merge commit itself is
      on, which is why the inference has less to work with on the trunk than anywhere else. Both
      forms are now pinned end to end, from what git writes to what `BranchNameService.ParseSubject`
      recovers.
- [ ] Noted, no action: the staged-added-file-counted-as-modified quirk from Step 4 turns out to be
      what a user sees during every merge — `git merge --no-ff --no-commit` stages the merged in
      files, so gmd lists them as modified until the merge is committed. Still harmless, since the
      counts are only ever summed, and now pinned by `TestMergeRoundTrip` as well.

## Step 6 — Pure utility tests ✅ done

Cheap, quick, and they establish the habit. Suite went from 249 tests to 320, in seven new test
classes: `ResultTest`, `StringExtensionsTest`, `EnumerableExtensionsTest`, `SorterTest`,
`TimeDateExtensionsTest`, `GlobTest` and `BuildTest`. None of them touch git, disk or terminal.

- [x] `Result` / `R<T>`: the ok and error paths of all six `Try` overloads, error propagation
      through a call chain, wrapping an error without losing the inner messages, every implicit
      conversion (value, exception, `ErrorResult`, `bool`), `Or`, and both `ToString` forms. Plus
      the three fail-fasts, which is where the type stops being polite: reading the value before
      the error was checked, reading the value of an error, and returning `null` as a value. Each
      raises `Asserter.AssertOccurred`, so that is asserted too.
- [x] `StringExtensions`: `Sid`, `TrimPrefix`, `TrimSuffix`, `Max`, `ToJson`, `Txt` and
      `FileSize`, including that the trims only take one occurrence and only at their own end.
- [x] `Build`: the version encoding both ways — days since the base build time and minutes since
      midnight into a `Version`, and `GetBuildTime` back out of one — plus a version text that does
      not parse and one with fewer than four parts, which `Updater` can be handed. The
      CI-placeholder path is covered by `Build.Sha()`, whose literal CI replaces before the tests
      run, so the assertion holds on both sides of that `sed`. See the findings for what had to be
      fixed and made testable first.
- [x] `EnumerableExtensions`: the whole file, i.e. both `ForEach` forms, the four `Join` overloads,
      `TryAdd`/`TryAddAll`/`TryAddBy`, `ContainsBy`, `FindIndexBy`/`FindLastIndexBy`, `Add` and
      `DistinctBy`.
- [x] `Sorter`: ascending order, in place, empty and single item, that it is *not* stable, and the
      partial-order case it exists for — a comparer that orders a branch against its ancestors and
      says nothing about anything else, which `List.Sort` leaves untouched and `Sorter` gets right.
      That answers the `List.Sort does not work, why ????` comment in `ViewRepoCreater`.
- [x] `TimeDateExtensions`: the four formats, and that they are culture invariant. See the findings.
- [x] `Utils/GlobPatterns` — vendored, so the tests document what we rely on: wildcards, `**`,
      character sets and their inversion, literal sets, that both slashes are separators and
      nothing is case sensitive, and the two behaviors `FileMonitor` depends on — a single-segment
      pattern also matches the file name at any depth, and everything else is anchored at the
      start, which is why a `folder/` line from `.gitignore` is rewritten to `**/folder/**/*`.

### Findings

- [x] **Fixed: the ISO date formats followed the user's culture.** `Iso`, `IsoMs`, `IsoZone` and
      `IsoDate` formatted through an interpolated string, i.e. with `CultureInfo.CurrentCulture`,
      so a culture with its own calendar wrote the year in that calendar: 2023-02-07 came out as
      `1444-07-16` under `ar-SA` (Umm al-Qura), `2566-02-07` under `th-TH` (Buddhist) and
      `1401-11-18` under `fa-IR` (Persian). The same bug as the Step 1 one, at the formatting end
      instead of the parsing end.

      Two callers make it more than cosmetic. `Server.GetChangeLogAsync` writes `IsoDate()` into
      the generated `CHANGELOG.md`, so a maintainer on one of those locales would commit dates
      in another calendar. `ViewRepoCreater` matches the log view's filter text against
      `AuthorTime.IsoDate()`, so searching for `2023-02` found nothing. Fixed with
      `CultureInfo.InvariantCulture`; covered for six locales by `TestIsCultureInvariant`.
- [x] **Fixed: `Build.Version()` threw instead of returning a version.** Two ways to get a negative
      `Version` component, which `Version` rejects with `ArgumentOutOfRangeException`, thrown from
      the first line `Program.Main` logs — i.e. before anything is on screen:
      - `Build.Time()` returns `default` when neither the CI placeholder nor the assembly's
        `SourceRevisionId` parses, i.e. year 1, some 738,000 days *before* the base build time.
        Every other parse failure in `Build` falls back to `(0, 0)` or `DateTime.MinValue`; this
        path was simply unguarded. It is also what a test run gets, which is why `Build.Version()`
        had no test until now. It now reports the base time, i.e. version `x.y.0.0`.
      - The minutes were counted from midnight *UTC* of the build date while the build time itself
        is local — the base time text ends in `Z`, and `TryParseExact` reads that as UTC and
        converts to local. A build made between local midnight and the machine's UTC offset was
        therefore negative: 00:30 in a UTC+2 zone gave −90. Now counted from `cbt.Date`, i.e. the
        local midnight of the build day the comment always claimed. Nothing changes for a released
        version, since CI builds in UTC where the two are the same; a local build's fourth version
        number moves by the offset.

      `GetTimeSinceBaseTime` now takes the build time as a parameter rather than calling
      `Build.Time()` itself, which is what makes both cases testable at all.
- [ ] **Noted, no action: `Sorter.Sort` never terminates on a cyclic comparer.** It restarts the
      outer index on every swap (`i = i - 1`), so a comparer where a < b < c < a keeps finding
      something to swap forever — `List.Sort` would return a wrong order or throw, this hangs the
      process with no clue why. gmd's comparers are partial orders rather than cyclic ones, so the
      only way in is a cycle in the branch hierarchy, which is exactly what the commented-out
      circular-ancestor guard in `DetermineAncestors` (Step 2 finding) was there to prevent. The
      two belong together whenever that one is revisited. No test — it would hang the suite.
- [ ] Noted, no action: two cosmetic quirks, pinned by tests so they cannot change unnoticed.
      - `StringExtensions.FileSize` divides with integer division before applying its `0.##`
        format, so the fraction can never appear: 1536 bytes is `1 KB`, not `1.5 KB`. Nothing in
        gmd calls it today.
      - `Version.Txt()` of a version with fewer than four parts writes the missing ones as -1
        (`0.91 (-1.-1)`), since that is what `Version` reports for them.

## Step 7 — CI and coverage

- [x] A fast job for pull requests, so they get feedback without the full multi-platform publish.
      A PR used to run all of `./build`, i.e. four self-contained ReadyToRun publishes
      (linux-x64, linux-arm64, win-x64, osx-arm64) whose output it then threw away, since only
      the release steps were gated on the branch. The workflow now has two jobs: `test_job` on
      `pull_request` runs `csharpier check` and `./build -l` (tests plus the two linux publishes),
      and the unchanged `build_test_release_job` runs on everything else, i.e. push to `main`/`dev`
      and a manual `workflow_dispatch`.

      Kept the linux publish rather than tests alone, so a PR still exercises a Release publish —
      the packaging is where a break would otherwise stay hidden until after merge. The
      `csharpier check` step has to come first here for the same reason it does in the release job:
      `./build` runs `dotnet test` in Debug, which formats the sources in place. The
      `BUILD_TIME`/`BUILD_SHA` `sed` is not repeated in the PR job — nothing is released from it,
      and `BuildTest` holds with the placeholders either way.

- [x] Every push gets tested, on every branch. The push trigger was `[main, dev]` and the only
      other trigger needs an open pull request, so a feature branch got no CI at all — the one
      case where local testing was the only safety net. It is now `branches: ['**']`, which is the
      pattern that matches a branch name containing a slash (`*` does not).

      The two jobs split the work by what the event is rather than by event type, so exactly one
      of them runs for any event:

      | Event | Job | Publishes |
      | --- | --- | --- |
      | Push to a feature branch | `test_job` | – |
      | Pull request | `test_job` | – |
      | Push to `main`/`dev` | `build_test_release_job` | release / pre-release |
      | Manual run, `main`/`dev` | `build_test_release_job` | release / pre-release |
      | Manual run, other branch | `build_test_release_job` | – |

      The last row is what keeps the manual full build useful: it is now the way to check the
      windows and macOS packaging on a branch, which a pull request no longer does.

      Publishing stayed exactly where it was, behind two independent gates that both key on
      `github.ref`: the job level `if` above, and the `isPublish` output that gates the two
      release steps. `test_job` has no release step at all, and now also drops to
      `permissions: contents: read`, so a branch cannot publish even if a step is added to it
      carelessly later. Both jobs run the whole suite — the same `dotnet test` in `./build`, with
      no category filter, so the integration tests run on every branch too.

      Known and accepted: a push to a branch that has a pull request open fires both the `push`
      and the `pull_request` event, so `test_job` runs twice on that commit. Deduplicating costs
      more complexity than the duplicate run does on free public runners. A `concurrency` group
      would cut the other kind of waste — several pushes in a row queueing up — but it also makes
      superseded runs report as cancelled, so it is left out until it is actually a nuisance.
- [ ] Turn on the `coverlet.collector` that is already referenced but unused; report coverage.
      This is the natural second step for the new `test_job`.
- [ ] Consider a coverage floor once the number stabilizes — as a ratchet, not a hard gate.

### Findings

- [x] **Fixed: `./build` reported success when a `dotnet publish` failed.** There is no `set -e` and
      only the `dotnet test` step checked `$?`, so a failed publish left the following `cp` to fail
      too — both printing an error nobody acted on — and the script carried on to its `exit 0`. That
      made the whole point of building on a PR moot: the job would have gone green on exactly the
      packaging break it is there to catch. On push it was less bad but still late, since the
      failure only surfaced as a missing file when `action-gh-release` uploaded the assets.

      Each of the four publishes now exits 1 with the RID in the message. This makes `./build` match
      `build.bat`, which has had `if errorlevel 1 exit /b 1` after every publish all along — the two
      were out of sync, not both wrong. Fixed the `Building widows ...` typo on the way, since
      `build.bat` already says windows.

---

## Step 8 — Maintainability

- [x] **Fixed the layering violation: `gmd/Server/` no longer references `gmd.Cui`.** The three
      `using gmd.Cui.RepoView;` lines existed for a single static class, `RepoExtensions`, which
      held two unrelated kinds of helper. Split along that seam rather than moved wholesale:
      - `CurrentBranch`, `CurrentCommit` and `GetUncommittedFiles` extend `Server.Repo` and only
        read the model, so they moved down to `gmd/Server/RepoExtensions.cs` where the type they
        extend lives. `AugmentedService.RebaseBranchAsync` was the one real user in the Server
        layer — the usings in `IServer.cs` and `Server.cs` were already stale.
      - `ShortNiceUniqueName` truncates a branch name to 16 characters with a `┅` glyph, i.e. it
        is a drawing concern with no business in the Server layer. It stayed in the UI, as
        `gmd/Cui/RepoView/BranchExtensions.cs` — the file is now named after the type it extends,
        since `RepoExtensions` no longer described it.

      `CommitDlg` was the only caller that needed a `using` change. Verified by build,
      `csharpier check` and the full suite (320 tests, unchanged).
- [x] **Fixed the last upward reference: `FileMonitor` no longer reaches into the UI.** It called
      `Cui.Common.UI.Post` (twice) and `Cui.Common.UI.AddTimeout` by fully qualified name, so it
      was invisible to a search for `using gmd.Cui` and was not what the item above described. It
      was also the harder one — a real runtime dependency rather than a stale import: the Server
      layer marshalled its own change events onto Terminal.Gui's main loop and drove its
      one-second timer from it.

      Now behind `IMainThread` (`gmd/Utils/IMainThread.cs`), the main loop reduced to the two
      things a lower layer needs from it — `Post(Action)` and
      `RunPeriodically(TimeSpan, Func<bool>)` — implemented by `MainThread` (`Cui/Common/`) over
      `UI`, and injected into `FileMonitor`. Auto-registered like everything else, so there was no
      DI to write. With it, `using Terminal.Gui;` left `FileMonitor` too (it was there only for the
      `MainLoop` parameter of the timer callback), and so did a dead
      `using Timer = System.Timers.Timer;`. The `object timer` field, which only ever served as a
      "already started" flag, became a `bool`.

      Timer registration deliberately stayed in `Monitor()` rather than moving to the constructor:
      `Program.Main` resolves the whole DI graph *before* `Application.Init()`, so a constructor
      registration would silently find no main loop. `Monitor()` is only called from
      `AugmentedService` when a repo is read, i.e. always after the UI is up.
- [x] The payoff, as predicted: `FileMonitor`'s debounce is now testable, and it is the first thing
      in this codebase reachable only because the UI dependency went behind an interface. Ten tests
      in `FileMonitorTest`, driving `FakeMainThread` (`gmdTest/Fixtures/`) — which queues posted
      actions and captures the periodic callback — plus an internal `Now` clock seam on
      `FileMonitor`, so the one-second trigger delays cost no wall clock time. Suite went from 320
      tests to 330, still under two seconds.

      Characterized: the trigger delay and its exact `<` boundary, that an event is raised once and
      then consumed, that a repo change replaces (rather than defers) the file change of the same
      tick, that `Pause` defers rather than drops, what each of `SetReadRepoTime` /
      `SetReadStatusTime` clears, that `.lock` files and gmd's own metadata writes are not repo
      changes, and that events are posted rather than raised inline.

      One guess corrected by writing the test, which is the reason these are discovered rather than
      predicted: the debounce is a *sliding* window, not a fixed one. Every change overwrites the
      pending event, so the delay restarts from the latest change — a folder being written to
      continuously defers its event for as long as the writing lasts rather than raising one a
      second.
- [x] Deferred to Step 11 and settled there: `Utils/Clipboard.cs` wrapped `Terminal.Gui.Clipboard`,
      the one direct Terminal.Gui reference left outside `Cui/`. Not the same problem — `Utils` is
      a leaf the other layers depend on, not a layer reaching up past its neighbors — but it did
      mean `gmd.Utils` could not be lifted out as a UI-free library as it was. Deliberately left
      alone here because the file needed a rewrite rather than an import change, and that rewrite
      was a user-facing bug fix with its own step. Nothing below `gmd/Cui/` names a Terminal.Gui
      type any more.
- [x] **Broke up `BranchStructureService.cs` (989 lines) along its pipeline stages.** The file is
      now 54 lines and holds nothing but `DetermineCommitBranches`, i.e. the six pipeline steps and
      their comments, delegating one stage at a time. Seven files, largest 410:

      | File | Lines | What it is |
      | --- | --- | --- |
      | `BranchStructureService` | 54 | The pipeline, and nothing else |
      | `CommitGraphService` | 86 | Stages 1–2: branch tips onto commits, parents/children linked |
      | `CommitBranchService` | 166 | Stage 3: the commit loop and the ordered rule chain |
      | `CommitBranchRules` | 410 | The 13 `Try…` rules the chain dispatches to |
      | `BranchAmbiguity` | 179 | `TrySetBranch` (repair) and `AddAmbiguousCommit` (give up) |
      | `BranchFactory` | 57 | The three `Add…Branch` creators for branches git no longer has |
      | `BranchHierarchyService` | 151 | Stages 4–6: parent branch, root branch, ancestors |
      | `WellKnownBranches` | 21 | `MainNamePriority` and the truncated branch name |

      Three splits are worth the words, since they are the ones that are not simply "a stage":
      - `CommitBranchRules` out of `CommitBranchService`, because the value of `DetermineCommitBranch`
        is the *order* of its rules — that is the whole inference strategy, and it now fits on one
        screen instead of being 570 lines with the rules inlined between the branches of the chain.
        The interface doubles as a table of contents of the rules.
      - `BranchAmbiguity`, because `TrySetBranch` and `AddAmbiguousCommit` are the two ends of the
        same idea (repair an ambiguous stretch once evidence turns up; give up and record the
        candidates so the user can choose) and were 180 lines sitting between unrelated rules.
      - `WellKnownBranches`, because `MainBranchNamePriority` was needed by three of the stages and
        the truncated branch name by two, so leaving either behind would have made a stage depend on
        the orchestrator.

      Pure code movement: no logic changed, no method renamed, every comment kept including the
      typos in the pipeline steps. The only edits were the mechanical ones — `static` dropped where
      a method became an interface member, the two shared constants moved, and an unused local
      (`amBranch` in `TryIsChildAmbiguousCommit`) dropped rather than moved. The three stage classes
      are DI-registered like everything else; the helpers below them are static, since they have no
      state and no dependencies.

      Verified beyond the suite (330 tests, unchanged), because a green suite is not the bar for
      this file: a throwaway probe dumped the full inferred structure of a real 1758-commit repo —
      every commit's branch, primary name, nice name, ambiguity flags and child ids, plus every
      branch's tip, bottom, parent, ancestors, related and ambiguous branches — for the code before
      and after. 1829 lines, 70 branches, byte identical.

      `RepoBuilder.NewAugmenter()` is now the one place the pipeline is wired by hand;
      `AugmentedServiceIntegrationTest` had a second copy of that wiring and now calls it.
- [x] **Broke up `RepoView.cs` (1068 lines) along the seam between the view and what the user does
      to it.** Nearly half the file was input handling — the 60 line key/mouse table plus its
      handlers — and what made those handlers long rather than one-liners was the *hoover*, i.e.
      which branch the pointer or the cursor is on, since most keys act on the hoovered branch when
      there is one and on the current commit when there is not. Three files:

      | File | Lines | What it is |
      | --- | --- | --- |
      | `RepoView` | 504 | The view: reading and refreshing the shown repo, and drawing the page |
      | `RepoViewInput` | 533 | Every key and mouse button, and the handlers they dispatch through |
      | `Hoover` | 128 | Where the hoover is, and the index math of moving it |

      `Hoover` is the part that is worth testing and the reason the split is where it is. The five
      hoover fields and the "has it actually moved" comparisons were spread over ten methods of the
      view, so nothing about them could be reached without a terminal. It is now a plain class with
      no Terminal.Gui, no commands and no drawing: the mutating methods return whether the hoover
      moved and the view decides to redraw, and the navigation is `NextLeft` / `NextRight` /
      `Locate` / `FollowCurrentIndex` returning the branch to hoover rather than hoovering it.

      `RepoViewInput` gets what it needs back from the view through `IRepoViewInputHost`, five
      members, rather than the concrete view — `ViewRepo` and `Menus` because both are replaced
      every time a repo is shown, and `ToggleDetails` / `ToggleDetailsFocus` / `RefreshAndFetch`
      because they are the view's own state. It is `new`ed by `RepoView`, like the menus are.

      Mostly code movement, with three deliberate exceptions:
      - `OnCursorUp` and `OnCursorDown` were the same 22 lines twice, differing only in `Move(-1)`
        vs `Move(1)`, and are now one `MoveCursor(delta)`.
      - The two commented-out sketches of moving the hoover left/right *across a page* went. The
        sentence saying it was tried and disabled because it was confusing is kept — that is the
        part worth having — but the code referred to fields that no longer exist, so keeping it
        would have been keeping something untrue. It is in git if anyone revisits the idea.
      - `Copy`'s text building is now `SelectedCommitsText`, an `internal static`, so the rule that
        a multi-row copy takes only the commits of the branch it started on is testable.

      25 new tests in `gmdTest/Cui/RepoView/` (`HooverTest`, `RepoViewInputTest`), suite 330 → 355.
      `HooverTest` drives the hoover over a real graph built by `GraphCreater`, not hand-made
      branch lists, since the whole of the hoover is where the graph puts branch columns — and that
      is what catches the case below. Verified beyond the suite: the key/mouse table is byte
      identical to the old one after the mechanical renames, and a throwaway probe resolved the
      whole DI graph (`IRepoView` and `Program`) to check the view still constructs.

      Two things the tests pinned that are easy to get wrong when touching this again:
      - A branch and its remote are drawn as two columns with one primary name, so moving right has
        to leave a branch by its *last* column (`FindLastIndexBy`) while moving left finds its
        first. Using the same lookup for both would hoover such a branch twice on the way right.
      - `FollowCurrentIndex` gives up a branch that is no longer on the row but still moves the
        hoover row to the new current row, leaving the row set while the column and the current row
        index are cleared. Deliberate — the commit of the new row stays hoovered — but the mixed
        state reads like a bug unless the test says otherwise.
- [x] **Broke up `UIDialog.cs` (715 lines) along the seam between the dialog and the views it is
      made of.** Six of the seven types in the file were custom Terminal.Gui views that `UIDialog`
      only happens to be the factory for, and each of them is used on its own elsewhere. Six files,
      largest 383:

      | File | Lines | What it is |
      | --- | --- | --- |
      | `UIDialog` | 383 | The builder: the `Add*` methods, `Show()` and the validations |
      | `UIComboTextField` | 157 | A text field with a drop down list of suggestions |
      | `UILabel` | 101 | A label that draws a styled `Text` and can be clicked |
      | `BorderView` | 52 | A rectangle in one color, since `View.Border` does not draw for all views |
      | `UITextView` | 29 | Multi line input where tab moves focus instead of inserting a tab |
      | `UITextField` | 20 | One line input that returns its text trimmed |

      Pure code movement, and verified as such rather than asserted: each moved class is byte
      identical to the lines it came from, and so is what is left of `UIDialog`. The only additions
      are one comment per file saying what the type is, since a file named after a type should say
      what it is for.
- [x] **Broke up `ContentView.cs` (651 lines), the scrollable list of rows that most of gmd is
      drawn in** — the log view, the diff view, the menus and several dialogs are all one. Two
      thirds of it was not view code at all but index math: where the view is scrolled to and what
      is selected, neither of which could be reached without a terminal. Three files:

      | File | Lines | What it is |
      | --- | --- | --- |
      | `ContentView` | 455 | The view: drawing, the keys and mouse buttons, and fetching the rows |
      | `ContentScroll` | 184 | The first shown row, the cursor row and the row count, and moving them |
      | `ContentSelection` | 157 | The selected rows and columns, and extending them by key and by drag |

      The two new classes follow `Hoover` from the item above: no Terminal.Gui, and the mutating
      methods return whether anything actually moved so the view is the one that decides to redraw.
      Two details are worth knowing before touching this again:
      - The view height is read through callbacks (`() => ViewHeight`) rather than stored, since a
        view is resized while it is shown. It is also two heights rather than one — a view with a
        top border has one row less for its content than it has height — and only some of the math
        knows the difference, which is what the last two `ContentScrollTest` tests are about.
      - The mouse drag no longer scrolls from inside the selection code: `Drag` returns which way
        the drag moved and the view scrolls when it reaches its edge. That moves the scroll to after
        the selected region is updated rather than before, which is safe because the region math had
        already read the first shown row before either could run.

      46 new tests in `gmdTest/Cui/Common/` (`ContentScrollTest`, `ContentSelectionTest`,
      `ContentViewTest`), suite 355 → 401. `ContentViewTest` is the surprise of the three: a
      `ContentView` built from a list of rows can be moved and scrolled with no Terminal.Gui driver
      at all, as long as its `Frame` is set, so the view's own delegation is covered too — every
      part of it except drawing. Verified beyond the suite by a throwaway probe that resolved the
      whole DI graph and drove a real view through its movement API.

      The split was pure movement, so the three bugs the tests uncovered are the three items below
      rather than part of it. None of them could have been found without pulling the math out of the
      view first, and each is a one line change.
- [x] **Fixed: shift+up selected two rows per key press while shift+down selected one.**
      `ContentView.ProcessHotKey` moved the cursor up after `OnSelectUp` had already moved it, so a
      selection made upwards grew by two rows per press and left the cursor a row below the
      selection, while the same thing downwards grew by one. Visible in every list in gmd — nothing
      handles shift+up before `ContentView` does — and it is what decides which commits ctrl-c
      copies and which range the commit menu acts on, so selecting upwards took a row more than the
      user asked for. The second `Move(-1)` is gone. `TestShiftUpSelectsOneRowPerKeyPress` and
      `TestShiftDownSelectsOneRowPerKeyPress` are written as what the view does, key press by key
      press, and are deliberately mirror images of each other so that this cannot drift apart again.
- [x] **Fixed: `MoveToTop()` only reached the top when the cursor was on the top row of the view.**
      It was `Move(-FirstIndex)`, i.e. it moved the cursor up by however many rows the view was
      scrolled down, so with the cursor further down the view it stopped exactly that many rows
      short and the rows above stayed out of sight. Now `Move(-CurrentIndex)`, since the cursor row
      takes the first shown row with it.

      The one caller not in scroll mode — where it did reach the top, and still does — is
      `FilterDlg.UpdateFilteredResults`, which calls it to show a new set of filter results from the
      top. With the log scrolled down when the filter was opened, the first results were out of
      sight, and worse, `ShowCommitInfo` reads `CurrentIndex`, so the commit shown below the list
      was not the one the list appeared to be pointing at, and Enter picked that one.
- [x] **Fixed: scrolling with the cursor below the content put it on the first row.** `Scroll` put a
      cursor that would end up below the view back at `newFirst - ContentHeight - 1`, which is
      negative and then clamped to 0, where `newFirst + ContentHeight - 1` was meant. Only reachable
      in a view with a top border, since `Move` bounds the cursor by the view height while the
      border takes a row off the content height — so it was invisible in gmd today, its one bordered
      view being the commit details view, which hides its cursor. Fixed anyway, since it is a
      trap for the next view that draws a border.
- [x] **Broke up `Menu.cs` (602 lines), the context menu every menu in gmd is drawn as.** It held
      five types, and two thirds of the `Menu` class itself was not view code but the geometry of
      where the menu goes and the text of the rows it draws. Five files:

      | File | Lines | What it is |
      | --- | --- | --- |
      | `Menu` | 353 | The view: showing the dialog, the keys and the mouse, and opening a sub menu |
      | `MenuDimensions` | 78 | Where the menu is drawn and how wide each of its parts is |
      | `MenuExtensions` | 110 | The extension methods menus are built with |
      | `MenuRows` | 69 | The items drawn as the `Text` rows the view shows |
      | `MenuItem` | 26 | `MenuItem`, `SubMenu` and `MenuSeparator` |

      `MenuDimensions` and `MenuRows` follow `Hoover` and `ContentScroll` from the items above: no
      Terminal.Gui, so the screen size is passed in rather than read from `Application.Driver`, and
      the dimensions are passed to the rows rather than shared as a field. That makes both testable,
      and a menu row is a picture in the test the same way a graph row is.

      Pure code movement otherwise, verified line by line against the old file: `MenuItem` and
      `MenuExtensions` are byte identical, and the only other differences are the parameters the two
      extracted functions now take and `Dimensions` being renamed `MenuDimensions`, since a record
      called `Dimensions` at namespace scope says nothing about what it is.
- [x] **Broke up `AugmentedService.cs` (681 lines) along the seam between reading a repo and
      writing to it.** The file's own comment describes only the first: it returns augmented repos.
      The rest was git write operations, and the two largest pieces of those had nothing to do with
      each other. Three files:

      | File | Lines | What it is |
      | --- | --- | --- |
      | `AugmentedService` | 367 | Reading a repo and its status through the pipeline, plus the writes that go straight to git (commit, tags, the metadata that resolves ambiguity, squash) |
      | `BranchWriteService` | 211 | The writes that need the augmented repo to work out what git to run: create, switch, merge, rebase |
      | `Uncommitted` | 165 | The virtual uncommitted commit: adding, updating and removing it |

      `Uncommitted` is the payoff: `AdjustUncommitted` and `GetUncommittedCommit` are pure `Repo` →
      `Repo`, with no git, no disk and no dependencies at all, and were the largest such block left
      in the Server layer. `AugmentedService` delegates the six branch write operations in one line
      each, which is what `Git` already does for the per-area git services.

      Pure code movement with one deliberate exception: `MergeBranchAsync` and `RebaseBranchAsync`
      held the same 20 lines twice — "a branch and its remote can have different tips, so use the
      youngest of the two" — and are now one `YoungestTipName`. Verified line by line: every other
      moved method is byte identical.
- [x] **Broke up `BranchCommands.cs` (740 lines) into its three groups of commands.** The interface
      is unchanged, so no menu or key handler moved; `BranchCommands` is still what they call and
      now delegates two groups on to the classes that own them.

      | File | Lines | What it is |
      | --- | --- | --- |
      | `BranchCommands` | 359 | Showing, hiding, switching, diffing, merging, and how a branch is drawn |
      | `BranchPushPullCommands` | 272 | Pushing and pulling, and the four predicates the menus enable their items with |
      | `BranchCreateCommands` | 239 | Creating a branch from a branch or a commit, and deleting one |
      | `CommandRunner` | 22 | Running a command in the background with progress and an error box |

      Two things beyond the movement, both to avoid making the duplication worse:
      - The rules for what can be pushed and pulled, and which branches 'push all' and 'pull all'
        act on, are plain functions of the shown repo, so they are now `internal static` and take a
        `Repo`. That is what the nine new tests drive, including the diverged branch that can be
        pulled but not pushed — the case the Step 3 finding was about, which had no test of its own
        until now.
      - `Do`, the eleven-line "run in the background, show progress, message box on error" helper,
        was already copied into all three `*Commands` classes and would have become five. It is now
        `CommandRunner.Do`, and each class keeps a one-line `Do` so no call site changed.

      Every command that moved is byte identical to the lines it came from, checked one by one.
- [x] **Fixed: gmd crashed on any repo whose local branch was behind its remote and pointed at the
      first commit of the repo.** `ViewRepoCreater.SetBehindCommits` reads the commit a local branch
      branched out from as `localBottom.ParentIds[0]`, with nothing to say what happens when the
      bottom *is* a root commit and so has no parent. The `ArgumentOutOfRangeException` is thrown
      outside the `R` error handling entirely — the same shape as the Step 1 date bug — so the log
      view never appeared.

      Reachable whenever a repo's history reaches back to its first commit on the branch being
      shown and that branch has not been pulled: clone a repo that had one commit, someone pushes,
      you fetch. Any local commit at all avoids it, since the branch bottom is then one of those,
      which is why it survived this long. `localBase` is now null when there is no such commit and
      the loop simply has one stop condition less. Found by writing the push/pull tests above, and
      covered by `ViewRepoCreaterTest.TestBranchBehindAtTheRootCommitHasRemoteOnlyCommits`.
- [ ] Noted, no action: three small things the new tests pin so they cannot change unnoticed.
      - A sub menu row is two columns wider than the others, since `MenuRows` writes the columns
        reserved for the ` >` marker *after* the marker rather than instead of it. Invisible, the
        two extra columns being blank and clipped by the view.
      - On a screen too narrow for the menu, `MenuDimensions` floors the item text at 10 columns,
        which is wider than the view it is drawn in. No terminal gmd is usable in is that narrow.
      - Adding the uncommitted commit gives it its parent but does not add it to that parent's
        children, while removing it filters those child lists all the same. Invisible, since the
        graph draws the row from the commit's own parent ids, but the two directions not matching
        is worth knowing before relying on a commit's children.

      Suite went from 402 tests to 441, in five new test classes (`MenuDimensionsTest`,
      `MenuRowsTest`, `BranchPushPullCommandsTest`, `UncommittedTest`, `BranchWriteServiceTest`).
      Verified beyond the suite as the earlier splits were: a throwaway probe resolved the whole DI
      graph, including the two new `Func<IViewRepo, IRepoView, …>` factories Autofac has to generate
      and the new `IBranchWriteService` registration, and the Release build (i.e. `csharpier check`)
      is clean.
- [x] Two different `Converter` classes (`Server/Private/` and `Server/Private/Augmented/Private/`)
      — confusing when both are in scope; consider renaming. Renamed after what each converts
      *from*, so the two pipeline steps read in order: `Augmented/Private/Converter` →
      `WorkRepoConverter` (`WorkRepo` → the immutable `Server.Repo`) and `Server/Private/Converter`
      → `ViewRepoConverter` (that repo → the view repo the UI renders, plus the git diffs).
      Interfaces renamed to match, files renamed with them, and a one-line comment on each names
      the other, since which comes first is the thing that was actually unclear. The two tests
      that had worked around the clash with `using AugConverter = …` / `using ViewConverter = …`
      aliases now use the real names.
- [x] `TagServis.cs` — filename typo, should be `TagService.cs`. Renamed; the types inside were
      already `ITagService`/`TagService`, so nothing else changed.
- [x] Reconsider `NoWarn IDE0090;CA1825` in `gmd.csproj` once formatting churn has settled.
      Deleted, after the collection expression sweep left `CA1825` with nothing to report. Both
      diagnostics are now unsuppressed and both builds are still 0 warnings; the cross-reference
      comment in `.editorconfig` went with it, so the explicit-`new` preference is stated once,
      where the rest of the style lives.
      Measured by dropping the `<NoWarn>` line and re-running the analyzers, since neither was
      what its name suggests any more. Both were added in `65528e8` (2023-08-05), three years
      before CSharpier arrived, so they never had anything to do with formatting.
      - `IDE0090` (target-typed `new()`) reports **nothing** un-suppressed, even though 11 files
        hold the candidate pattern (`List<Button> x = new List<Button>();` in `MessageDlg.cs`,
        `GraphCreater.cs`, `Augmenter.cs`, …). `.editorconfig` already states the preference the
        other way (`csharp_style_implicit_object_creation_when_type_is_apparent = false:silent`),
        and IDE rules do not run at build without `EnforceCodeStyleInBuild` anyway. The entry is
        dead weight, and the comment at `.editorconfig` explaining the duplication goes with it.
      - `CA1825` (`new T[0]` → `Array.Empty<T>()`) hides 12 real sites: `Config.cs`, `Updater.cs`,
        `Repo.cs` (six in a row), `GitLog.cs`, `Server.cs`, `Augmenter.cs`, `AugmentedService.cs`.
        Its default severity is *info*, so un-suppressing it can never fail a build — the only
        effect is IDE hints. (`GlobPatterns/Glob.cs` has one too, exempt as vendored code.)
      - Sequencing: those 12 sites are exactly what the collection expression sweep below
        (IDE0300) rewrites to `[]`, so do the sweep first. Un-suppressing CA1825 before it aims
        the IDE's fix at `Array.Empty<T>()`, the wrong direction for this codebase — and after it
        CA1825 has nothing left to flag, so the whole `<NoWarn>` line can just be deleted with no
        code change. `[]` for an array compiles to `Array.Empty<T>()`, so nothing is lost.

### Migrate to collection expressions

Collection expressions (`[]`) are the preferred style going forward. The codebase predates C# 12,
so the analyzers are enabled as **suggestions** — new and touched code adopts `[]`, the rest
migrates over time. Sites, counting each one once (`dotnet format` reports some twice, which is
where the earlier "156 sites" came from):

| Diagnostic | Sites | Left | What it changes | Risk |
| --- | --- | --- | --- | --- |
| IDE0028 | 71 | 0 | `new List<T>()` → `[]` for initializers | Safe, mechanical |
| IDE0300 | 35 | 2 | `new T[0]` / `new[] { … }` → `[]` | Safe, mechanical |
| IDE0301 | 3 | 0 | empty collection → `[]` | Safe, mechanical |
| IDE0305 | 14 | 14 | fluent `.ToList()` / `.ToArray()` → `[.. x]` | **Review by hand** |

- [x] Sweep the safe ones as one mechanical commit (then run CSharpier, since it normalizes the
      resulting layout but does not do the conversion itself):
      `dotnet format style --diagnostics IDE0028 IDE0300 IDE0301 --severity info gmd.sln`
      Done: 107 of the 109 safe sites, 45 files. Two hand corrections to what the tool produced:
      - `Utils/GlobPatterns/Glob.cs` reverted, so the vendored code stays as it came. The tool
        rewrote one arm of `(last == null) ? new string[0] : new[] { last }` and not the other,
        which is worse than leaving it; `--diagnostics` overrides the `severity = none` that
        `.editorconfig` sets for that folder, so it has to be reverted by hand each sweep.
      - `Server/Private/Server.cs` — the tool left `? new[] { commit } : []`, one arm of a
        ternary in each style. Written as `? [commit] : []`, which compiles because the
        conditional is target-typed from `Concat`'s `IEnumerable<Commit>`.

      Nothing to review beyond that: the only sites where `[]` is not simply the same type are
      the interface-typed ones, and there the compiler picks by target type — `ICollection<T>` /
      `IList<T>` get a mutable `List<T>`, `IEnumerable<T>` / `IReadOnlyList<T>` get an empty
      array. `Menu.Items` (`ICollection<MenuItem> => []`, added to by every menu) was the one
      that would break loudly if that were wrong, so a throwaway test added to it and checked
      each call still returns a fresh collection. 441 tests pass, Release build clean.
- [ ] IDE0305 by hand, in a separate commit. Converting `x.ToList()` to `[.. x]` can change the
      concrete type produced behind an `IReadOnlyList<T>` return, and this codebase returns
      `IReadOnlyList<T>` widely, so each site needs a look rather than a blanket fix. The 14 are
      `MessageDlg.cs` (2), `UIComboTextField.cs` (1), `UIDialog.cs` (3), `StatusService.cs` (6)
      and the vendored `GlobPatterns/` (2, leave them).
- [x] Sequencing note: test coverage is still thin outside the augmentation pipeline and
      `LogService`, so a 156-site sweep is less safe than it looks. Either do it after Steps 4–6
      widen coverage, or accept it as a reviewed mechanical change verified by build + CSharpier.
      Taken the second way, after Step 8 rather than before it, so the split-out services and the
      441 tests were already in place under it.
- [x] Unblocked by the sweep: no `new T[0]` is left, so `CA1825` reports nothing and the
      `<NoWarn>` line in `gmd.csproj` was deleted outright — see the item in Step 8.
- [ ] Open question: target-typed `new()` (IDE0090) is the same "codebase predates the feature"
      category. Adopt it too, or keep types explicit? Purely a style question now — keeping types
      explicit needs no build setting, since `.editorconfig` says so on its own and IDE0090 stays
      silent without the `<NoWarn>` entry that used to duplicate it.

## Step 9 — Framework and dependency updates

Deliberately after the tests, so regressions are detectable. Current status of every dependency:

| Package | Current | Latest | Notes |
| --- | --- | --- | --- |
| Terminal.Gui | 1.19.0 | 2.4.17 | 1.x is now current. 2.x is a major rewrite of the UI layer — see below. |
| Autofac | 9.3.2 | 9.3.2 | Done. |
| DiffPlex | 1.9.0 | 1.9.0 | Done. |
| MSTest.\* | 4.3.3 | 4.3.3 | Done. |
| Microsoft.NET.Test.Sdk | 18.8.1 | 18.8.1 | Done. |
| coverlet.collector | 10.0.1 | 10.0.1 | Done. |

- [x] Test packages first (MSTest 3.6.0 → 4.3.3, Test.Sdk 17.11.1 → 18.8.1, coverlet 6.0.2 →
      10.0.1) — contained to `gmdTest`, no product code touched. Two breaking changes in MSTest 4,
      both mechanical:
  - `Assert.ThrowsException<T>` is gone (8 sites in `ResultTest`, `EnumerableExtensionsTest`,
    `GlobTest`). Replaced with `Assert.ThrowsExactly<T>`, **not** `Assert.Throws<T>`: MSTest 3's
    `ThrowsException<T>` required the exact type, and `Throws<T>` accepts derived ones, so
    `ThrowsExactly<T>` is the like-for-like replacement.
  - `[DataTestMethod]` is obsolete (MSTEST0044; 4 sites). `[TestMethod]` carries `[DataRow]` on
    its own now.

    Left as they are on purpose: the separate `MSTest.TestAdapter` + `MSTest.TestFramework`
    references rather than the `MSTest` meta-package, and VSTest rather than
    `EnableMSTestRunner` — Microsoft.Testing.Platform would change the `dotnet test` command
    line in `./test` and needs a different coverage extension than `coverlet.collector`.
    441 tests pass, `--collect:"XPlat Code Coverage"` still writes a cobertura report, Release
    build and `csharpier check` clean.
- [x] `DiffPlex` 1.7.2 → 1.9.0 and `Autofac` 8.1.0 → 9.3.2. Version bumps in `gmd.csproj` only —
      neither needed a source change, and the solution builds with no warnings. Both are used in
      exactly one place, which is why a major Autofac bump is a small change: `DiffPlex` is
      `new Differ().CreateCharacterDiffs` in `Cui/Diff/DiffService.cs`, and `Autofac` is
      `Utils/DependencyInjection.cs`, whose `RegisterAssemblyTypes` / `FindConstructorsWith` /
      `OwnedByLifetimeScope` API is unchanged in 9.x.
  - Verified by running the app, not just the suite: neither line is covered by a test. Nothing
    resolves the container (`./test` builds services by hand) and `DiffServiceTest` covers
    `Git/DiffService`, the git-output parser, not the `Cui` view that calls DiffPlex. So gmd was
    started under tmux against a throwaway repo, which resolves the whole object graph, and `d`
    on the uncommitted-changes row drew the side-by-side diff. `beta gamma delta` vs
    `beta gemma delta` came back with only the `a`/`e` background-colored, i.e. DiffPlex returned
    a one-character diff block rather than falling back to a whole-line diff. `~/gmd.log` had no
    exception, and `dotnet list package --vulnerable/--deprecated` are clean.
  - Worth knowing for the remaining items: the DI container is a **runtime** dependency with no
    test behind it. `RegisterAllAssemblyTypes` is convention-based, so a registration mistake
    surfaces as a resolve failure at startup, not as a compile error — start the app after
    touching it. `--version` is not enough on its own: `Program.Main` resolves `IProgramCommands`
    and returns before `Resolve<Program>()`, so it never builds the UI half of the graph.
- [x] .NET 8 → .NET 10 (LTS), ahead of .NET 8 support ending Nov 2026. Seven files, all of them
      the same string change, and no product code touched at all: both `TargetFramework`s
      (`gmd.csproj`, `gmdTest.csproj`), `DOTNET` in `build` and `build.bat` (it spells the
      `bin/Release/$DOTNET/<rid>/publish` copy paths, so a miss breaks the build script rather
      than the compile), the `program` path in `.vscode/launch.json`, `dotnet-version: '10.x'` in
      both CI jobs, and the devcontainer image `mcr.microsoft.com/devcontainers/dotnet:10.0`.
  - Nothing in the source needed changing — no analyzer warnings, no obsoleted API, no
    `global.json` to pin. Every dependency already resolves for net10.0: Terminal.Gui 1.17.1 is
    netstandard2.0, and Autofac / DiffPlex / MSTest / coverlet / `CSharpier.MsBuild` all restore
    and run unchanged. `dotnet list package --deprecated/--vulnerable` stay clean.
  - Verified on SDK 10.0.302: solution builds with 0 warnings, 441 tests pass (integration ones
    included, so the real `git` path is covered), `csharpier check` clean, and `./build -l`
    publishes both linux-x64 and linux-arm64 self-contained single-file ReadyToRun — the part
    most likely to break on a framework bump, since it cross-compiles with crossgen2.
  - Then started under tmux against a throwaway repo, for the reason the item above records: the
    DI container has no test behind it and `--version` returns before the UI half of the graph is
    built. The log view drew the branch graph and `d` drew the side-by-side diff. `~/gmd.log` had
    no exception — only the expected DEBUG failures for a repo with no tags, no origin and no gmd
    metadata.
  - Note the devcontainer is *declared*, not rebuilt: an existing container keeps its .NET 8 SDK
    until it is rebuilt, and .NET 8 cannot build a net10.0 project. So "Rebuild Container" is
    required after pulling this, and a stale container fails with NETSDK1045 rather than anything
    that points at the cause.
  - Follow-up found after the rebuild: `./test` printed **nothing** in an interactive terminal.
    The .NET 10 SDK enables the MSBuild *terminal logger* by default (it was off in .NET 8), and
    it swallows the VSTest console logger output completely — the run still passes and still
    exits 0, but not one line reaches the screen. It only shows when stdout is a terminal, so
    piping the output (or CI) hides the problem. Fixed by passing `-tl:false` to `dotnet test`
    in `test`, `build` and `build.bat`.
- [x] **Terminal.Gui 1.17.1 → 1.19.0, which fixes the 100% CPU bug on Linux and macOS.** One line
      in `gmd.csproj`, and that is the whole fix — no product code touched.

      The bug: gmd pinned one CPU core for as long as it ran, on Linux and macOS only. The cause is
      in Terminal.Gui, and it is one character. `UnixMainLoop.Setup` creates a self-pipe and
      registers a poll watch on its *read* end (`wakeupPipes[0]`), but the watch callback drains the
      *write* end (`wakeupPipes[1]`). `read()` on a write-only fd fails, so the byte written by
      `Wakeup()` is never consumed, `poll()` reports `POLLIN` forever, and `MainLoop.Run`'s
      `while (running) { EventsPending(true); MainIteration(); }` never blocks again.

      The trigger is `MainLoop.Invoke` → `AddIdle` → `Driver.Wakeup()`, i.e. `UI.Post`
      (`Cui/Common/UI.cs:45`) — which `FileMonitor` and every background operation that marshals
      back to the UI thread go through. So the spin starts within seconds of launch and never stops.
      It needs no user input at all, which is why it was mistaken for a key or mouse listener.
      Windows was never affected, `WindowsDriver` having its own main loop.

      Fixed upstream by commit `433df8b`, released in **1.18.0** — under the title "Fixes #3738.
      CursesDriver stops responding", which is why it was never connected to the CPU reports.
      [Terminal.Gui#3018](https://github.com/tui-cs/Terminal.Gui/issues/3018) describes this exact
      symptom and is *still open*: it was reopened in Jan 2024 to track a v1 fix, and its Nov 2025
      comments concern a v2-only PR. Nobody retested v1 after 1.18.0 shipped. So it is fixed in both
      branches, and it needed a patch bump rather than the 2.x port it was assumed to need.
  - Measured, not assumed — a source diff is not a measurement. gmd run against a throwaway repo on
    a pty, CPU time sampled from `/proc/<pid>/stat` over three 10 s windows: **99.9 / 100.0 / 100.0 %
    on 1.17.1, and 0.0 / 0.1 / 0.0 % on 1.19.0**. It was already at 100% before any file was
    touched, since gmd's own startup repo read posts to the UI thread.
  - `1.17.1...1.19.0` is 42 commits touching four product files, with no public API change:
    `UnixMainLoop.cs` (the fix above), `WindowsDriver.cs` (#3752, Windows Terminal Preview corrupts
    the app size), `Views/Menu.cs` (#3740, a disabled `MenuItem` throws — gmd builds its own `Menu`,
    so no effect here), and a Windows clipboard availability check (#3541, which guards the
    `TrySetClipboardData` at `Utils/Clipboard.cs:166`). 1.19.0 ships the same `lib/net8.0` asset
    1.17.1 did, so the net10.0 roll-forward and the transitive `NStack.Core` are unchanged.
  - Verified: 0 warnings, 441 tests unchanged, `csharpier check` clean, `./build -l` publishes both
    linux RIDs. Then run against a throwaway repo for the reason the .NET 10 item above records —
    the log view drew the graph, a file change still reached it (so `UI.Post` and `FileMonitor` do
    still work, they merely stopped spinning), `d` drew the side-by-side diff, and `~/gmd.log` held
    only the expected DEBUG failures for a repo with no tags, no origin and no gmd metadata.
- [ ] **Terminal.Gui 1.x → 2.x. Deferred deliberately — "when, not if", no longer "do last".** The
      CPU bug was the only urgent reason to port, and the item above removed it for a one-line diff.
      What remains is a real case, but an unforced one. Written down here so the decision is not
      re-litigated from scratch each time.

      For it:
  - **v1 is frozen.** `v1_release` and `v1_develop` both stop at 2025-06-12, the v1 milestone is
    closed, and no v1 fixes are planned. The CPU bug is the proof of what that costs: two years
    unfixed in the pinned version, and the fix that does exist arrived under an unrelated title.
  - **24-bit color, which is a product feature for this tool specifically.** `BranchColorService`
    has a five color branch palette, because `Cui/Common/Color.cs` is pinned to 1.x's 16-value
    `Color` enum. A tool whose whole premise is showing many branches at once runs out of colors on
    any real repo and starts reusing them. 2.x makes `Color` a true-color struct.
  - **`Cui/` becomes testable.** 2.x ships a `Terminal.Gui.Testing` namespace (`InputInjector`,
    `KeyInjectionEvent`, `MouseInjectionEvent`). Steps 3 and 8 have been *working around* the
    untestability of views by extracting Terminal.Gui-free classes (`Hoover`, `ContentScroll`,
    `ContentSelection`, `MenuDimensions`, `MenuRows`); this would cover the other half directly.
  - **Code that gets deleted rather than ported**: `MessageDlg.cs` (148 lines, a vendored copy of
    1.x's `MessageBox`), `BorderView.cs` (52 lines, which exists only because 1.x's `View.Border`
    does not draw for all views), and the parts of `ContentView`/`Menu` that 2.x has built in
    (per-view scrolling, adornments, `ScrollBar`, `PopoverMenu`).

      Against, for now:
  - **The 441 tests do not cover where the risk is.** They protect the model, the parsers and the
    graph's *content* (`GraphCreater`/`GraphWriter` emit gmd's own `Text`, no driver involved). They
    cover none of drawing, layout, key dispatch, dialogs, or what color reaches the screen — which
    is exactly the break list. Worse, the color assertions that do exist (`GraphText.ColorsOf`,
    `BranchColorServiceTest`) are written against 1.x's `Color` enum, so the safety net itself needs
    porting. **Step 12 exists to fix this, and should be done first** — its tmux tier names no
    Terminal.Gui type, so it survives the port and becomes its acceptance suite.
    **Update: that tier now exists** (12 tests, `gmdTest/Cui/TerminalTest.cs`), so this argument is
    weaker than it was. It is not gone: the tests cover the log view, the menus, the details pane,
    the diff and the filter, but not the dialogs that write, not the mouse, and not colors. Run them
    against a 2.x branch as the first thing after it compiles — a failing screen there is a real
    regression, and a passing one is worth more than the whole 441-test model suite for this
    purpose.
  - **It cannot be a series of small reviewable commits**, which every other step here has been. It
    is a branch that does not compile until it is finished.
  - **2.x is stable-tagged but still moving fast**: 2.0.0 was 2026-04-28 and 2.4.17 is 2026-07-07,
    seventeen releases in ten weeks, with `BREAKING CHANGE` items inside *minor* bumps (2.1.0
    renamed `TableSelection.Cursor`, 2.4.0 moved `Bind`/`PlatformKeyBinding` to a new namespace).
    Porting now means re-porting parts of it later.

      The surface, so the size is not guessed later: 32 files with a real Terminal.Gui dependency
      over ~11.7k lines in `gmd/Cui/`, and 9 custom `View` subclasses. `ColorScheme` and `Toplevel`
      do not exist in 2.x at all (→ `Scheme`, and `Runnable`/`Window`/`IRunnable`); the key API is
      wholly replaced (84 `Key.*` uses, 43 distinct keys, `Key.CtrlMask | X` composition, 5
      `ProcessKey`/`ProcessHotKey` overrides); drawing is replaced (`Redraw(Rect)` →
      `OnDrawingContent`, `Bounds` → `Viewport`, 42 `SetNeedsDisplay` → `SetNeedsDraw`, 6 overrides);
      `ScrollBarView` and `Terminal.Gui.Trees` are gone (~50 refs in the two browse dialogs);
      `Application.RootKeyEvent`/`RootMouseEvent` and `WantMousePositionReports` are gone; the driver
      glyph fields at `Program.cs:62-64` and `MainLoop.Driver.Wakeup()` at `Progress.cs:84` have no
      equivalent; and the namespace is split into `Terminal.Gui.App`/`.Views`/`.ViewBase`/`.Drawing`/
      `.Input`/`.Drivers`, so every file's single `using` becomes several. One thing is easier than
      the migration guide implies: static `Application.Init`/`Run`/`Shutdown`/`Invoke`/`AddTimeout`
      still exist in 2.4.17 alongside the new `Application.Create()` instance model, so the port need
      not adopt `IApplication` on day one. 2.4.17 also ships a single `net10.0` asset, which matches
      gmd's target exactly — no roll-forward, unlike the `net8.0` asset 1.x is running on today.

      One trap worth knowing before reading anything upstream: **the migration guide lives on
      `v2_develop` and describes things that are not in the released package.** It states that
      `TextField` is renamed to `Editor`, for instance, while 2.4.17 still has both `TextField` and
      `TextView` and no `Editor` at all. Check the shipped assembly — `Terminal.Gui.xml` inside the
      nupkg, or reflection over `GetExportedTypes()` — rather than trusting the guide on any specific
      API. That is also how the `FakeDriver`/`FakeMainLoop` visibility in Step 3's finding was
      settled, after the XML docs alone gave the wrong answer (they list only *documented* members,
      so absence from them is not absence from the assembly).

      Start it when there is a concrete want — a real branch palette, a v1 bug with no upstream fix,
      UI-level tests blocking something else, or 2.x's cadence settling — not because the version
      number is old, and not before Step 12 has given it something to be verified against. Note that
      one of the arguments above is now weaker than it looks: "`Cui/` becomes testable" is only half
      true, since Step 3's finding shows the *drawing* half is reachable on 1.x already. What 2.x
      adds over that is `InputInjector`, i.e. the input half — real, but a smaller prize than it
      first appeared. Until then the preparation is what Step 8 is already doing anyway: keep pushing
      logic out of the Terminal.Gui-touching classes, and keep the surface funnelled through
      `UI.cs`, `UIDialog.cs`, `Color.cs` and `ColorSchemes.cs`. When it does start, the order that
      follows from the list above is `Color.cs` + `ColorSchemes.cs` first (everything renders through
      them), then `UI.cs`, then `ContentView.cs`, then `UIDialog.cs` (which carries the ~8 dialog
      files that never reference Terminal.Gui themselves), then the two browse dialogs, with
      `MessageDlg.cs` deleted in favour of 2.x's `MessageBox`.
  - Knock-on for Step 11: it is sequenced after this step only to avoid writing the Windows
    clipboard path twice against `Terminal.Gui.Clipboard`. With the port deferred that reason is
    gone, so Step 11 should be pulled forward — and it removes the last Terminal.Gui reference
    outside `Cui/` while it is at it.

## Step 10 — Deferred / open questions

- [ ] `gmdSetup.exe` is a **prebuilt binary committed to the repo**
      (`gmd/Installation/installer/`). Neither `./build` nor CI builds the Inno Setup installer;
      CI just uploads the committed file. Should CI build it, or should it be documented as a
      manual Windows step?
- [ ] Intel macOS is effectively unreleased: `install.sh` looks for a `gmd_osx` asset for
      Darwin/x86_64, but neither `./build` nor CI produces one. Drop Intel support explicitly, or
      start building it?
- [ ] `Program.MajorVersion`/`MinorVersion` are hand-edited constants. Worth a check that the
      changelog and version bump are not forgotten.
- [ ] No `.runsettings`; tests run sequentially. If parallel execution is ever enabled, the
      culture test in `LogServiceTest` mutates `CultureInfo.DefaultThreadCurrentCulture` and
      would need isolating.

## Step 11 — Rewrite clipboard copy ✅ done

**Reported by the user: copying text works on some systems, not at all on others, and sometimes
works and then does not on the same machine.** `Utils/Clipboard.cs` needed a rewrite rather than a
patch — the reasons below were separate defects that each produce that symptom.

Pulled forward past Step 9. It was originally scheduled after it for one concrete reason: the
Windows path went through `Terminal.Gui.Clipboard`, and Terminal.Gui 2.x changes that API, so doing
this first meant writing the Windows side twice. That reason went away when Step 9 deferred the 2.x
port indefinitely, and waiting would only have meant leaving the copy bug unfixed. The rewrite drops
`Terminal.Gui.Clipboard` entirely, which also settles the Step 8 layering note.

### What was actually wrong

Every one of these was read out of the code rather than reproduced against a failing machine, so
which of them bit the user is still unknown — but they are all real, and the one below that is the
best candidate for "sometimes" has now been measured (see Findings).

- [x] **macOS never worked at all.** The `Build.IsMacOs` branch in `Clipboard.Set` was commented out
      with a `// Does it work ????`, so `Set` fell straight through to
      `R.Error("Clipboard not supported on this platform")`. gmd releases `gmd_osx_arm64`, so this
      was a whole shipped platform with no copy at all. Fixed with `pbcopy`, which is part of macOS.
- [x] **Linux required `xsel`, which most distributions do not install by default** (Debian and
      Ubuntu minimal, Fedora, Arch). There was no fallback to `xclip` and no `wl-copy` for Wayland.
      Under Wayland `xsel` reaches only XWayland, so the copy could *report success* and paste
      nothing into a native Wayland application — which is exactly "works on some systems".
- [x] **The forking-helper problem, the best candidate for "sometimes".** An X selection is owned
      by a live process, so `xsel -i` forks a daemon to hold it — and that daemon inherits the
      redirected stdout/stderr pipes of `Cmd.Command`, which calls `WaitForExit()` with no timeout.
      That is [dotnet/runtime#27128](https://github.com/dotnet/runtime/issues/27128), and the
      commented-out `DoubleWaitForExit` workarounds that sat in *both* `Cmd.Command` and
      `BashRunner` said someone had already hit it and backed out.
- [x] **A failed copy was usually silent.** Three of the five call sites discarded the returned `R`,
      and what the other two reported was `"Clipboard copy not supported on this platform"` — wrong
      and unactionable when the real cause is a missing `xsel`. So the common user experience was a
      menu item that appears to do nothing, with no error and nothing in the log.
- [x] **Windows took a completely different route** for no reason, `Terminal.Gui.Clipboard`, which
      was also the sole Terminal.Gui reference outside `Cui/` (the Step 8 layering note).
- [x] Dead code removed with it: `BashRunner`, called by nothing and throwing instead of returning
      `R`; `LinuxClipboard.cmd`, an unused field; and `LinuxClipboard.GetText`/`InnerGetText`, a
      paste implemented for Linux only and never called. The commented-out `DoubleWaitForExit` in
      `Cmd.Command` went too, replaced by a method that actually handles the case.

### What was built

Four files, 489 tests (was 475), and the last Terminal.Gui reference outside `Cui/` gone.

| File | What it is |
| --- | --- |
| `Utils/ClipboardService.cs` | `IClipboardService`, the chain of ways to copy and the order they are tried |
| `Utils/TerminalClipboard.cs` | OSC 52, i.e. asking the terminal itself |
| `Utils/WindowsClipboard.cs` | The Win32 clipboard, with the retries `OpenClipboard` needs |
| `Utils/Cmd.cs` | Gained `CommandWithStdin`, the process call that survives a detaching helper |

The chain *is* the design, so it is worth writing down. Each entry is tried in order and the first
that succeeds wins:

| Platform | Order |
| --- | --- |
| macOS | `pbcopy`, then OSC 52 |
| Windows | Win32 `SetClipboardData`, then `clip.exe` |
| Linux, Wayland session | `wl-copy`, then the X tools if `DISPLAY` is set too, then OSC 52 |
| Linux, X session | `xclip -selection clipboard`, `xsel --input --clipboard`, then OSC 52 |
| WSL without WSLg | `clip.exe`, then OSC 52 |
| ssh, container, anything with no display | OSC 52 |

Four decisions behind it:

- **A tool is only offered when the session it talks to exists**, i.e. `wl-copy` only when
  `WAYLAND_DISPLAY` is set and the X tools only when `DISPLAY` is. Not an optimization: an X tool
  under a native Wayland session *succeeds* into the XWayland clipboard, which nothing reads, and a
  writer that lies about working is worse than one that is absent. Wayland before X for the same
  reason, since a Wayland session usually has `DISPLAY` set as well.
- **The text goes in on the child's stdin**, which removed the temp file, the `bash -c` and the
  nested quoting all at once.
- **OSC 52 last.** It is the only mechanism that reaches the user's own clipboard over ssh or from
  a container — for a terminal git client, arguably the most valuable case, and one no amount of
  `xsel` fixing would ever cover. It is last because it is the only one that cannot report whether
  it worked: a terminal without support ignores the sequence in silence. Written to `/dev/tty`
  rather than stdout, which Terminal.Gui owns.
- **Every failure is reported and names the cause.** All five call sites now surface the error, and
  the message lists each writer that was tried with what it said, plus what to install.

### Findings

- [x] **The hang is real, and it is a hang rather than a slow path.** Measured with a throwaway
      probe: a helper that forks a child holding the inherited pipes for 10 s made `Cmd.Command`
      take **10010 ms**, while `CommandWithStdin` took **1 ms**. With a real `xclip` the child lives
      until the clipboard is replaced, so the old code would block for as long as the copied text
      stayed on the clipboard — and it blocks the UI thread, since copy is a key handler. That is
      the strongest explanation the code offers for "works, and then does not, on the same machine".
- [x] **`Build.IsMacOS` does exist**, contrary to what this step said before it was done — it is in
      `Build.cs` beside `IsWindows` and `IsLinux`. The macOS branch was commented out anyway, so the
      conclusion (macOS had no copy at all) was right for a different reason than the one written.
- [x] **`CopyCommitId` and `CopyCommitMessage` are unreachable from the UI.** They are on
      `IRepoCommands` and implemented, but no key and no menu item calls either of them — so of the
      five call sites this step set out to fix, only three can be reached by a user: Ctrl+C in the
      log view, in the diff view and in the unicode dialog. Left in place and now correct rather
      than deleted, since both are useful commands that need only a menu entry; worth a decision.
- [x] **The copy became assertable end to end, which nothing in the suite could reach before.**
      Because the tmux session has no display, gmd takes the OSC 52 path, and `set-clipboard on`
      makes tmux keep what it is sent as a buffer — so `TmuxSession.Clipboard()` reads back exactly
      what a copy produced. Two tests in `TerminalTest`. This also closed a hermeticity hole that
      was about to be opened: without `DISPLAY=` and `WSL_DISTRO_NAME=` in the session environment,
      a test run on a developer's desktop would have *overwritten their real clipboard*.
- [x] Characterization, found by writing that test: a selection within one row copies the row **as
      drawn**, graph column included, and with the `|` selection marker standing where the `●`
      current-commit marker normally is. A multi-row selection copies sid and subject instead.
- [x] **Cmd+C cannot be supported on macOS, and it is not gmd that is in the way.** Asked for, and
      settled as "leave the keys alone" — recorded here so it is not investigated a second time.
      Three layers, each on its own fatal:
      - **The terminal keeps the key.** Terminal.app and iTerm2 bind Cmd+C to their own Copy, of
        the *mouse* selection, and never transmit it to the running program. So does kitty,
        Ghostty and WezTerm by default.
      - **The classic key protocol cannot express Cmd.** Its modifier set is Shift/Alt/Ctrl, and
        macOS terminals map Option to Meta, never Command. Only the newer kitty keyboard protocol
        encodes Super, and an application has to opt into it.
      - **Terminal.Gui 1.x has no Command modifier**: `Key` defines exactly `ShiftMask`, `AltMask`
        and `CtrlMask` (checked against the 1.19.0 API), and no driver speaks the kitty protocol.
        Verified rather than assumed — `ESC[99;9u`, i.e. kitty's super+c, sent into a running gmd
        did nothing at all, while Ctrl+C in the same session copied. It is at least swallowed
        cleanly: no stray key, nothing drawn.

      A user who wants the literal key can bind it in the terminal instead: an iTerm2 *profile*
      key binding of Cmd+C to Send Hex Code `0x03` sends Ctrl+C to gmd, and being per profile it
      leaves Cmd+C alone everywhere else. Not documented in `gmd/doc/help.md` — that file is about
      gmd's keys, and this is a setting in someone else's application.

      If this comes up again, the answer that needs no modifier key is a menu item: copy is on
      Ctrl+C only, and the commit menu has no entry for it — which is also where the unreachable
      `CopyCommitId` / `CopyCommitMessage` above would find a home.
- [ ] **Windows and macOS are not verified on hardware** — see below. The Windows path is the one
      that would benefit most from a real check, since it is new code rather than a new command
      line.

### Verified

- **Linux, no display (container, ssh)** — driven in tmux against the built binary: the copy lands
  in the tmux buffer byte for byte, and the log names the writer that did it. This is the tier that
  now runs on every `./test`.
- **Linux, tool path** — with a stand-in `xclip` on `PATH` that reads stdin and then forks a child
  holding the pipes for two minutes, exactly as the real one behaves: the text arrived complete, the
  UI was responsive again in 118 ms, and OSC 52 was correctly *not* used, since the tool reported
  success. What this does not prove is `xclip` itself, only everything gmd does around it.
- **macOS** — not run. `pbcopy` is part of the system and reads stdin, so the risk is low, but it is
  untested.
- **Windows** — not run. Both writers are new: the P/Invoke, and `clip.exe` behind it. They fail
  independently, so a bug in the first degrades to the second rather than losing the copy, but a
  real check on Windows is still owed.

## Step 12 — Terminal (end-to-end) UI testing ✅ tmux tier done

**Do this before Step 9's 2.x port.** The suite has 441 tests and none of them covers what reaches
the screen: not drawing, not layout, not key dispatch, not dialogs, not the colors a user sees. That
is also, precisely, the list of things the 2.x port rewrites — so today the port would be
unverifiable, which is the single strongest argument against starting it. This step is the safety
net that changes that, and it pays for itself long before the port ever happens.

It is the same move this document has made twice already: characterization tests before a risky
rewrite (Step 2 before `BranchStructureService`, Step 3 before the graph work). Step 9 opens with
"deliberately after the tests, so regressions are detectable" — this is the missing half of that
sentence for the UI.

### Two tiers, and only one of them survives the port

This distinction is the whole point of the step, so do not blur it.

| | tmux end-to-end | `FakeDriver` headless |
| --- | --- | --- |
| What runs | the built binary, real git, real pty | views in-process, no terminal |
| Drives | keystrokes (`tmux send-keys`) | direct calls; input is not reachable |
| Asserts on | the rendered screen (`capture-pane -p`) | `FakeDriver.Contents` cell grid |
| Speed | ~seconds per test | ~milliseconds |
| Terminal.Gui API used | **none** | `FakeDriver`, `Application.Init`, `Contents` |
| **Survives the 2.x port** | **yes, unchanged** | **no — rewritten by the port** |

That last row is the sequencing answer. A tmux test says "open this repo, press `d`, expect a
side-by-side diff containing `beta gemma delta`" and never names a Terminal.Gui type, so it is just
as valid against a 2.x build as a 1.x one. Written as characterization tests — capture what gmd does
*today*, not what it ought to do — they become a before/after comparison across the port, which is
the bar `CLAUDE.md` already sets for `BranchStructureService` and the one Step 8 used for the
989-line split. `FakeDriver` tests, by contrast, name types that 2.x removes; they are worth having
for fast feedback during v1's remaining life, but they are not port insurance and should not be
counted as such.

So: build the tmux tier first and treat it as the port's acceptance suite. Add `FakeDriver` tests
opportunistically where a fast unit-level check of drawing is what is actually wanted.

### What is already known to work

Both tiers were proven out in the session that wrote this step, so neither is speculative.

- **tmux.** Installed by `./installtools`. The recipe, the two traps (drive the built binary rather
  than `./run`; poll `capture-pane` rather than sleeping) and the `script` fallback are written up in
  `CLAUDE.md` under "Running the TUI from a non-interactive shell". Driving gmd to the diff view and
  capturing a clean, readable screen takes about a dozen lines of shell.
- **`FakeDriver`.** Public in Terminal.Gui 1.19.0; `Application.Init(new FakeDriver(), null)`
  initializes with no terminal at all and `FakeDriver.Contents` is the drawn cell grid, rune at
  `[row, col, 0]` and attribute at `[row, col, 1]` — so colors are assertable too. `FakeMainLoop` is
  `internal`, hence the `null`. See the Step 3 finding.

### What was built ✅ tmux tier done

Suite went from 441 tests to 475, in one new test class (`gmdTest/Cui/TerminalTest.cs`, 34 tests,
~55 s) over five new fixtures. **No product code was touched** — that was not a constraint chosen
up front, it is what the investigation concluded was possible, and it is what keeps the tier valid
across the 2.x port.

| File | What it is |
| --- | --- |
| `Fixtures/TmuxSession.cs` | The harness: start, capture, poll, send keys, kill |
| `Fixtures/TempHome.cs` | A throwaway `$HOME` — the whole hermeticity story |
| `Fixtures/ScreenText.cs` | The normalizer, sibling of `GraphText` |
| `Fixtures/E2eRepo.cs` | The fixture repo shape, seven commits over two branches |
| `Fixtures/Proc.cs` | A process runner with environment variables and a timeout |
| `Fixtures/TempRepo.cs` | Gained `CommitAtAsync` / `CommitFileAtAsync` / `GitAt` |

Covered: startup and the whole log view, that the run is hermetic, quit by `q` and by `Esc`, the
four column-width arms, the details pane, the commit menu, the help dialog, show/hide branch as a
round trip, a commit diff, the filter, paging a 30-commit log, and the repo-mutating keys —
committing, amending, creating a branch, tagging, switching and merging.

- [x] **Hermeticity, which turned out to be the actual work.** A gmd run *writes* `~/.gmdconfig`,
      **truncates `~/gmd.log`** and **deletes `~/.gmdstate*`**, and none of those paths can be
      redirected — `ConfigService`, `ConfigLogger` and `Upgrader` all anchor on
      `SpecialFolder.UserProfile` with no override. On Unix that resolves `$HOME`, so a throwaway
      home is the only lever, and it covers all three plus `~/.gitconfig` for the git subprocesses.
      This is what the step was missing: it is a bigger risk than any of the determinism items
      below, since it is a side effect on the developer's machine rather than a flaky assertion.
- [x] Deterministic commit ids, not just times. With the author *and* committer dates pinned, every
      input to a commit object is fixed, so the sids on screen are stable across machines and get
      asserted rather than masked. `ScreenText` therefore replaces only the temp repo path.

### Findings

- [x] **The built binary is not a dev instance, so it really does call GitHub.**
      `Build.IsDevInstance()` (`Build.cs:64`) is `CommandLine.Contains("gmd.dll") || ProcessPath ==
      "dotnet"`, which is true for `./run` and **false for the apphost** these tests drive. So
      `RepoView.cs:143`'s update checker runs, and a release newer than the test build would add a
      `⇓` to the application bar, extra items to the repo menu, and could re-open the main menu
      asynchronously mid-test. Disabled by seeding `CheckUpdates: false`.
- [x] **`git log --all --date-order` makes identical commit timestamps a row-order flake.** Ordering
      is by commit date, and a fixture built in under a second has nothing to break ties with. This
      is why the pinned dates are a correctness requirement rather than a cosmetic one — the same
      reason `RepoBuilder`'s times are distinct and increasing.
- [x] **gmd drops keystrokes while a git command runs.** `Progress.Show` calls `UI.StopInput`, which
      is `Application.RootKeyEvent = _ => true` — keys are swallowed, not queued. A key sent into a
      moving screen is silently lost, which is why `WaitFor` requires three identical captures
      before returning and why nothing here ever sleeps a fixed time. Deliberately no resend on
      timeout: it would mask this and could double-apply a command.
- [x] Confirmed working, so the risk written down for it did not materialize: **modified keys
      survive the pty** — `S-Right` reaches `RepoViewInput`'s `Key.CursorRight | Key.ShiftMask` and
      opens the Open Branch menu.
- [ ] **Noted, no action: tmux cannot report gmd's exit code.** `#{pane_dead_status}` is empty for a
      binary tmux exec'd directly, and only filled in when the pane command went through a shell.
      gmd does exit 0 (checked with `sh -c "gmd …; echo $?"`, which reports 0 and makes tmux report
      0 too), but adding that shell only to read the code would put a wrapper process between tmux
      and gmd, which `CLAUDE.md` warns against. The tests assert that gmd terminated; a crash shows
      up as a `WaitFor` timing out with the screen and the log tail in the message, which is better
      evidence anyway.
- [x] **Fixed, and it was pre-existing rather than introduced here: `./test` truncated the
      developer's `~/gmd.log`.** Any test that runs a git command goes through `gmd.Utils.Cmd`,
      which logs, and `ConfigLogger`'s static constructor truncates the log the first time anything
      in the process logs at all. So `./test` wiped the log that `./log` exists to read — worst
      exactly when a developer is reading it, i.e. while chasing a bug. Measured before the fix:
      the 16 pre-existing integration tests alone rewrote it.

      Fixed by giving the *test process* a throwaway `$HOME` too, in `gmdTest/TestSetup.cs`
      (`[AssemblyInitialize]`, reusing `TempHome`) — the same move `TmuxSession` already makes for
      the gmd it starts, so both halves of the suite are now isolated the same way. It covers
      `~/.gmdconfig` and `~/.gmdstate*` as well, should a test ever reach the services that write
      those. No product code changed.

      Two things that had to be got right, both found by probing rather than assumed:
      - **The folder must exist before `HOME` is set.** `GetFolderPath(UserProfile)` returns an
        *empty string* for a `HOME` that does not exist, and `Path.Join` then yields the relative
        `gmd.log` — which would quietly land in the working directory instead. It does honor a
        `HOME` changed at runtime, i.e. the value is not cached, which is what makes this work.
      - **It has to run before anything logs**, since `ConfigLogger` resolves the path once in its
        static constructor. `[AssemblyInitialize]` is early enough; nothing in the suite logs from
        a static initializer.

      Verified: 459 tests pass, `~/gmd.log` byte-identical across a full run (checked with a marker
      line and an md5), the log demonstrably written into `/tmp/gmdTest-home-*/gmd.log` instead, no
      stray `gmd.log` in the working directory, temp homes cleaned up, and
      `--collect:"XPlat Code Coverage"` still writes a cobertura report.

      Unix only, and deliberately so: on Windows the user profile folder does not come from `HOME`,
      so a test run there still truncates the log. Not worth product code to fix — Linux/macOS are
      the development platforms, and Windows matters as a *release* target that is verified by
      running the app, not by running this suite. What a rare Windows session gets is a suite that
      still passes: the fast and integration tests run normally, and the terminal tests report
      `Inconclusive` (tmux being Unix only) rather than failing.
- [x] **Colors are covered now**, closing the half of this that was worth doing.
      `ScreenText.ColorsOf` / `ColorRows` / `BackgroundRows` parse the SGR codes out of an escaped
      capture and give one letter per cell, lined up under the text exactly as
      `GraphText.ColorsOf` does for the graph column — uppercase for a normal color, lowercase for
      its bright variant. Two tests, `TestLogViewColors` and `TestCurrentRowIsHighlighted`.

      What that buys, beyond "colors are drawn":
      - **The branch palette reaches the screen.** Main is magenta by special case, so the test
        also shows `dev` and pins it as green, i.e. the SHA256-into-five-colors path in
        `BranchColorService` that main never exercises. Which color a name lands on is a promise
        to the user — it is why a branch keeps its color between runs.
      - **The dark `╮`/`╯` of a hidden branch is drawn dark.** `GraphTest` asserts that on
        `GraphWriter`'s output; this asserts it on the screen, which is the gap Step 3's `Φ` bug
        fell through.
      - **The current row's highlight is a background**, so it was invisible to every other
        assertion in the suite. `BackgroundRows` shows it starts after the graph column, i.e.
        `RepoWriter` highlights the non-graph part of the row only.
      - The cyan sid, the dark author and time, the green tag, and that a subject is white only on
        the branch the cursor row is on (`RepoWriter.GetSubjectText`).

      Suite 453 → 455. It survives the 2.x port like the rest of this tier: the letters come from
      what the terminal was sent, so no Terminal.Gui type is named.
- [x] **Fixed: `Q` did not quit, although the help guide says it does.** The log view bound only
      lower case `q` (`RepoViewInput.cs`), so the upper case `Q` that `gmd/doc/help.md:14`
      documents as "Esc / Q  Exit application in log view" did nothing at all. Keys are looked up
      by exact value in `ContentView.ProcessHotKey`, and nothing folds case — deliberately, since
      `p`/`P` and `u`/`U` are different commands (push and pull, current versus all). So the fix
      is to register both cases of this one key, in the log view and in the diff view.

      **Correcting what this finding first claimed**, which was half wrong and is the more
      interesting half: it said `q` does nothing in the diff view, since that view binds only
      `Key.Q`. Driving it proved the opposite — `q` closes the diff, and does so *by accident*.
      It is unbound there, so it falls through to the next toplevel in the chain, i.e. the log
      view's quit handler, and that handler is `UI.Shutdown()` → `Application.RequestStop()`,
      which stops the **topmost** toplevel. In the log view that is the application; with a diff
      open it is the diff. Right outcome, wrong reason, and it would have broken silently the day
      the diff view became modal or the log view's handler stopped being `RequestStop`. Now
      registered explicitly, so it is intended rather than emergent.

      Also checked and found *not* broken, since the same reasoning predicted it would be: that
      `p` might reach the `P` handler and push every branch instead of the current one. Driven
      against a repo with two branches ahead of a local bare origin, `p` ran exactly
      `git push --porcelain origin --set-upstream refs/heads/main:refs/heads/main` and left `dev`
      alone. Nothing folds case, as the code says.

      `help.md` needed no edit — it already documented the behavior this makes true. Four test
      cases in `TerminalTest`, written failing first: `TestQuitWithUpperCaseQ` (which timed out
      before the fix), `TestDiffViewClosesWithQ` for both cases, and
      `TestTypingQuitKeysIntoADialogDoesNotQuit`, which pins the risk registering a second quit
      key introduces — a dialog above the log view has to swallow the key rather than let a
      typed 'q' quit gmd. Suite 455 → 459.

      This is the first product change this step has made, and it is the payoff the step was
      argued for: a key that silently did nothing, in the one place a user is told to look.
- [x] **The commit flow is covered, and a commit gmd makes is as deterministic as a fixture one.**
      The first tests here that change the repository rather than only look at it. Six of them
      (suite 459 → 465): the uncommitted row, the dialog and the commit it writes, a message with a
      body, the dialog's Ctrl-D diff, cancelling, and the empty-message validation.

      The problem worth writing down is determinism, since it is the thing that would have made
      this tier flaky. A commit gmd creates is dated *now*, so its sid and its row in the time
      column change every run — exactly what `E2eRepo` pins for every other commit on screen.
      Masking both would work and would throw away the assertion. Instead the *session* pins them:
      `TmuxSession.StartGmd(repo, commitTime: …)` puts `GIT_AUTHOR_DATE` / `GIT_COMMITTER_DATE`
      into the environment gmd runs under, and `Cmd` starts git with `UseShellExecute = false`
      without touching `Environment`, so the git that `CommitService` shells out to inherits them.
      The commit the test makes then has a fixed sha, and `2d0391 … 24-10-15 12:07` is asserted
      like any fixture row. It is opt-in per test rather than always on, for the same reason
      `E2eRepo`'s times increase: two commits pinned to one second would have nothing to order them
      by under `git log --all --date-order`.

      Consequences and the small things found on the way:
      - **The uncommitted row now has coverage at all**, which it had at no tier before —
        `Repo.UncommittedId`'s sentinel row, the `©2` change count in the application bar, and the
        current branch marker moving up onto it. Its own time really is `DateTime.Now`, so this is
        what `ScreenText.MaskTimes` finally exists for. It was made row-targeted
        (`MaskTimes(screen, "uncommitted")`) rather than whole-screen: the commit rows on the same
        screen do have pinned times and are worth asserting.
      - `E2eRepo.CreateWithChangesAsync` deliberately leaves **both** a modified tracked file and
        an untracked one, because gmd commits by running `git add .` and then `git commit -am`, and
        only the add picks the untracked one up. `git show --name-only` asserts both landed.
      - **Escape from the dialog's Ctrl-D diff lands back on the commit dialog**, not on the log
        view — a modal over a modal over the log view, drawn and unwound correctly. Cheap to assert
        and precisely the sort of thing the 2.x port rewrites.
      - **No product bug this time**, unlike the `Q` finding. Every branch driven behaved as the
        code says it should.
      - **Amend (`a`) is a no-op against this fixture**, and correctly so: `Commit` returns early
        unless `CurrentCommit().IsAhead`, which needs a remote to be ahead of, and `E2eRepo` has
        none on purpose. Covering amend therefore needs a fixture with an origin — done since, see
        below, and it was that fixture which turned up the `--prune-tags` bug.
- [x] **The rest of the repo-mutating keys are covered: `b`, `t`, `s` and `e`.** Six more tests
      (suite 465 → 471, tier ~50 s): create a branch from a commit and from a hoovered branch, add
      a tag, switch, merge, and the merge-from menu. No product code changed, and nothing was found
      broken — every path behaved as the code says, which is the outcome a characterization suite
      wants and is not the same as having learned nothing.

      What driving them taught, which is the part worth keeping:
      - **The hoover is what these keys act on, and it is not what the application bar shows.**
        `applicationBarView.SetBranch` is called from two places: by the hoover when it moves
        (`RepoViewInput.cs:508`) and by the *current row's* branch when the row changes
        (`RepoView.OnCurrentIndexChange`). So an operation that moves the row leaves the bar naming
        one branch while the hoover is on another. The only reliable readout of the hoover from
        outside is the branch menu, whose title is `Branch: <name>` — which is how these tests were
        written, and how the next one should be.
      - **After `Enter` shows a branch, the hoover stays on the branch it was on, not the branch
        that appeared.** So the natural "open dev, press `s`" does nothing at all: the hoover is
        still `main`, and `OnKeyS`'s `PrimaryName != currentName` guard drops it. That is what the
        code says should happen and it is not a bug, but it looks exactly like a swallowed
        keystroke, so `TestSwitchToBranch` pins both halves — the no-op, and that one `Right` later
        the same key switches. This cost most of the time this step took, since the symptom (`s`
        does nothing) has three plausible causes and only the menu probe distinguishes them.
      - **`e` does not merge and commit; it merges and then opens the commit dialog.**
        `MergeBranch` ends in `RefreshAndCommit(…, commits)`, so the working tree is left with an
        uncommitted merge and the dialog offers `Merge branch 'main' into dev` as the message. The
        merge is therefore one keystroke away, but the commit is not — which is the opposite of
        what the "no confirmation dialog" note in the plan assumed, and the reason the test needs
        `commitTime:` like the commit tests do.
      - **Create branch publishes by default**, and against a repo with no origin the push fails
        and the failure is deliberately swallowed (`BranchCreateCommands` matches on `'origin' does
        not appear to be a git repository`). The dialog snapshot keeps both check boxes visible, so
        a change to either default shows up as a failing test.
      - **The merge-from menu lists only shown branches**, so with just `main` shown it draws an
        empty menu box rather than saying there is nothing to merge. `TestMergeFromMenu` therefore
        shows `dev` first. Worth a look some day; it is a UI nicety, not a defect.
- [x] **Amend (`a`) is covered, and the fixture with an origin found a real bug** (suite 471 → 473).
      Two tests: amending the message of an unpushed commit, and that the key is refused once the
      commit is on the remote. `E2eRepo.CreateWithOriginAsync` is the fixture — the standard shape,
      a bare origin next door, everything pushed except one commit on top, which is what
      `CommitCommands.Commit`'s `CurrentCommit().IsAhead` guard needs to have anything to offer.

      It is also the only fixture here with a remote, so these two tests are the only place the
      ahead marker (`▲1` in the application bar, `▲` on the commit), the `(^/main)` remote tip and
      the local branch drawn beside its remote reach a snapshot at all.

      - **Amend stays deterministic**, but not the way the commit tests do: git keeps the original
        *author* date when amending and only rewrites the committer date, so `GIT_AUTHOR_DATE` from
        `commitTime:` is ignored and `GIT_COMMITTER_DATE` is not. Both are pinned either way, so
        the amended sha is stable — and the time column does not move, since it shows the author
        date. The test asserts that pair explicitly, because it is the kind of thing a future git
        version could change.
- [ ] **Bug found, not fixed — opening a repo deletes local tags that are not on the remote.**
      `RemoteService.FetchAsync` (`RemoteService.cs:31`) runs
      `git fetch --force --prune --tags --prune-tags origin`, and `--prune-tags` means exactly
      "delete local tags the remote does not have". Git's own documentation warns about this
      option: *"it will remove tags that were created locally"*. `RepoView` fetches when a repo is
      opened, every five minutes after that, and on `r`/`F5`, with no way to turn it off.

      Driven and isolated rather than reasoned about: a fixture with `v1.0` on a commit, a bare
      origin the tag was never pushed to, then start gmd and press nothing. `git tag` lists `v1.0`
      before and lists nothing after startup. The control — the same fixture with the tag pushed —
      keeps it. So this is not about amend, or about any command; **it is the act of opening the
      repository**, and the tag is gone with no message and no undo.

      The one-line fix is to drop `--prune-tags` (keeping `--prune`, which only prunes remote
      tracking branches, and `--tags`, which fetches the remote's tags). The cost of dropping it is
      that a tag deleted on the server lingers locally until someone removes it by hand, which is
      what plain `git fetch` does and is a great deal better than silently deleting a local tag.

      **Deliberately deferred** (2026-08-04): the maintainer wants to see whether this bites in
      practice before changing fetch semantics for everyone, since `--prune-tags` is a flag someone
      typed on purpose rather than an oversight. What it looks like when it does bite: a tag added
      locally and not yet pushed disappears — on opening the repo, on `r`/`F5`, or within five
      minutes of sitting in the log view — with no message. Everything needed to act on it is
      above: the cause, the one-line change, and the fixture shape that reproduces it
      (`E2eRepo.CreateWithOriginAsync` minus its `push origin v1.0`). A regression test belongs
      with the fix.

      Consequence already taken: `E2eRepo.CreateWithOriginAsync` pushes `v1.0`, since a local only
      tag would otherwise vanish from that fixture at whatever moment the first fetch completed —
      i.e. this bug would have shown up as a flaky snapshot rather than as a finding, had the
      screens been written without noticing it.

### Determinism, which is where this will actually go wrong

A captured screen is full of content that changes every run. All of it was visible in the first
captures taken: commit sha (`88453f`), author/commit dates (`26-08-03 02:47`), the temp repo path in
the application bar, and right-edge padding that moves with pane width. Design for it up front
rather than fighting flakes later:

- [x] Fix the pane size explicitly (`tmux new-session -x 120 -y 40`), so wrapping and the app bar's
      space filler are stable. The size is also asserted right after start, since a detached
      session's size interacts with `window-size`/`default-size` and has moved between tmux
      versions — one clear failure beats every snapshot failing at once.
- [x] Normalize before comparing — `ScreenText`, next to `GraphText` as predicted. It turned out to
      need *less* than the step assumed: with the dates and identity pinned the shas and times are
      deterministic and get asserted rather than masked, so only the repo path is replaced, plus a
      per-line right trim and dropping the trailing blank rows.
- [x] Never sleep a fixed time; `WaitFor` polls for the text **and** for three identical captures in
      a row. The stability half was not in the plan and turned out to be required, see the swallowed
      keystrokes finding above.
- [x] Reuse `TempRepo` for the repository — via `E2eRepo`, and one fresh repo per test, since
      `.git/.gmdconfig` is rewritten on every repo show and is the one piece of state a redirected
      `HOME` cannot isolate.
- [x] `[TestCategory("Integration")]` on the lot, plus a second `E2e` category so the slow tier can
      be excluded on its own. tmux's absence fails with "run ./installtools" rather than a timeout;
      on Windows it is `Assert.Inconclusive`, that being the one case where skipping is right.

### First moves

- [x] One tmux test end to end. It is `TestStartupShowsTheLogView`, and the diff went into its own
      test (`TestDiffOfACommit`) since the two assert different screens.
- [x] Then the paths that are pure UI and have no other coverage at all: the menus, the branch
      show/hide keys and scrolling a long log are covered. The commit dialog is not — it mutates the
      repo, which this step deliberately stayed out of; it is the obvious next test.
- [x] **Dropped: the `FakeDriver` test. The tmux tier did its job, and better.** It was here to
      make the `GraphText` snapshots weigh more, since they assert `GraphWriter`'s output rather
      than what reaches the screen. That gap is now closed from the other end: the tmux tests
      assert the same graph runes *as drawn by the real binary in a real terminal*, and since the
      colors went in they assert the runes' colors too, including the dark hidden-branch marker
      that was the specific thing `GraphText.ColorsOf` covered at writer level.

      So a `FakeDriver` test would be a third rendering of the same information, sitting between
      two tiers that already agree — and the least durable of the three: it names `FakeDriver`,
      `Application.Init` and `Contents`, all of which 2.x removes, so it would be written now and
      deleted at the port. This document already says as much about the tier ("worth having for
      fast feedback during v1's remaining life, but not port insurance"); what changed is that its
      one concrete job is done, so the fast feedback alone does not pay for it. `GraphText` still
      covers the writer in milliseconds.

      Reversible if a reason turns up — the finding in Step 3 that `FakeDriver` works headlessly
      stands, and the note on `FakeMainLoop` being `internal` with it. The likeliest such reason
      is a drawing bug in a view that is genuinely hard to reach through the running app.
- [x] **CI runs the tmux tier in the existing jobs.** No workflow change was needed to make it run —
      both jobs already run `dotnet test` unfiltered through `./build` — so the only addition is one
      `tmux -V || apt-get install` step per job, which is free when the image already has it and
      self-healing if a future image drops it. A separate job was rejected: it would need its own
      checkout, SDK setup and build to get the binary, roughly doubling CI time to parallelize
      fourteen seconds, and it would let the release job publish without this tier having passed.

### Next, when this is picked up again

- [x] The repo-mutating flows, one throwaway repo each: commit (`c`), create branch (`b`), tag
      (`t`), switch (`s`) and merge (`e`) — **all done**, see the two findings above. Commit built
      the machinery the rest needed (a fixture with changes, and pinned dates for a commit gmd
      makes itself); the other four turned out to be mostly a lesson in the hoover.
- [x] Amend (`a`) with an origin fixture — done, see the finding above, which also turned up the
      `--prune-tags` bug.
- [ ] The `--prune-tags` finding above, parked on purpose until it is noticed in real use. Not a
      question to re-open unprompted — pick it up when a local tag actually goes missing.
- [x] Colors, via `TmuxSession.CaptureColors()` — done, see the finding above.
- [x] **The middle column widths — which turned out to be two arms, not one.**
      `RepoWriter.ColumnWidths` is a four way ladder, and the note this item used to carry ("the
      middle arm, `commitWidth` 70–109") described the two middle ones as if they were a single
      case. They differ by exactly one column, and it is the identifying one:

      | `commitWidth` | sid | author | time | |
      | --- | --- | --- | --- | --- |
      | < 70 | — | — | — | `TestNarrowWidthDropsTheSidAuthorAndTimeColumns`, pane 70 |
      | 70–99 | — | 10 | 9 | `TestMediumWidthDropsTheSidAndCutsTheTimeToADate`, pane 95 |
      | 100–109 | 7 | 10 | 9 | `TestNearlyFullWidthKeepsTheSidButStillCutsTheTime`, pane 112 |
      | ≥ 110 | 7 | 15 | 15 | every other test here, pane 120 |

      Two things came out of writing them, both of which the next person here needs:
      - **A shortened column is not marked as shortened.** `Txt` (`RepoWriter.cs:328`) truncates
        with a plain `text[..width]`, so at `timeWidth` 9 the time `24-10-15 12:06` is drawn as
        `24-10-15`: the clock is gone and the column looks like it was meant to be a date. The rest
        of the UI marks a truncation with `┅`, which `gmd/doc/help.md` documents as meaning exactly
        that. Not changed here — these are characterization tests — but it is the sort of thing
        this tier exists to make visible, and it is now pinned in two snapshots.
      - **The arm is chosen by `commitWidth`, not by the pane width**, and
        `commitWidth = width + 1 - (graphWidth + 3)`. So the same pane can sit in different arms
        for different repos, and *showing a branch can push a row down an arm* by widening the
        graph. The two widths above were measured against `E2eRepo` with `dev` hidden (graph 6
        columns, so the arms start at panes 78, 108 and 118) rather than calculated — a new width
        test has to measure too.

      Still uncovered, and cheap if it is ever wanted: the author column being *visibly* cut. At
      `authorWidth` 10 the fixture's ` Test User` is exactly 10 columns, so it fits perfectly and
      the narrowing cannot be seen in that column at all. It needs a fixture whose author name is
      longer than nine characters, which means new dates and new ids for its commits.
- [ ] Mouse interaction, which `send-keys` cannot express — it needs raw SGR sequences
      (`send-keys -H`) and exact coordinates.

## Step 13 — Merge the current branch *into* another branch ✅ done

**Reported by the user: merging repeatedly in one direction is awkward.** Gmd could only merge
*into* the current branch, so a recurring `dev → main` meant switching to `main`, picking `dev`
under **Merge from**, committing, and switching back — four steps for one action.

The first question asked was whether the mechanism behind 'Pull/Update all branches' and 'Push all
branches' could be reused, since those already operate on branches that are not checked out.
**It cannot, and the reason is worth writing down so it is not re-asked:** that mechanism is
`git fetch origin <name>:<name>` (`RemoteService.PullBranchAsync`) with no `+` and no `--force`,
so git only permits a **fast-forward** of the local ref. Gmd merges with `--no-ff`
(`BranchService.MergeBranchAsync`), so after the very first `dev → main` merge, `main` carries a
merge commit that is not on `dev`, and every later merge in that direction is a non-fast-forward.
Git has no porcelain that merges into a branch without a working folder. (The plumbing route —
`git merge-tree --write-tree` + `commit-tree` + `update-ref` — does exist, but needs git ≥ 2.38,
which gmd does not gate on, bypasses hooks, and leaves a conflict nowhere to be resolved.)

So the command automates the four steps instead: `BranchWriteService.MergeToBranchAsync` checks
out the target and merges, then `BranchCommands.MergeToBranch` refreshes, opens the normal commit
dialog on the target, and switches back once the merge is committed.

### Findings

- [x] **A refresh that does not name the branch HEAD just moved to can drop it from the view.**
      `ShowRefreshedRepoAsync` passes the *previous* `ViewBranches` as the branches to show, and
      `ViewRepoCreater.FilterOutViewBranches` only forces the current branch in when no branches
      were specified at all — otherwise just main and the detached one. So refreshing after the
      checkout, without passing the target, can leave the new current branch and its uncommitted
      row out of `ViewCommits` entirely, and `CommitDlg` reads `ViewCommits[0]` for the branch it
      names. Every refresh in this flow passes the branch HEAD is on.
- [x] **`R<bool>` is a trap with this `Result` type, and was avoided.** `R<T>` defines *both*
      `implicit operator bool(R<T>) => r.IsOk` and `implicit operator R<T>(T value)`, so for
      `T = bool` they compile in opposite directions: `bool b = result;` silently yields `IsOk`
      rather than the value. `CommitAsync` therefore returns a three-valued `CommitResult` enum,
      which is what the caller actually needs anyway — `NothingToCommit` (the target already had
      everything, so switch back) and `Cancelled` (the merge is still staged, so git cannot check
      out over it and the user stays on the target) are different outcomes.
- [x] **`SwitchToAsync` is not a plain checkout — it recreates a branch git no longer has**
      (`BranchWriteService.SwitchToAsync`), so 'Merge to' a `~deleted` branch would resurrect it.
      Merging *from* a deleted branch is fine, merging *to* one is not, so the 'Merge to' list and
      the `Shift-E` key both require `IsGitBranch`. This is the one place where the two directions
      do *not* share a candidate list.
- [x] **Fixed: `ToMergeCommits` could throw a `KeyNotFoundException` past all the `R` handling.**
      It looked up every commit of the merge log in the shown repo, which is `git log --all
      --max-count=30000` and can be truncated, while the merge log itself is `HEAD..<source>`.
      Pre-existing in `MergeBranchAsync`, but 'merge to' makes `HEAD..<source>` reach further back,
      so the exposure grew. Missing commits are now skipped rather than looked up blindly.
- [x] **The menu wording had to be settled before the item could be added**, because
      `Merge to {current}` already existed on a *non-current* branch's menu and means the opposite
      of the new feature. Both directions are now worded from the branch the menu is for:
      **`Merge to X`** merges the menu's branch into X, **`Merge from X`** merges X into it. That
      holds for the two submenus on the current branch and the two items on every other branch, so
      the key mirrors the menu everywhere: `e` merges into the current branch, `E` merges the
      current branch out.
- [x] The keys hold progress — and therefore hold input off — across checkout, merge, refresh, a
      modal dialog, the commit, the checkout back and a second refresh. That is by far the longest
      input-dead window of any command. It is safe only because the outer `Do`'s `progress.Show()`
      outlives the dialogs: `UI.EnableInput` *captures and restores* `RootKeyEvent`, so if progress
      ever reached zero while a dialog were open, the restore would put back `_ => true` and input
      would die for good. Do not move the dialog out of the `Do` action.
- [x] `await repoView.RefreshAsync(...)` also closes a pre-existing window: `Refresh()` is
      fire-and-forget, so the outer progress could previously drop to zero while a refresh was
      still running.

### Verified

All three outcomes were driven by hand in a throwaway repo, since only the happy path is reachable
from the E2E suite:

- Happy path — `TerminalTest.TestMergeToBranch`, plus the menu arm in `TestMergeToMenu`.
- Nothing to merge — `TerminalTest.TestMergeToBranchThatIsAlreadyUpToDate` and
  `AugmentedServiceIntegrationTest.TestMergeToBranchThatIsAlreadyUpToDate`.
- Commit cancelled — by hand: HEAD stays on the target, the merge stays staged, the message says
  so, and input is still live afterwards.
- Conflict — by hand: HEAD stays on the target, the conflict row is drawn, and the error names the
  branch HEAD ended up on ("Failed to merge 'dev' while on 'main'"), which is the whole point of
  reporting the checkout and the merge as separate failures.

## Step 14 — Blame a file ✅ done

`Full File History ...` answers "how did this file change". The other half — "who last touched
*this line*, and why" — meant leaving gmd for a console. Console `git blame` is also hard to read:
it repeats the sha, author and date on every line, so a 40-line block from one commit costs 40
identical prefixes and the eye has nothing to latch onto.

**`Blame File ...`**, next to `Full File History ...` in the commit menu, reusing the same
`FileBrowseDlg` picker. The new view aggregates *runs* — consecutive lines from the same commit —
into one bracket in the gutter and names the commit once per run:

```
┌ c6d2d7 Test User   25-11-02 │   1┃// Blames a file
│                             │   2┃
└                             │   3┃interface IBlameService
╺ 215c93 Test User   24-07-19 │   4┃    Task<R<Blame>> BlameAsync(
```

- [x] `gmd/Git/Private/BlameService.cs` — `git blame --porcelain`, parsed into
      `Blame`/`BlameLine`/`BlameCommit` (`IGit.cs`). The porcelain block is emitted only on the
      first sighting of a sha, so the parser keeps a dictionary and the lines reference it by id —
      which is also the shape the view wants.
- [x] `gmd/Server/` — mirrored records plus `ViewRepoConverter.ToBlame`, the `CommitDiff`
      precedent, so nothing under `Cui/` names a `gmd.Git` record.
- [x] `gmd/Cui/Blame/` — `BlameRows` (row model), `BlameColumns` (pure column math),
      `BlameService` (run classification, age heat, gutter text) and `BlameView` (the Toplevel).
      Everything except the view is pure, so the gutter is snapshot-asserted as ASCII art through
      `Text.ToString()` with no driver, the way `GraphText` does for the graph.
- [x] Drill-down: `P` re-blames at the porcelain `previous` sha *and path*, so a rename is followed
      for free where `<sha>^` plus the current path would silently get it wrong. A stack inside the
      one `Show` call rather than nested views, so `Esc` still means "close" and `Backspace` means
      "one hop back".
- [x] Commit details: `Enter` toggles the log view's own `CommitDetailsView` in a pane at the
      bottom, following the cursor. The blame knows only the *first* line of the message
      (porcelain `summary`) and nothing about branches, so the full body and the gmd-inferred
      branch come from `Repo.CommitById` — the view takes a `Server.Repo` rather than just its
      path for this. `ICommitDetailsView` grew a `SetRows` overload so a caller with no
      `Server.Commit` can render its own rows, which is the fallback below.

### Findings

- **A missing `blame.ignoreRevsFile` is fatal, not ignored.** `git blame` exits with
  `fatal: could not open object name list: <file>`. This repo's own `installtools:31` sets that
  config, and `.git-blame-ignore-revs` was deleted in `a81fbda` — it survives only as an untracked
  file in existing clones, so **a fresh clone of gmd cannot blame anything**. The service honors
  the config (that is what `git blame` and the hosting sites do) but retries once with
  `-c blame.ignoreRevsFile=` when it hits exactly that error. `installtools:31` is now dead either
  way and should be dropped or the file restored — left alone here deliberately.
- **`Cmd.Command` `TrimEnd()`s the whole output** (`Utils/Cmd.cs:133`), so a file whose last line is
  empty loses its `\t` content marker entirely and the output ends on a bare header. Only the last
  line can ever be affected, since every other content line is followed by a header. The parser
  treats "header at EOF with no content line" as an empty line rather than an error; there is a
  regression test for it. Its `Replace("\r", "")` is welcome — CRLF files render correctly.
- **Tabs are expanded in the Cui layer, not the git layer**, so `Ctrl-C` yields the file's own text.
  `DiffView.OnCopy` has to guess where the line-number prefix ends; blame rows map 1:1 onto
  `blame.Lines`, so the copy slices those directly.
- **The uncommitted sha is the all-`0` `Repo.UncommittedId`.** `git blame` uses the same value, so
  a blamed line's commit id can be handed straight to `IServer.GetCommitDiffAsync`, which already
  routes that sentinel to the uncommitted diff — `D` works on uncommitted lines with no special
  case.
- **`IBranchColorService` was investigated for the gutter and rejected.** `GetColor` returns
  magenta for any main branch, and in a normal repo most commits are on main, so nearly every run
  would be the same color — the exact adjacency collapse the coloring is there to prevent. It also
  has no answer for a sha outside the loaded log. Age heat by *rank* over the distinct commits is
  used instead: rank rather than absolute time means the ramp is fully used whether the file is a
  week or a decade old, and recent edits stay apart in a file dominated by one ancient bulk commit.
  Annotating `BlameCommit` with the gmd branch in the Server layer, and *tinting* rather than
  replacing, is where the branch-color idea would actually pay off.
- **`UILabel.Text` sizes the label from the text it is replacing** (`UILabel.cs:40-48` sets
  `Width = text.Length` before assigning), so a header that grows is clipped to the previous
  header's length. `BlameView.SetHeader` sets `Width = Dim.Fill()` after every assignment. The
  setter itself is worth fixing, but not from here.
- **`ToggleDetailsFocus` does not move the keyboard, only the drawn highlight.** `RepoView`'s
  version says so in a comment ("unfortunately SetFocus() does not seem to work"), and the
  consequence is easy to miss: `ContentView.ProcessHotKey` returns early on `!HasFocus`, so the
  pane never receives a key no matter what `IsFocus` says. Tab in the blame view therefore *looked*
  like it worked — the border went heavy — while `Down` still moved the blame cursor underneath.
  Fixed by forwarding the scroll keys by hand from the view that really has the keyboard, which is
  what `FilterDlg` already does for the log view's results (`FilterDlg.cs:86-129`). Worth knowing
  that the log view's own Tab has the same limitation.
- **`Server.Commit` and `Server.Branch` are ~30 fields each**, so synthesizing one for a commit the
  log does not have was rejected as fragile. The blame view falls back to
  `BlameService.ToDetailsRows`, which renders what git blame itself said and names what is missing
  rather than leaving blanks the reader has to interpret. Only the log is capped (30 000 commits),
  never the blame, so this is reachable in a very large repo.
- **Adding one commit-menu item broke three golden screens** (`TerminalTest` `TestCommitMenu` and
  both windows of `TestBranchesSubMenuInCommitMenu`), exactly as the menu section of `CLAUDE.md`
  warns. The menu grows one row taller, so the `ScreenText.Rows` windows shift by one.

### Verified

`./test` is green (490 fast + 45 E2E). Beyond the suite, driven by hand in a throwaway repo with a
redirected `HOME`: the run brackets and the `╺` single-line stub; the age ramp in real ANSI
(yellow → bright cyan → dark, bright yellow for uncommitted); `I` through all four detail levels;
the automatic step-down on a narrow view; `←`/`→` scrolling the code with the gutter pinned and `…`
at both cut ends; `P` twice down to the root commit and the refusal there; `Backspace` back out;
`D` opening the diff over the blame view and input still live after `Esc` (the Step 13 check);
`Ctrl-C` putting the file's own text on the clipboard through OSC 52; and `q` closing the view
rather than gmd. For the details pane: `Enter` opening it, the pane following the cursor through
three commits including the uncommitted one, `Tab` into it and `PageDown` reaching the last line of
a 14-line message while the blame cursor stayed put, `Tab` back restoring cursor movement, and
`Enter` closing it.

### Not done, deliberately

`-w` (ignore whitespace) and `-M` / `-C` (detect moved lines) as menu toggles that re-run the
blame. They change the answer, so gmd would disagree with `git blame` at the CLI, and `-C` is slow
— they are per-question and belong on the menu, but they are not part of this cut.

## Step 15 — Adjustable diff context ✅ done

**Reported by the user: the diff shows six lines around each change and there is no way to see
more.** `--unified=6` was a literal in five separate command strings in `Git/Private/DiffService.cs`,
so when six lines was not enough the only way on was to leave gmd for a console.

**`+` and `-` in the diff view**, stepping the file the cursor is on through 6 → 15 → the whole
file and back (`=` is an alias for `+`, which needs a shift on most layouts). It is per file: the
rest of the commit stays as it was, and a file that is not at the default says so in its header —

```
Modified: long.txt  (context 15)
```

- [x] `Cui/Diff/DiffContext.cs` — the levels, the default, and `WholeFile`. Every caller of the
      server diff methods is in `Cui/`, so the numbers live in one layer and the two below just
      pass on what they are given: `int contextLines` before the trailing `wd` in the six diff
      methods of `IDiffService`/`IGit`/`IServer`. The parser and `ViewRepoConverter` needed no
      change at all — a larger `-U` only means more `DiffSame` lines and fewer sections.
- [x] `Cui/Diff/DiffReload.cs` — a `DiffReload` delegate captured when the view opens, replacing
      the hardcoded `GetCommitDiffAsync(commitId, …)` the refresh used. See the findings.
- [x] `DiffRow` carries the source line numbers it draws, so the cursor can be put back on the line
      it was on after the rows are rebuilt, and `OnCopy` can slice the gutter off by its real width
      instead of guessing.

### Findings

- [x] **A pathspec would have been cheaper and is wrong.** Asking git for one file
      (`git show <sha> --unified=15 -- <path>`) is the obvious way to make a per-file feature cheap,
      but `--find-renames` runs on the pathspec-filtered set — which is the whole reason
      `git log --follow` exists — so a renamed file comes back as *added* with its history lost.
      The diff is therefore re-fetched whole and the one file spliced into the one on screen
      (`DiffService.ReplaceFileDiff`). That costs exactly what `r` already cost. If it ever needs
      to be cheap, the pathspec has to name **both** sides (`-- <before> <after>`).
- [x] **Refresh was fetching the wrong thing in four of the six views.** `RefreshDiff` always called
      `server.GetCommitDiffAsync(commitId, …)` however the diff had been opened, and `commitId` is a
      *stash name* for a stash diff, the wrong end of a range for a range diff, a branch tip for a
      preview merge, and `""` for full file history — where it would have run `git show` with no
      rev. Pre-existing, and invisible because `r`/`d` are rarely pressed there. The `DiffReload`
      delegate fixes all four, which is also what made `+`/`-` work everywhere rather than only on
      a commit. The menu's `Refresh` item was gated on the diff being the uncommitted one while the
      `r`/`d` *keys* were not; the gate is gone now that refresh is genuinely valid everywhere.
- [x] **`RefreshDiff` dereferenced the diff it had just failed to get.** It reported the error and
      then ran `diffs = [diff!]` regardless — a `NullReferenceException` past all the `R` handling.
- [x] **`SetCurrentIndex` then `ScrollToShowIndex` does not put the cursor where you asked**, and
      that is the order `BlameView.ScrollToCommit` (`BlameView.cs:453`) uses. `ContentScroll.Scroll`
      moves `CurrentIndex` by the same delta as `FirstIndex`, so setting the cursor first has the
      scroll add the delta to it a second time; the clamp then leaves it on the *last visible row*
      rather than the target. Scrolling first and setting the cursor after is correct, and is what
      the diff view does. Blame's "scroll to commit" highlights the wrong row for this reason —
      not fixed here, since it is a different view and a different command.
- [x] **`ContentScroll` never clamps a cursor that the content shrank past.** `SetTotalCount` comes
      from the draw callback, and the only rescue is for the case where *zero* rows come back, which
      jumps to the bottom. Narrowing the context makes this reachable — the cursor simply stops
      being drawn — so both reload paths restore it explicitly rather than leaving the index alone.
      A stale selection has the same shape: it covers rows that no longer hold what they did, and
      `OnCopy` would copy whatever now sits there, so a reload clears it.
- [x] **Two commands were quietly inconsistent about context.** `GetFileDiffAsync` passed no
      `--unified` at all, so full file history rendered at git's default of 3 while everything else
      used 6, and the empty-repo fallback `git diff --staged` passed none either, so an empty repo
      ignored whatever was asked for. Both are told now; the file history change is visible.
- [x] **Git has no "all", but a context larger than the file is not an error** — it stops at the
      ends of the file. `WholeFile` is 100 000, which is beyond any source file worth reading in a
      terminal; a longer file stays truncated. Pinned by a `TempRepo` test, since this is git's own
      behavior and canned output cannot catch it.
- [x] **Terminal.Gui 1.x has no `Key` value for `+`, `-` or `=`**, and the ascii cast used for `?`
      in `RepoViewInput` works for them too — verified by logging `ProcessHotKey`, which reported
      43, 45 and 61. `+ - =` were the only obvious keys still free in the diff view; taken are
      `Esc Q q ← → Ctrl-C m r d s u c`.
- [x] **The line number gutter is wider than 5 once the number does not fit in 4**, which whole
      file context on a large file reaches. `OnCopy` guessed the width with
      `t.Length > 4 && char.IsNumber(t[3]) ? t[5..] : t` and so left a digit on every copied line
      of such a file. Now that the row carries the number, `WithoutLineNbr` computes it.
- [x] **No E2E fixture could show this feature at all.** Every file in `E2eRepo` is one or two lines
      long, so the whole file is already drawn at six lines of context and `+` would change nothing
      on screen. `CreateWithLongFileAsync` is a *new* fixture rather than another commit on
      `CreateAsync`, because changing that one changes the id of that commit and of every commit
      after it, and with it every snapshot in `TerminalTest` that names one.
- [x] `ScreenText.Of` keeps the scroll bar column, so a scrolled screen has trailing spaces and a
      `"Modified: short.txt\n"` substring assertion silently fails. Assert the text without the
      anchor and the *absence* of what should not be there.
- [x] `IStashService.GetDiffAsync` is dead in production — `Git.cs` calls `diffService`
      directly — and is reached only by `StashServiceTest`. Left alone, with the parameter threaded
      through it like the rest.

### Verified

`./test` is green (511 fast + 46 E2E + integration = 580). Beyond the suite, driven by hand in a
throwaway repo with a redirected `HOME`: the full `6 → 15 → whole file → 15 → 6` cycle on a 60 line
file, with the second file of the same commit untouched throughout and its header staying plain;
`+` above the first file header and `-` on a file already at the narrowest, both no-ops that run no
git command; the cursor staying on line 29 across a forced scroll into the whole-file view, and
landing on the nearest line still drawn when narrowing removed the one it was on; `+` inside full
file history, which is one of the views whose refresh was broken; and `Ctrl-C` putting `line 29`
`line 30` `line 31` on the clipboard through OSC 52 with the gutter cleanly removed.

- [x] **The keys needed somewhere to be found.** The diff view has no footer and nothing else
      advertised them, so the menu (`M`) carries the same two commands. They are worded
      `More Context of long.txt (15 lines)` — naming the file, because per-file is the part these
      commands do not give away, and naming what picking it would show rather than what is shown
      now, which is what the header already says. The direction with nowhere to go is disabled, so
      at the default there is no **Less Context** to pick and at the whole file no **More Context**.

### Not done, deliberately

A step *below* 6 (0 or 3, "changes only") for scanning a large commit. Same shape as the rest, and
a one line addition to `DiffContext.Levels` if it is wanted.

## Step 16 — Resolve merge conflicts

**Reported by the user: the diff view can *show* a conflict but there is no way to resolve one.**
That it shows conflicts at all is incidental — `GetUncommittedDiff` skips its `git add .` while
merging and runs `git diff HEAD`, so the markers git wrote into the working-tree file arrive as
ordinary `+` lines, which `ParseLineDiff` recognises. The only way on is `git mergetool`, i.e. out
of gmd. The plan is a three-way resolver (ours / base / theirs with an editable result) in a new
`Cui/Conflict/`, entered from the diff view.

The shaping finding: **that data is fine for showing and useless for writing.** It is a diff, with
diff context, and the ours/theirs pairing in `AddSectionDiff` is a rendering heuristic over `+`
prefixes. Resolution reads the working-tree file itself and writes the whole file back, so it is a
new service rather than an extension of `DiffRows`.

- [x] **Step 1 — operation detection, and the two bugs it fixes.** See below.
- [x] **Step 2 — abort *and continue*.** See below.
- [x] **Step 3 — commit gating, and routing a rebase to *Continue*.** See below.
- [x] **Step 4 — the `ConflictFile`/`FileLine` model and the pure `ConflictParser`.** See below.
- [ ] Step 5 — the resolver view.
- [ ] Step 6 — the base pane, via `checkout-index --stage=all --temp` + `merge-file --diff3`.
- [ ] Step 7 — file-level resolutions (whole-file ours/theirs, `UD`/`DU`/`DD`, binary, un-resolve).
- [ ] Step 8 — manual edit of a conflict region.
- [ ] Step 9 — (optional) true inline editing, gated on a focus probe.
- [ ] Step 10 — `gmd/doc/help.md`.

### Step 1 findings

Everything below was probed against real git (2.55) before it was written; three of the probes
changed the design and two of them found bugs that were already shipping.

- [x] **Opening the diff view during a `git rebase --apply` or `git am` conflict destroyed it.**
      Those backends write `rebase-apply/` and **no `MERGE_MSG`**, which is the only thing
      `IsMergeInProgress` looked at, so `GetUncommittedDiff` ran its `git add .` — staging the
      unmerged path with the conflict markers as its content. That resolves the conflict, and the
      `git reset` afterwards does **not** put the stages back: `git ls-files -u` drops to zero and
      `git checkout --merge` can no longer recover it, only `--abort` can. Silent, unrecoverable,
      and triggered by merely *looking* at the diff. The default rebase backend and cherry-pick do
      write `MERGE_MSG`, so gmd's own rebase was safe — it needed `rebase.backend=apply`, an
      explicit `git rebase --apply`, or `git am`. Pinned by
      `TestDiffDuringAnApplyRebaseKeepsTheConflictUnmerged`.
- [x] **`git commit -am` on a conflicted merge succeeds and commits the markers into history.**
      Verified by reading `git show HEAD:f.txt` back. `-a` is `add -u` semantics, and `git add` on
      an unmerged path *resolves* it with the working-tree content — so `-a` was a second way into
      the same hole as `git add .`, and the old guard only ever covered the first. Both are now
      guarded on `Operation != None`.
- [x] **`MERGE_MSG` never meant "a merge is in progress".** A stopped rebase (merge backend) and a
      stopped cherry-pick both write it. It is now tested for last, after the directories and the
      `*_HEAD` files, which is the order git's own `wt_status_get_state()` uses.
- [x] **An interactive rebase cannot be told from a plain one, so the distinction was dropped.**
      Modern git runs both through the sequencer and writes `rebase-merge/interactive` for both —
      a plain `git rebase main` produces it. A `RebaseInteractive` enum member was written, found
      to be undetectable by its own test, and removed. Nothing needs it: `--continue`, `--skip` and
      `--abort` are the same commands either way.
- [x] **gmd's own cherry-pick reports `Merge`, and that is correct.** `BranchService` runs
      `cherry-pick --no-commit`, which writes no `CHERRY_PICK_HEAD` at all — only `MERGE_MSG` — and
      `git cherry-pick --abort` in that state answers "no cherry-pick or revert in progress". There
      is genuinely no sequence to continue or abort, only a conflicted index to resolve and commit.
      A cherry-pick started outside gmd does record one and is detected.
      Both are pinned, so the day gmd drops `--no-commit` the test says so.
- [x] **`.git` is a *file* in a linked worktree and in a submodule**, holding `gitdir: <path>`, and
      that is where the operation state lives — so `Path.Join(wd, ".git")` found none of it there.
      `GetGitDir` follows the pointer, resolving a relative one (a submodule's) against the working
      folder. Pre-existing; every new probe would have inherited it. No extra git process: reading
      the file is cheaper than `git rev-parse --absolute-git-dir` on every status.
- [x] **Staging nothing made git refuse a commit it used to make.** Resolving in an external editor
      without `git add` worked by accident before, because `-am` staged the edited file. Now git
      answers `error: Committing is not possible because you have unmerged files` with four lines
      of hints, so `CommitAllChangesAsync` recognises that and leads with gmd's own sentence. The
      `git mergetool` path is unaffected — mergetool stages for you.
- [x] **The conflict *kind* was parsed and thrown away.** All seven porcelain codes were recognised
      only to be flattened into a list of paths, so nothing downstream could tell a modify/delete
      (no text to merge, only keep-or-delete) from a both-modified. `Status.Conflicts` is now
      `ConflictedFile(Path, Kind)` and `ConflictsFiles` is derived from it, one source of truth.
- [x] `IsMerging` is kept as the name, now `Operation != GitOperation.None`, so `Status.IsOk`,
      `Uncommitted` and the integration tests were untouched. It is a misnomer — documented at both
      declarations rather than renamed, which would have rippled for no behavioural gain.
- [x] `Augmenter.ToStatus` and `WorkRepoConverter.ToStatus` were byte-identical copies of the same
      field-for-field conversion. Rather than add the new members to both, they are now one
      `StatusConverter`, whose two enum switches fail fast on an unmapped member as
      `ViewRepoConverter` does.
- [x] The `Status` record is declared twice and built in four places, all compiler-enforced — the
      change was mechanical exactly as expected, and `RepoBuilder.WithStatus(isMerging:)` became
      `WithStatus(operation:)` in the two tests that used it.

### Step 1 verified

`./test` is green (561 fast + integration, 606 with E2E). Beyond the suite, driven by hand against
the built binary under tmux with a redirected `HOME`: a normal commit including an untracked file,
which is the path Step 1 changes for *every* user, not just users with conflicts; a conflicted
merge, where the diff view leaves all three index stages intact, an unresolved commit is refused
with gmd's own wording, and a hand-resolved and staged commit goes through with the resolved
content; and a `rebase --apply` conflict — no `MERGE_MSG` on disk — where opening the diff left
`git ls-files -u` reporting all three stages, which is the bug above.

### Step 2 findings

`Git/Private/ConflictService.cs` — `AbortOperationAsync`, `ContinueOperationAsync`,
`SkipOperationAsync` — plus a self-hiding section at the head of the repo menu, wording the
operation as e.g. `Rebase 'dev' (1 of 2)  ·  1 conflict`.

- [x] **gmd could start a rebase and not finish it.** `BranchService` runs `rebase`, `rebase --onto`
      and `cherry-pick`, and on a conflict there was no `--continue` anywhere in the codebase. For a
      rebase "commit" is the wrong verb entirely — `git commit` makes the commit but leaves the
      rebase mid flight with its remaining commits unapplied — so a conflicted rebase started in gmd
      could only be finished by leaving for a console. That, rather than abort, was the real gap.
- [x] **`--continue` opens an editor, which would hang gmd behind the terminal it owns.** `ICmd`
      cannot pass environment variables, so `GIT_EDITOR` is out; `-c core.editor=true` says the same
      thing as config, `true` being a program that exits 0 without writing. Verified for both
      `rebase --continue` and `cherry-pick --continue`.
- [x] **Stopping again is success, not failure.** A rebase over several commits stops on each one
      that conflicts, and `--continue` then exits non-zero with `CONFLICT` in its output — the same
      shape as being refused because nothing was resolved (`needs merge` / `You must edit all merge
      conflicts`). Both are non-zero, so they are told apart by what git printed, the same sniffing
      `BranchService` already does when *starting* one, and each gets its own wording.
- [x] **The Server methods take no operation.** `AbortOperationAsync(wd)` rather than
      `AbortOperationAsync(operation, wd)`: the Git layer probes what is in progress itself, so the
      UI cannot act on a stale operation and the `GitOperation` enum never has to be converted back
      *down* through the layers — only up, which `StatusConverter` already does.
- [x] **Only a rebase and an `am` have a commit to skip, and only a merge has no continue.** Both
      are *omitted* rather than disabled: a permanently greyed `Continue Merge` would suggest that
      continuing a merge is something gmd might one day do. That is the opposite of the diff view's
      `More/Less Context`, which are disabled because they are meaningful in general and merely have
      nowhere to go just now — the distinction is "never applies here" versus "nothing to do yet".
- [x] `git merge --abort` after a conflicted merge, `rebase --abort`, `cherry-pick --abort` and
      `revert --abort` all leave a clean tree at the commit they started from. Pinned per operation,
      since this is exactly the sort of thing a git version can change.

### Step 2 verified

`./test` is green (630, up from 608). By hand against the built binary under tmux with a redirected
`HOME`: a two step rebase stopped on both of its commits, driven entirely from the repo menu —
Continue refused with gmd's wording while unresolved, then advanced 1 of 2 → 2 of 2 and said it had
stopped on more conflicts, then finished, leaving `dev` rebased onto `main` with a clean tree and no
rebase state; and a conflicted merge, whose menu section correctly offers only **Abort Merge**,
confirmed first, leaving the working folder as it was.

### Step 3 findings

`RepoCommands.ConfirmConflictsResolvedAsync`, in front of both of the things that make a commit —
`CommitCommands.CommitAsync` and `ContinueOperation` — plus `OfferContinueInsteadOfCommit`.

- [x] **Marking a file resolved does not mean it is resolved.** Marking resolved is only `git add`,
      and git does not look at what it stages, so a file staged with the markers still in it commits
      `<<<<<<<` into history. Step 1's guards cannot catch this: they stop *gmd* from staging, and
      here the user staged it deliberately — or a merge tool gave up half way. `git diff --cached
      --check` is git's own answer, and it is what the second check runs.
- [x] **Trusting `--check`'s exit code would refuse a commit over a trailing space.** It reports
      whitespace problems as well as conflict markers and exits non-zero for either — verified, a
      file with only trailing whitespace exits 2. So the *lines* are filtered for
      `: leftover conflict marker` and the exit code is ignored. Findings go to stdout; stderr stays
      empty, which is what tells a real failure apart from a finding. Both halves are pinned, the
      whitespace one twice: with `FakeCmd` and against real git.
- [x] **A rebase leaves HEAD detached, so `c` during one used to answer "Cannot commit in detached
      head state. Please create/switch to a branch first."** True, and useless: the way out is to
      continue the rebase, not to make a branch. The routing therefore runs *before* that check.
- [x] **The check is skipped entirely when no operation is in progress**, so an ordinary commit runs
      no extra git command and cannot be blocked by it. Verified by reading the log after a normal
      commit — `diff --cached --check` does not appear.
- [x] The gate is on `IRepoCommands` rather than duplicated, because continuing a rebase *is* how it
      makes its commit and needs exactly the same two checks. `CommitCommands` reaches it through
      `IViewRepo.Cmds`.
- [x] Wording is per action: the same gate says "then commit" or "then continue" depending on which
      called it, and the override button is `Commit Anyway` or `Continue Anyway`. The default button
      on the marker warning is **Cancel**, since committing markers is the thing being prevented.
- [x] `FakeCmd.Problems(output)` was added beside `Ok` and `Fail`: a command that reports problems
      on *stdout* with a non-zero exit had no shape in the fake, and `Fail` puts its text on stderr.

### Step 3 verified

`./test` is green (639, up from 630). One run of the full suite failed a single E2E test which two
later full runs and a standalone E2E run all passed — a timing flake, not tracked down further,
noted here because it happened.

By hand against the built binary under tmux with a redirected `HOME`: a file staged with the markers
still in it, where Commit is stopped by **Conflict Markers Left** naming the file, *Show Diff* opens
the uncommitted diff on it, and *Cancel* is the default; `c` during a rebase offering **Continue
Rebase** rather than the detached head message; Continue then stopped by **Unresolved Conflicts**
worded "then continue"; and after resolving, the rebase finishing from the same key, leaving `dev`
rebased with a clean tree. Finally an ordinary commit of a modified and an untracked file, with
trailing whitespace in one of them, going through untouched.

### Step 4 findings

`Git/ConflictFile.cs` (the model), `Git/Private/ConflictParser.cs` (pure, no `ICmd` and no `File`),
and the reading, writing and per-path commands on `ConflictService`. No UI.

- [x] **One line ending flag per file would be wrong.** A merge of an LF file and a CRLF file really
      does produce a file with both, so the terminator is kept per line — `FileLine(Text, Eol)`,
      where `Eol` is `"\r\n"`, `"\n"`, or `""` on a last line the file did not end with. Because
      every line carries what followed it, writing back what was read needs no line ending logic at
      all, and git's check-in conversion then applies exactly as it would to a hand edit. There is
      no `core.autocrlf` handling anywhere in gmd as a result.
- [x] **`ToText(Parse(x)) == x` is the test that matters**, and it is asserted byte for byte over
      every shape a file can have: all three conflict styles, CRLF, *mixed* endings, no final
      newline, an empty file, an empty side, markers with no labels, longer markers, and conflicts
      at the very start and very end. Everything the resolver writes is a hunk change plus `ToText`,
      so while that identity holds, resolving one conflict cannot rewrite the rest of the file.
- [x] **Marker lines are kept verbatim rather than regenerated.** `.gitattributes` can set
      `conflict-marker-size`, so a file with eleven-character markers must come back with eleven.
      Recognition is a run of *at least* seven followed by end of line or a space, which is what
      makes `=======x` text and `<<<<<< ` (six) text.
- [x] **Anything that is not a complete conflict is text.** A `<<<<<<<` with no `>>>>>>>`, a
      `>>>>>>>` with no `=======`, two starts in a row — each is kept as ordinary lines rather than
      guessed at, which is what makes the round trip hold for a half-edited file too.
- [x] **Git normalizes a missing final newline itself, and gmd cannot put it back.** A conflicted
      file whose sides had no trailing newline comes out of git ending `>>>>>>> dev\n`, with a
      newline after the last line of each side so the markers can start a line — so the missing
      terminator is not represented in the conflict at all. Found by an integration test written
      expecting the opposite. What gmd must not do is add one of *its* own, which the write-back
      test and the parser round trip both pin.
- [x] **The trailing newline repair in `Chosen` is live only for hand edited text.** Every parsed
      block is followed by a marker line, so its last line always has a terminator; only `Manual`
      lines can arrive without one, and without the repair the line after the conflict would be
      joined onto the last edited line. Nearly left untested for being unreachable — it is not.
- [x] **`:(literal)` works on `add`, `checkout` and `rm` but not on `checkout-index`**, which wants
      a plain path (verified in Step 3's probing and again here). `--` alone does not disable
      globbing, so a file named `a[1].txt` would otherwise match `a1.txt`; pinned by a test with
      both files present.
- [x] A BOM is read, remembered and written back — `File.WriteAllText` defaults to UTF-8 *without*
      one, so a file that had a BOM would silently lose it and show as wholly changed. Decoding
      throws rather than replacing, so a file gmd cannot represent exactly is refused instead of
      being rewritten with `U+FFFD`.
- [x] The model is `public` like `ConflictKind` and `ConflictedFile` beside it, rather than
      `internal` like `CommitDiff` — a `[DataRow]` of an internal enum cannot be a public test
      method's parameter, and the family reads better kept together.
- [x] Step 4 stops at the Git layer: `IServer` gains nothing, because `ConflictFile` has no Server
      mirror until Step 5 builds one alongside its converter, as `CommitDiff` has.

### Step 4 verified

`./test` is green (697, up from 639) — 47 pure parser tests with no fake, no repository and no
driver, plus real-git tests for reading, writing, whole-file resolution, un-resolve, a `zdiff3`
repository, a BOM, CRLF, and a path with glob characters in its name.

**A pre-existing E2E flake was found while checking this and is *not* fixed here.**
`TestShowAndHideBranchRoundTrip` failed once in Step 3 and once here, and passes in isolation and in
three consecutive full runs of both this tree and an unmodified one. The cause looks like the test
rather than the app: it does `Send("Down")` then `WaitFor("Merge branch")`, but that text is already
on screen before the key, so the wait is satisfied by the already-settled screen and does not
actually wait for the key to be processed. Under load the `Left` can then be sent first and `Enter`
act on the wrong hoover. A real wait would be one for something that *changes* — the application bar
going from `(main)` to `(dev)` once the branch is hoovered.
