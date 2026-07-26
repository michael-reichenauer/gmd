# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this is

**Gmd** is a cross-platform console-UI (TUI) Git client written in C# / .NET 8, built on
[Terminal.Gui](https://github.com/gui-cs/Terminal.Gui) 1.x. Its distinguishing feature is
*interactive branch visibility*: the user chooses which branches are shown in the commit
graph, so a clean log is achieved without rebasing or squashing.

Gmd shells out to the `git` CLI — there is no libgit2/LibGit2Sharp dependency. All git
knowledge lives in `gmd/Git/`, and everything the user sees that git itself does not provide
(which branch a commit belongs to, branch hierarchy) is *inferred* in `gmd/Server/…/Augmented/`.

## Commands

```bash
./run [args]     # dotnet run --project gmd/gmd.csproj -- "$@"
./test           # dotnet test gmdTest/gmdTest.csproj
./build          # full release: test + package audit + publish all platforms (slow)
./build -l       # linux only (much faster; use this for local verification)
./log            # tail the runtime log with lnav (~/gmd.log)
./updatepackages # list outdated NuGet packages; -u non-major upgrades, -m incl. major
```

Faster inner loop for verification: `dotnet build gmd.sln` and `./test`.

There are `.bat` equivalents for Windows (`build.bat`, `run.bat`, `log.bat`) — keep them in
sync when changing the shell scripts.

Runtime log: `~/gmd.log`. Log with `Log.Info/Warn/Error/Debug/Exception` (`gmd.Utils.Logging`,
already a global using). The TUI owns stdout, so **never use `Console.WriteLine` for
diagnostics** — only `ProgramCommands` (pre-UI command-line handling) prints to the console.

## Architecture

Strictly layered, top calls down. Each layer has a public `IXxx` interface and a `Private/`
folder holding the implementation:

```
gmd/Cui/           Terminal.Gui views, dialogs, menus, graph rendering
    ↓ IServer
gmd/Server/        Repo model the UI consumes (view repo = filtered/shown subset)
    ↓ IAugmentedService
gmd/Server/Private/Augmented/   Infers commit→branch assignment + branch hierarchy
    ↓ IGit
gmd/Git/           One service per git area (log, branch, status, diff, remote, stash, tag…)
    ↓ ICmd
gmd/Utils/Cmd.cs   Process launcher for the `git` executable
```

Key types and flow:

- `gmd/Server/Repo.cs` — the immutable `record Repo` the UI renders. Holds `AllCommits`/
  `AllBranches` plus the `ViewCommits`/`ViewBranches` subset the user chose to show, and
  `CommitById`/`BranchByName` lookups. `Repo.UncommittedId`, `TruncatedLogCommitId` and
  `EmptyRepoCommitId` are sentinel all-`0`/`f`/`e` SHAs — check for them when touching commit code.
- `Augmented/Private/BranchStructureService.cs` — the heart of the product and the largest,
  most subtle file (~950 lines). `DetermineCommitBranches` runs a fixed pipeline: set branch
  tips → link parents/children → assign branches to commits → build hierarchy → find root
  branch → compute ancestors. Merge-commit subjects are parsed by `BranchNameService` to
  recover branch names git has forgotten. Treat this file as high-risk: change it only with
  tests, and preserve the pipeline comments.
- `Augmented/Private/MetaDataService.cs` — persists user branch choices as git key/value
  data so they can be pushed/pulled and shared.
- `Cui/RepoView/` — `IViewRepo` is the per-view façade the menus and command classes use;
  `RepoView.cs` (~1000 lines) is the main log view. Commands are grouped into
  `RepoCommands` / `BranchCommands` / `CommitCommands`, menus into `*Menu.cs`.
- `Cui/GraphCreater.cs` + `Graph.cs` + `GraphWriter.cs` — turn a `Repo` into the drawn
  branch graph.

Layering note: `gmd/Server/` currently has three `using gmd.Cui.RepoView;` lines
(`IServer.cs`, `Server.cs`, `AugmentedService.cs`), so the dependency arrow is not clean
today. Do not add new upward references; removing the existing ones is welcome cleanup.

### Dependency injection

Autofac, configured in `gmd/Utils/DependencyInjection.cs`. `RegisterAllAssemblyTypes()`
scans the whole assembly and registers every type `AsSelf().AsImplementedInterfaces()`.
Consequences to remember:

- **New classes are auto-registered** — no registration code to write. Add a constructor
  that takes the interfaces you need.
- Mark a type `[SingleInstance]` (the attribute in `DependencyInjection.cs`) for singletons.
  Services holding state or events (`Git`, `Server`, `AugmentedService`, `Config`) do.
- Internal and non-public constructors are found via a custom `DefaultConstructorFinder`,
  so `internal` constructors are fine.
- Because registration is by convention, a type implementing an interface that already has
  an implementation will silently take over the resolution — check for an existing
  implementer before adding one.

## Conventions

### Errors: `R` / `R<T>` and `Try`, not exceptions

`gmd/Utils/Result.cs` defines a result type used for all fallible operations, with
`global using static gmd.Utils.Result;` in `gmd/Usings.cs` making `Try` available everywhere.
Exceptions are for bugs, not for control flow.

```csharp
// Propagate an error from a call that returns R<T>
if (!Try(out var status, out var e, await git.GetStatusAsync(wd))) return e;

// Ignore the error value
if (!Try(out var branches, await git.GetBranchesAsync(wd))) return R.Error("no branches");

// Wrap a throwing API into an R
if (!Try(out var e, () => File.Move(src, dst))) return e;

// Returning: implicit conversions mean you just return the value or an error
return commits;                       // → R<IReadOnlyList<Commit>>
return R.Error($"Folder missing: {path}");
return R.Ok;                          // → R
```

`R.Error` captures caller file/line automatically. `R<T>.GetResultValue()` fail-fasts if the
error state was never checked, so always go through `Try`.

### Style as found in this codebase

- Namespaces are file-scoped (`namespace gmd.Git.Private;`), one type family per file, and
  `interface IFoo` usually sits directly above `class Foo` in the same file.
- Fields are `readonly` and *not* prefixed (`readonly IGit git;`), assigned in a plain
  constructor — no primary constructors.
- Expression-bodied one-line members are used heavily for delegation (see `Git.cs`).
- Nullable reference types and implicit usings are **on**; `gmd/Usings.cs` holds the global
  usings and `[assembly: InternalsVisibleTo("gmdTest")]` (so tests can reach `internal` types).
- Every git-facing method takes a trailing `string wd` — the repo working directory. It is
  threaded through explicitly rather than stored; keep doing that.
- Async methods end in `Async` and return `Task<R<...>>`. `RunInBackground()`
  (`Utils/TaskExtensions.cs`) is the fire-and-forget helper.
- Comments explain *why* / describe the algorithm step. The `Augmented` and graph code relies
  on them — keep them accurate rather than deleting them.

### UI threading

Terminal.Gui has a single main loop thread. Marshal back with `UI.Post(...)`
(`Cui/Common/UI.cs`), assert with `UI.AssertOnUIThread()` / `Threading.AssertIsMainThread()`.
Dialogs run via `UI.RunDialog`; message boxes via `UI.InfoMessage` / `UI.ErrorMessage`.
`Asserter.FailFast` deliberately kills the process on broken invariants.

### Persistence

- `~/.gmdconfig` — user config (`Common/Config.cs` + `ConfigService`), JSON via `FileStore`.
- Per-repo state — `Common/RepoConfig.cs`.
- Shared branch metadata — inside the git repo via `MetaDataService`.

## Testing

MSTest 3.x + coverlet in `gmdTest/`, mirroring the `gmd/` folder layout. Currently there are
effectively **two real tests** (`BranchNameServiceTest`, plus a placeholder `UnitTest1`) —
`GitRepoTest.cs` is entirely commented out and `StateTest.cs` is empty. Growing this suite is
an explicit goal of ongoing work.

- Put a test at the path mirroring its subject, e.g.
  `gmdTest/Server/Private/Augmented/Private/BranchNameServiceTest.cs`.
- `gmdTest/Usings.cs` provides the global usings (`Assert`, `Try`, `Log`).
- `internal` types are visible to tests, so services can be constructed directly
  (`new BranchNameService()`) — no DI container needed in tests.
- Best test targets are the pure/inference layers: `BranchNameService`, `BranchStructureService`,
  `Augmenter`, `ViewRepoCreater`, `GraphCreater`, `Utils/GlobPatterns`, `Utils/Result`.
  These need no git process and no terminal.
- Tests that need a real repository should create a throwaway repo in a temp dir and drive it
  through `IGit`; do not run git commands against this working tree.
- Terminal.Gui views are not unit-testable without a driver — keep logic out of the view
  classes so it can be tested.

Always run `./test` before reporting work done. Prefer adding a regression test with every
bug fix — that is the agreed direction for this repo.

## Gotchas

- **`CHANGELOG.md` is generated — never hand-edit it.** `gmd --updatechangelog` rewrites it
  from git history, driven by the `post-commit` hook (`gmd/tools/post-commit-sample`, installed
  by `./installtools`) on the `main` branch only.
- **`gmd/Build.cs` contains CI placeholders.** The literals `"BUILD_TIME"` and `"BUILD_SHA"`
  are `sed`-replaced by `.github/workflows/build-and-release.yml`. Do not rename, reformat or
  move that file or those strings.
- **Version lives in `gmd/Program.cs`** (`MajorVersion`/`MinorVersion`); the last two version
  components are derived from build time in `Build.cs`.
- **Known stale references to .NET 7** (net8.0 is what the projects target): `build.bat`
  (`set DOTNET="net7.0"`), `.vscode/launch.json` (`bin/Debug/net7.0/gmd.dll`), and
  `.github/workflows/build-and-release.yml` (`dotnet-version: '7.x'`). Fixing these is
  wanted; be aware the CI one changes release behaviour.
- Branch layout: `main` = releases, `dev` = pre-releases; pushing to either publishes a
  GitHub release from CI. Work on feature branches and target `dev` unless told otherwise.
- `NoWarn` in `gmd.csproj` suppresses `IDE0090;CA1825`. There is no `.editorconfig` yet, so
  formatting is by imitation of nearby code.
- `Utils/GlobPatterns/` is vendored third-party-style code — leave its style alone.

## Working agreements

- Modernizing this codebase, fixing bugs, adding tests and improving maintainability is the
  active goal — but keep changes reviewable. Prefer a series of focused commits over one
  sweeping refactor, especially around `BranchStructureService` and `RepoView`.
- Do not commit or push unless asked.
- When behaviour visible to users changes, check whether `gmd/doc/help.md` (embedded into the
  binary as a resource) needs updating too.
