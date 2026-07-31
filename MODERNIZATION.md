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
- [ ] **`DiffService` cannot parse a combined diff.** A `diff --cc` file is recognized and marked
      `DiffConflicts`, but its `@@@ -1,1 -1,1 +1,1 @@@` hunk headers are not (`ParseSectionDiff`
      only accepts `@@ `), so the file parses with no content at all. Unreachable today: every gmd
      git command uses `--first-parent`, and none passes `--cc` or `-m`. Either delete the
      `diff --cc` branch or finish it — showing a merge commit's true conflict resolution would be
      a genuinely useful feature, and this half-written branch is what it would need.
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

## Step 5 — A thin layer of real-git integration tests

Few and deliberately small — a canary for git version and output-format drift, which the
`FakeCmd` tests cannot catch.

- [ ] Temp-repo helper: create a throwaway repo in a temp dir, run real `git init`/commit/
      branch/merge, drive it through `IGit`, assert. Must never touch the working tree.
- [ ] Cover the round trip for log, branches, status, and a merge.
- [ ] Keep them in a separate test category so `./test` can stay fast if they get slow.

## Step 6 — Pure utility tests

Cheap, quick, and they establish the habit.

- [ ] `Result` / `R<T>`: error propagation, the fail-fast when the error state was never checked,
      implicit conversions.
- [ ] `StringExtensions`: `Sid`, `TrimPrefix`, `TrimSuffix`.
- [ ] `Build.Version()` / `GetBuildTime` round-tripping, including the CI-placeholder path.
- [ ] `EnumerableExtensions`, `Sorter`, `TimeDateExtensions`.
- [ ] `Utils/GlobPatterns` — vendored, so tests document what we rely on.

## Step 7 — CI and coverage

- [ ] A fast test-only job so PRs get feedback without the full multi-platform publish
      (today a PR runs all of `./build`).
- [ ] Turn on the `coverlet.collector` that is already referenced but unused; report coverage.
- [ ] Consider a coverage floor once the number stabilizes — as a ratchet, not a hard gate.

---

## Step 8 — Maintainability

- [ ] Fix the layering violation: `gmd/Server/` has three `using gmd.Cui.RepoView;` lines
      (`IServer.cs`, `Server.cs`, `AugmentedService.cs`), so the dependency arrow points back up
      into the UI. Move the shared types down.
- [ ] Break up `BranchStructureService.cs` (~950 lines) along its existing pipeline stages, which
      are already well separated by `DetermineCommitBranches`. Needs Step 2 first.
- [ ] Break up `RepoView.cs` (~1000 lines), pulling logic out of the view so it becomes testable.
- [ ] `Cui/Common/UIDialog.cs` (633) and `Cui/Common/ContentView.cs` (621) are the next largest.
- [ ] Two different `Converter` classes (`Server/Private/` and `Server/Private/Augmented/Private/`)
      — confusing when both are in scope; consider renaming.
- [ ] `TagServis.cs` — filename typo, should be `TagService.cs`.
- [ ] Reconsider `NoWarn IDE0090;CA1825` in `gmd.csproj` once formatting churn has settled.

### Migrate to collection expressions

Collection expressions (`[]`) are the preferred style going forward. The codebase predates C# 12,
so the analyzers are enabled as **suggestions** — new and touched code adopts `[]`, the rest
migrates over time. Current state: **156 sites across 44 files**.

| Diagnostic | Sites | What it changes | Risk |
| --- | --- | --- | --- |
| IDE0028 | 71 | `new List<T>()` → `[]` for initializers | Safe, mechanical |
| IDE0300 | 68 | `new T[0]` / `new[] { … }` → `[]` | Safe, mechanical |
| IDE0301 | 3 | empty collection → `[]` | Safe, mechanical |
| IDE0305 | 14 | fluent `.ToList()` / `.ToArray()` → `[.. x]` | **Review by hand** |

- [ ] Sweep the safe ones as one mechanical commit (then run CSharpier, since it normalizes the
      resulting layout but does not do the conversion itself):
      `dotnet format style --diagnostics IDE0028 IDE0300 IDE0301 --severity info gmd.sln`
- [ ] IDE0305 by hand, in a separate commit. Converting `x.ToList()` to `[.. x]` can change the
      concrete type produced behind an `IReadOnlyList<T>` return, and this codebase returns
      `IReadOnlyList<T>` widely, so each site needs a look rather than a blanket fix.
- [ ] Sequencing note: test coverage is still thin outside the augmentation pipeline and
      `LogService`, so a 156-site sweep is less safe than it looks. Either do it after Steps 4–6
      widen coverage, or accept it as a reviewed mechanical change verified by build + CSharpier.
- [ ] Open question: target-typed `new()` (IDE0090) is currently in `<NoWarn>` in `gmd.csproj` —
      the same "codebase predates the feature" category. Enable it too, or keep types explicit
      there?

## Step 9 — Framework and dependency updates

Deliberately after the tests, so regressions are detectable. Current status of every dependency:

| Package | Current | Latest | Notes |
| --- | --- | --- | --- |
| Terminal.Gui | 1.17.1 | 2.4.17 | Major rewrite of the UI layer. Do last. |
| Autofac | 8.1.0 | 9.3.1 | Major; DI is small and centralized, so low risk. |
| DiffPlex | 1.7.2 | 1.9.0 | Minor. |
| MSTest.\* | 3.6.0 | 4.3.2 | Major; do early, it only affects tests. |
| Microsoft.NET.Test.Sdk | 17.11.1 | 18.8.1 | Major. |
| coverlet.collector | 6.0.2 | 10.0.1 | Major. |

- [ ] Test packages first (MSTest 4.x, Test.Sdk 18.x, coverlet 10.x) — contained to `gmdTest`.
- [ ] `DiffPlex` and `Autofac`.
- [ ] .NET 8 → .NET 10 (LTS). .NET 8 support ends Nov 2026. Needs the devcontainer image, CI
      `dotnet-version`, both `TargetFramework`s, `DOTNET` in `build`/`build.bat`, and
      `.vscode/launch.json` updated together.
- [ ] Terminal.Gui 1.x → 2.x. The big one. Should not start until Step 3 gives the UI-adjacent
      logic snapshot coverage.

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
