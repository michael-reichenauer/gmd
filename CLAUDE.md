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
./installtools   # devcontainer setup: tools, dotnet local tools, git hooks
```

Faster inner loop for verification: `dotnet build gmd.sln` and `./test`.

There are `.bat` equivalents for Windows (`build.bat`, `run.bat`, `log.bat`) — keep them in
sync when changing the shell scripts. Linux/macOS are the primary targets; the Windows
scripts exist mainly for debugging Windows-specific behavior.

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
- `Cui/RepoView/` — `IViewRepo` is the per-view facade the menus and command classes use;
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

### Formatting: CSharpier owns it

**Never hand-format C#, and never argue with the formatter.** [CSharpier](https://csharpier.com)
is the single source of truth for layout (line breaks, spacing, wrapping, `using` order), and
it also formats `.csproj` files. Settings are in `.csharpierrc` (`printWidth` is **120** — the
default 100 would explode this codebase's one-line delegation methods into one parameter per
line).

It runs in four places:

| Where | How |
| --- | --- |
| On save in VS Code | `.vscode/settings.json` → `editor.formatOnSave` + the `csharpier.csharpier-vscode` extension |
| On build | the `CSharpier.MsBuild` package in both `.csproj` files |
| On commit | `.git/hooks/pre-commit` (installed from `gmd/tools/pre-commit-sample`) |
| In CI | an explicit `dotnet csharpier check .` step before `./build` |

The MSBuild integration behaves differently per configuration, which matters:

- **Debug** — *formats* the sources in place before compiling. A `dotnet build` can therefore
  modify files in the working tree. This is intended.
- **Release** — *checks* only, and **fails the build** if anything is unformatted. This is why
  CI runs an explicit `csharpier check` step *before* `./build`: `./build` runs `dotnet test`
  (Debug) first, which would silently format everything and hide the problem from the Release
  check.
- Escape hatch: `dotnet build -p:CSharpier_Bypass=true`.

CSharpier is a local dotnet tool pinned in `.config/dotnet-tools.json`. If `dotnet csharpier`
is not found, run `dotnet tool restore`. Run it manually with `dotnet csharpier format .` or
`dotnet csharpier check .`. It honors `.gitignore`, so `obj/`/`bin/` are skipped.

`.editorconfig` deliberately contains **no** whitespace or wrapping rules — only naming rules
and non-layout code style, so it cannot conflict with CSharpier.

Note that C# raw string literals (`$"""…"""`) are indentation-normalized against their closing
delimiter, so CSharpier re-indenting one does not change the string's value.

### Style as found in this codebase

- Namespaces are file-scoped (`namespace gmd.Git.Private;`), one type family per file, and
  `interface IFoo` usually sits directly above `class Foo` in the same file.
- Fields are `readonly` and *not* prefixed (`readonly IGit git;`), assigned in a plain
  constructor — no primary constructors.
- **Collection expressions (`[]`) are preferred for new and touched code** — `List<string> x = [];`
  and `return [];` rather than `new List<string>()` / `new string[0]`. Most of the codebase
  predates C# 12 and still uses the old form; it is being migrated gradually rather than in one
  sweep, so expect both styles to coexist. The relevant analyzers (IDE0028, IDE0300–IDE0305) are
  enabled as suggestions, so the IDE will point out remaining sites as you work in a file.
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

MSTest 3.x + coverlet in `gmdTest/`, mirroring the `gmd/` folder layout. Growing this suite is
an explicit goal — see `MODERNIZATION.md` for what is planned next.

There are two pieces of test infrastructure; use them rather than inventing a third way.

**`RepoBuilder`** (`gmdTest/Fixtures/`) builds a `GitRepo` — the raw facts git would report —
and runs the real augmentation pipeline. Commits are declared **newest first** (git log order)
and named with short hex-ish names that expand to full 40-character ids:

```csharp
var repo = await new RepoBuilder()
    .Commit("c3", "Merge branch 'dev' into main", "c2", "d1")   // parents last
    .Commit("d1", "Feature work", "c1")
    .Commit("c2", "Second", "c1")
    .Commit("c1", "Initial")
    .BranchWithRemote("main", "c3", isCurrent: true)            // adds main + origin/main
    .LocalBranch("dev", "d1")
    .AugmentAsync();

Assert.AreEqual("dev", repo.CommitsById[RepoBuilder.Sha("d1")].Branch!.Name);
```

Watch out: a commit declared with no parents is a **root**, so forgetting the parent silently
changes the graph under test rather than failing loudly.

**`FakeCmd`** (`gmdTest/Utils/`) is a double for `ICmd`, the seam between the git services and
the `git` executable. Every git service takes `ICmd` in its constructor, so canned output tests
all parsing with no subprocess:

```csharp
var cmd = new FakeCmd(gitLogOutput);            // or FakeCmd.Fail("fatal: ...")
var log = new LogService(cmd);
Assert.IsTrue(Try(out var commits, out var e, await log.GetLogAsync(100, "/wd")));
StringAssert.Contains(cmd.Calls[0].Args, "--max-count=100");
```

Other things to know:

- Put a test at the path mirroring its subject, e.g.
  `gmdTest/Server/Private/Augmented/Private/AugmenterTest.cs`.
- `gmdTest/Usings.cs` provides the global usings (`Assert`, `Try`, `Log`).
- `internal` types are visible to tests, so services can be constructed directly
  (`new BranchNameService()`) — no DI container needed.
- The whole inference chain is constructible by hand and touches no git, disk or terminal:
  `Augmenter` → `BranchStructureService` → `BranchNameService` have no other dependencies, and
  `Converter` has none at all. `RepoBuilder.NewAugmenter()` wires it up.
- `Text.ToString()` flattens styled output to a plain string, so `GraphWriter` output can be
  snapshot-tested as ASCII art without a Terminal.Gui driver.
- `BranchColorService` and `ViewRepoCreater` need `IRepoConfig`, a two-method interface that
  fakes in-memory.
- Tests that need a real repository should create a throwaway repo in a temp dir and drive it
  through `IGit`; **never** run git commands against this working tree.
- Terminal.Gui views are not unit-testable without a driver — keep logic out of the view
  classes so it can be tested.
- Tests run sequentially (no `.runsettings`). `LogServiceTest` mutates
  `CultureInfo.DefaultThreadCurrentCulture`, so enabling parallel execution would need care.

Always run `./test` before reporting work done. Prefer adding a regression test with every
bug fix — that is the agreed direction for this repo. When the subject is a parser or the
inference pipeline, write the failing test first; both have already hidden real bugs.

For the inference pipeline the tests are **characterization** tests: they pin down what the code
actually does, not what it ought to do. Do not guess the expected values — discover them, then
assert. The quickest way is a throwaway test that dumps the result and fails, e.g.
`Assert.Fail(dumpOfEveryCommitAndBranch)`, read the real output, write the assertions, delete the
probe. Guessing produces tests that encode a bug as correct, or that fail for the wrong reason.
`Console.WriteLine` in a test is swallowed by the default logger, hence dumping via the failure
message.

## Gotchas

- **`CHANGELOG.md` is generated — never hand-edit it.** `gmd --updatechangelog` rewrites it
  from git history, driven by the `post-commit` hook (`gmd/tools/post-commit-sample`, installed
  by `./installtools`) on the `main` branch only.
- **`gmd/Build.cs` contains CI placeholders.** The literals `"BUILD_TIME"` and `"BUILD_SHA"`
  are `sed`-replaced by `.github/workflows/build-and-release.yml`. Do not rename, reformat or
  move that file or those strings.
- **Version lives in `gmd/Program.cs`** (`MajorVersion`/`MinorVersion`); the last two version
  components are derived from build time in `Build.cs`.
- **A Debug build rewrites source files** (CSharpier formatting — see above). Do not be
  surprised by a dirty working tree after `dotnet build`.
- **`gmdSetup.exe` is a prebuilt binary committed to the repo**
  (`gmd/Installation/installer/`). Neither `./build` nor CI builds the Inno Setup installer;
  CI just uploads the committed file. Rebuilding it requires Windows + `BuildSetup.bat`.
- **The `gmd_linux` release asset is a duplicate of `gmd_linux_x64`** kept under the original
  name because the built-in updater falls back to it (`gmd/Installation/Updater.cs`). Do not
  drop it from the release workflow.
- Branch layout: `main` = releases, `dev` = pre-releases; pushing to either publishes a
  GitHub release from CI. Work on feature branches and target `dev` unless told otherwise.
- `NoWarn` in `gmd.csproj` suppresses `IDE0090;CA1825`.
- `Utils/GlobPatterns/` is vendored third-party-style code. CSharpier formats it like
  everything else, but do not restructure its logic; `.editorconfig` keeps analyzers quiet there.

## Working agreements

- **`MODERNIZATION.md` is the running plan** for this work — the ordered, step-by-step checklist
  of what is done and what is next. Read it before starting anything substantial, and tick items
  off / add findings there as work lands.
- Modernizing this codebase, fixing bugs, adding tests and improving maintainability is the
  active goal — but keep changes reviewable. Prefer a series of focused commits over one
  sweeping refactor, especially around `BranchStructureService` and `RepoView`.
- Do not commit or push unless asked.
- When behavior visible to users changes, check whether `gmd/doc/help.md` (embedded into the
  binary as a resource) needs updating too.
