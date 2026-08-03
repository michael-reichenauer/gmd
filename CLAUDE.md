# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this is

**Gmd** is a cross-platform console-UI (TUI) Git client written in C# / .NET 10, built on
[Terminal.Gui](https://github.com/gui-cs/Terminal.Gui) 1.x. Its distinguishing feature is
*interactive branch visibility*: the user chooses which branches are shown in the commit
graph, so a clean log is achieved without rebasing or squashing.

Gmd shells out to the `git` CLI — there is no libgit2/LibGit2Sharp dependency. All git
knowledge lives in `gmd/Git/`, and everything the user sees that git itself does not provide
(which branch a commit belongs to, branch hierarchy) is *inferred* in `gmd/Server/…/Augmented/`.

## Commands

```bash
./run [args]     # dotnet run --project gmd/gmd.csproj -- "$@"
./test [args]    # dotnet test gmdTest/gmdTest.csproj "$@"
                 #   --filter "TestCategory!=Integration"  fast tests only (~0.4 s)
                 #   --filter "TestCategory=E2e"           the tmux end-to-end UI tests
./build          # full release: test + package audit + publish all platforms (slow)
./build -l       # linux only (much faster; use this for local verification)
./log            # tail the runtime log with lnav (~/gmd.log)
./updatepackages # list outdated NuGet packages; -u non-major upgrades, -m incl. major
./installtools   # devcontainer setup: tools, dotnet local tools, git hooks
```

Faster inner loop for verification: `dotnet build gmd.sln` and `./test`.

### Running the TUI from a non-interactive shell

gmd is a full-screen curses app, so it needs a pty: started from a shell with no terminal it will
not run at all. There are two ways in, and they answer different questions.

**tmux — the real binary, and the one to use.** tmux parses the escape sequences and keeps a screen
model, so `capture-pane` hands back the rendered screen as plain text, which is what makes it
assertable. Installed by `./installtools`.

```bash
tmux new-session -d -s gmd -x 120 -y 40 -c /path/to/some/repo  gmd/bin/Debug/net10.0/gmd
until tmux capture-pane -t gmd -p | grep -q "uncommitted"; do sleep 1; done  # wait, never sleep blind
tmux capture-pane -t gmd -p        # the rendered screen, as the user sees it
tmux capture-pane -t gmd -p -e     # ... with the colors kept as ANSI
tmux send-keys -t gmd d            # press a key; Escape is `tmux send-keys -t gmd Escape`
tmux kill-session -t gmd
```

Always drive the *built binary* (`gmd/bin/Debug/net10.0/gmd`), not `./run` — `dotnet run` wraps the
app in a second process, so the pid you measure or kill is the wrong one. Never point it at this
working tree; use a throwaway repo, exactly as `TempRepo` does.

**Redirect `HOME` whenever you start gmd yourself.** A gmd run does not merely read the developer's
home: it *writes* `~/.gmdconfig` (the git version, and the opened repo into `RecentFolders`),
**truncates `~/gmd.log`**, and **deletes `~/.gmdstate*`**. None of those paths can be redirected —
`ConfigService`, `ConfigLogger` and `Upgrader` all anchor on `SpecialFolder.UserProfile` with no
override — so `HOME` is the only lever, and on Unix it also isolates `~/.gitconfig` from the git
commands gmd runs. Seed `{"CheckUpdates": false}` into that config as well: `Build.IsDevInstance()`
only recognizes `gmd.dll` and `dotnet`, so the *built binary is not a dev instance* and really does
call the GitHub releases API on startup.

**`script` — the fallback when tmux is missing.** `script -qfc "stty rows 45 cols 140; <cmd>"
/dev/null` gives a pty and records the raw byte stream. That stream is redraw *traffic*, not a
screen, so it reads as a smear of partial updates and is poor to assert on. Fine for "does it start
and not crash", and for measuring CPU; use tmux for anything about what is on screen.

Measuring CPU needs the delta of `utime+stime` from `/proc/<pid>/stat` over a window — `ps %cpu` is
the average over the whole process lifetime, which hides a spin that starts late.

**The same thing, as tests.** All of the above is packaged as `TmuxSession` (`gmdTest/Fixtures/`),
and the tests are `gmdTest/Cui/TerminalTest.cs` — see the Testing section below.

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
- `Augmented/Private/BranchStructureService.cs` — the heart of the product, and the most subtle
  code in the repo. `DetermineCommitBranches` is only the pipeline: set branch tips → link
  parents/children → assign branches to commits → build hierarchy → find root branch → compute
  ancestors. Each stage is its own class, so start from the pipeline and follow the call:
  - `CommitGraphService` — the first two stages, which make the commit graph traversable.
  - `CommitBranchService` — assigns a branch to every commit. `DetermineCommitBranch` is an
    ordered chain of rules where the order *is* the strength of the evidence; the rules
    themselves are `CommitBranchRules`, and `BranchFactory` / `BranchAmbiguity` are what they
    call when a branch has to be invented or a commit given up on as ambiguous.
  - `BranchHierarchyService` — the last three stages, which relate the branches to each other.

  Merge-commit subjects are parsed by `BranchNameService` to recover branch names git has
  forgotten. Treat all of this as high-risk: change it only with tests, and preserve the
  pipeline comments. The bar these files are held to is a before/after comparison over a real
  repo's history, not just a green suite — see the findings in `MODERNIZATION.md`.
- `Augmented/Private/MetaDataService.cs` — persists user branch choices as git key/value
  data so they can be pushed/pulled and shared.
- `Cui/RepoView/` — `IViewRepo` is the per-view facade the menus and command classes use;
  `RepoView.cs` is the main log view, i.e. reading and refreshing the shown repo and drawing it.
  What the user does to it is split off: `RepoViewInput.cs` holds every key and mouse button plus
  the handlers they dispatch through, and `Hoover.cs` holds which branch the pointer or cursor is
  on — what most keys act on — as state and index math with no view, so it is unit testable.
  Commands are grouped into `RepoCommands` / `BranchCommands` / `CommitCommands`, menus into
  `*Menu.cs`.
- `Cui/GraphCreater.cs` + `Graph.cs` + `GraphWriter.cs` — turn a `Repo` into the drawn
  branch graph.
- `Cui/Common/ContentView.cs` — the scrollable list of rows nearly every view is drawn in (log,
  diff, menus, dialogs). Rows are either handed to the constructor or fetched while drawing through
  a `GetContentCallback`, so a large repo is never materialized as text. Where it is scrolled to
  (`ContentScroll.cs`) and what is selected (`ContentSelection.cs`) are index math with no view, so
  they are unit testable — keep new logic there rather than in the view.
- `Cui/Common/UIDialog.cs` — builds a dialog from the custom views beside it (`UILabel`,
  `UITextField`, `UITextView`, `UIComboTextField`, `BorderView`) and runs it modally.

Layering note: the arrow is clean — nothing below `gmd/Cui/` references it, and nothing below
it references Terminal.Gui. The one thing a lower layer needs from the UI is its main loop, and
that goes through `IMainThread` (`gmd/Utils/IMainThread.cs`): `Post` to raise an event on the UI
thread, `RunPeriodically` for a timer, implemented by `MainThread` (`Cui/Common/`) over `UI`.
`FileMonitor` is its only user. Keep it that way: if a lower layer needs something else from the
UI, widen that interface rather than reaching up. (`Utils/Clipboard.cs` wrapping
`Terminal.Gui.Clipboard` is the one remaining direct Terminal.Gui use outside `Cui/`.)

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

There are five pieces of test infrastructure; use them rather than inventing a sixth way.

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

`AugmentAsync()` stops at the `WorkRepo`. `ViewRepoAsync(...)` goes all the way to the
`Server.Repo` the UI renders — augmentation, the uncommitted commit, and the branches the user
chose to show — by building the real `AugmentedService` and `ViewRepoCreater` with the fakes in
`gmdTest/Fixtures/` (`FakeGit`, `FakeFileMonitor`, `FakeMetaDataService`, `FakeRepoConfig`).
`FakeGit` implements only the members the pipeline reaches and throws on the rest, so a test that
starts depending on git fails loudly. `builder.Config` is the in-memory `IRepoConfig`, for tests
that set branch colors or branch order.

**`GraphText`** (`gmdTest/Fixtures/`) draws the graph of a view repo as plain text, so the
expected value is a picture that can be reviewed by looking at it:

```csharp
var repo = await new RepoBuilder()
    .Commit("c2", "Second", "c1")
    .Commit("c1", "Initial")
    .BranchWithRemote("main", "c2", isCurrent: true)
    .ViewRepoAsync();                         // or ViewRepoAsync("dev"), ViewRepoAsync(ShowBranches.AllActive)

Assert.AreEqual(
    """
    ┣─┺  Second
    ┗    Initial
    """,
    GraphText.WithSubjects(repo));
```

`Of` is the graph alone, `WithSubjects` adds the commit subject, and `ColorsOf` gives one letter
per rune telling its color (`M` magenta, `B` blue, `W` white, …), aligned under `Of`. Use raw
string literals for the expected value — they keep the picture readable and their value is
unaffected by CSharpier re-indenting them.

**`FakeCmd`** (`gmdTest/Utils/`) is a double for `ICmd`, the seam between the git services and
the `git` executable. Every git service takes `ICmd` in its constructor, so canned output tests
all parsing with no subprocess:

```csharp
var cmd = new FakeCmd(gitLogOutput);            // or FakeCmd.Fail("fatal: ...")
var log = new LogService(cmd);
Assert.IsTrue(Try(out var commits, out var e, await log.GetLogAsync(100, "/wd")));
StringAssert.Contains(cmd.Calls[0].Args, "--max-count=100");
```

**`TempRepo`** (`gmdTest/Fixtures/`) is the opposite end: a throwaway repository in the system
temp folder, driven through the real `git` executable and the real `IGit` services. It is the
canary for git version and output-format drift, which canned output cannot catch, so keep these
few and small — `FakeCmd` is the right tool for anything about parsing:

```csharp
using var repo = await TempRepo.CreateAsync();      // 'main', local config set, no commits yet
var c1 = await repo.CommitFileAsync("file.txt", "text\n", "Initial");
Assert.IsTrue(Try(out var e, await repo.Git.CreateBranchAsync("dev", true, repo.Path)), $"{e}");
await repo.AddOriginAsync();                        // a bare repo next door, for push/fetch
await repo.GitAsync("reset --hard HEAD~1");         // raw git, for what IGit has no method for
```

The repository is deleted on `Dispose`, and nothing outside its temp folder is ever touched —
`Dispose` refuses to delete a path it did not create. Both integration test classes
(`GitIntegrationTest`, `AugmentedServiceIntegrationTest`) carry `[TestCategory("Integration")]`,
so `./test --filter "TestCategory!=Integration"` runs only the fast tests. `./test` passes its
arguments on to `dotnet test`.

`CommitFileAtAsync` / `CommitAtAsync` / `GitAt` commit with the author *and* committer dates pinned.
Use them for any fixture whose drawn output is asserted: it fixes the time column, makes the commit
ids reproducible (a commit object is just its tree, parents, identity, dates and message), and —
the part that is not cosmetic — removes the row-order flake, since `git log --all --date-order`
orders by commit date and has nothing to break a tie with. They go around `IGit` because `ICmd`
cannot pass environment variables, and `GIT_COMMITTER_DATE` is the only way to set a committer date.

**`TmuxSession`** (`gmdTest/Fixtures/`) is the end-to-end tier: the built binary, real git, a real
pty. tmux keeps a screen model, so `capture-pane` gives back the rendered screen, and that is what
is asserted — the drawing, the layout, the key dispatch and the dialogs, none of which anything
else in the suite reaches. It names no Terminal.Gui type, deliberately, so it is as valid against a
2.x build as a 1.x one. `ScreenText` normalizes a capture and `E2eRepo` builds the fixture repo:

```csharp
using var repo = await E2eRepo.CreateAsync();
using var gmd = TmuxSession.StartGmd(repo);            // 120x40, hermetic env, updater off
ScreenText.AssertEqual("""<a picture of the screen>""", gmd.WaitFor("Initial"), repo.Path);
gmd.Send("Enter");                                     // a key; SendText types into a dialog
gmd.WaitUntilGone("Gmd Help Guide");                   // i.e. "the dialog closed"
```

Run them with `./test --filter "TestCategory=E2e"`; they also carry `Integration`, so the fast
filter above excludes them. Five things they do that matter, and that a new test must keep doing:

- **A throwaway `$HOME` per session**, seeded with `CheckUpdates: false` — see the `HOME` paragraph
  under "Running the TUI from a non-interactive shell" for why both halves are mandatory.
- **`TZ=UTC` and `LC_ALL=C.UTF-8`**, since the time column is local time formatted with the current
  culture, and the UI is drawn with `● ┣ ┅ Ϙ`.
- **A private tmux server** (`-L <socket> -f <conf>`, socket inside the temp home) so the
  developer's `~/.tmux.conf` and running server cannot change a capture or be disturbed by one.
- **Polling, never sleeping.** `WaitFor` waits for the text *and* for three identical captures in a
  row. The stability half is not optional: gmd **drops** keystrokes while a git command is running
  (`Progress.Show` → `UI.StopInput` → `RootKeyEvent = _ => true`), so a key sent into a moving
  screen is silently lost, not queued.
- **A fresh repo per test.** `<repo>/.git/.gmdconfig` holds the shown-branch list and is rewritten
  on every repo show, and it is the one piece of state `HOME` cannot isolate.

Two traps worth knowing before adding one: **`Escape` in the log view quits the app**, so never send
a "safety" Escape; and a modal dialog is drawn *over* the log view rather than replacing it, so the
rows behind it still match whatever `WaitFor` is looking for — use `WaitUntilGone` to mean "closed".
When a snapshot disagrees, `AssertEqual` prints the actual screen ready to paste back in, and
`GMD_E2E_KEEP=1` leaves the session up to attach to.

Other things to know:

- Put a test at the path mirroring its subject, e.g.
  `gmdTest/Server/Private/Augmented/Private/AugmenterTest.cs`.
- `gmdTest/Usings.cs` provides the global usings (`Assert`, `Try`, `Log`).
- `internal` types are visible to tests, so services can be constructed directly
  (`new BranchNameService()`) — no DI container needed.
- The whole inference chain is constructible by hand and touches no git, disk or terminal:
  `Augmenter` → `BranchStructureService` → its three stage services → `BranchNameService`, whose
  only dependency is the one before it, and `Converter` has none at all.
  `RepoBuilder.NewAugmenter()` wires the lot up, so use that rather than repeating it.
- `Text.ToString()` flattens styled output to a plain string, which is how `GraphText` snapshots
  `GraphWriter` output as ASCII art without a Terminal.Gui driver.
- Tests that need a real repository use `TempRepo`; **never** run git commands against this
  working tree.
- Anything that *draws* needs a driver; constructing and driving a view does not. `ContentViewTest`
  builds a real `ContentView`, sets its `Frame` (which is where its height comes from) and exercises
  everything on it except drawing. Keep logic out of the view classes so it stays reachable this way
  — that is why `ContentScroll`, `ContentSelection`, `Hoover`, `MenuDimensions` and `MenuRows` exist.
- **A driver is available in the box, and drawing is testable with no terminal.** Terminal.Gui ships
  a public `FakeDriver`, and `Application.Init(new FakeDriver(), null)` succeeds headlessly —
  `FakeDriver.Contents` is then the rendered cell grid, `[row, col, 0]` being the rune and
  `[row, col, 1]` the attribute, so both the drawn text *and* its colors can be asserted. Verified
  against 1.19.0 by a standalone probe; not adopted by the suite yet, and note `FakeMainLoop` is
  `internal`, so pass `null` as the main-loop driver rather than trying to construct one. See the
  Step 3 finding in `MODERNIZATION.md`.
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
