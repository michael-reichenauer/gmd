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

## Step 2 — Finish the augmentation characterization tests

The safety net that has to exist before `BranchStructureService` (~950 lines) can be refactored.
Extend `AugmenterTest` using `RepoBuilder`; no new infrastructure needed.

- [ ] Ambiguous commits and branches: `IsAmbiguous`, `IsAmbiguousTip`, `AmbiguousTip`,
      `AmbiguousBranches`. User-visible via the `Φ` symbol and the resolve/unresolve menu items,
      currently untested.
- [ ] `ResolveAmbiguityAsync` / `UnresolveAmbiguityAsync` / `SetBranchManuallyAsync` round trips.
- [ ] More merge-subject forms in `BranchNameService.ParseSubject`. The existing test covers 4;
      real git sees many more (GitHub/GitLab/Azure PR wording, `Merge remote-tracking branch`,
      localized clients, `Merge tag`).
- [ ] Branch hierarchy edge cases: `DetermineAncestors`, and `IsCircularAncestors` — there is
      explicit handling for circular ancestry that nothing currently exercises.
- [ ] Root branch selection when none of `main`/`master`/`trunk` exist.
- [ ] Divergent local/remote branches (`ahead`/`behind`, `HasLocalOnly`, `HasRemoteOnly`).
- [ ] Empty repo (`Repo.EmptyRepoCommitId`) and single-commit repo.
- [ ] Uncommitted changes (`Repo.UncommittedId`) via `RepoBuilder.WithStatus`, and merging state.
- [ ] Consider extending `RepoBuilder` with a compact text form for large graphs if the fluent
      calls start to obscure the shape being tested.

## Step 3 — Snapshot tests for the graph rendering

`Text.ToString()` flattens the styled output to a plain string, so the drawn graph is testable
without a Terminal.Gui driver. These read as pictures of the graph, which makes them reviewable.

- [ ] Fake `IRepoConfig` (two methods, in-memory) so `BranchColorService` and `ViewRepoCreater`
      can be constructed in tests.
- [ ] Pipeline helper: `GitRepo → Augmenter → Converter → ViewRepoCreater → GraphCreater →
      GraphWriter → string`.
- [ ] Snapshot tests over the Step 2 fixtures: linear history, branch out and merge, several
      concurrent branches, merges from deleted branches, truncated log.
- [ ] Branch show/hide (`ShowBranch`/`HideBranch`, the `ShowBranches` modes) — the product's
      distinguishing feature and entirely untested.
- [ ] Colors via `Text.Fragments` for `BranchColorService` (colors are stable per branch).
- [ ] Decide inline expected strings (preferred — reviewable in the diff, no extra tooling)
      versus a committed approval-file workflow.

## Step 4 — Remaining git output parsers

All via `FakeCmd`, with fixtures captured from real git output once and committed.

- [ ] `BranchService.ParseBranches` — highest priority. A 16-group regex with positional
      indices hardcoded in `ToBranch` (`Groups[1], [3], [4], [5], [8], [11], [14]`), so adding a
      group anywhere silently shifts everything after it. Cases: detached HEAD, `ahead`/`behind`/
      both, `gone` upstream, the `->` pointer lines `IsNormalBranch` filters, names with spaces.
      Then refactor to named groups, which the tests make safe.
- [ ] `StatusService.Parse` — porcelain output, the `" -> "` rename split, merge state and merge
      messages, conflicts.
- [ ] `DiffService` (468 lines, the largest git service) — hunk headers, binary files, renames,
      conflict markers.
- [ ] `TagService`, `StashService`, `RemoteService`, `CommitService`, `KeyValueService`.
- [ ] `MetaDataService` — the push/pull of branch choices through git key/value storage.

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
