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
./test [args]    # dotnet test gmd.sln "$@", i.e. both gmdTest and gmdE2eTest (~35 s with the build)
                 #   --filter "TestCategory!=Integration"  fast tests only (~890 tests, ~1 s)
                 #   --filter "TestCategory=E2e"           the tmux end-to-end UI tests (~30 s, in parallel)
./build          # full release: test + package audit + publish all platforms (slow)
./build -l       # linux only (x64 and arm64; much faster — use this for local verification)
./log            # tail the runtime log with lnav (~/gmd.log)
./updatepackages # list outdated NuGet packages; -u non-major upgrades, -m incl. major
./installtools   # devcontainer setup: tools, dotnet local tools, git hooks
./demo           # re-record gmd/doc/Animation.gif, the README's animation (~30 s; tmux + agg)
```

Faster inner loop for verification: `dotnet build gmd.sln` and `./test`.

### Running the TUI from a non-interactive shell

gmd is a full-screen curses app, so it needs a pty: started from a shell with no terminal it will
not run at all. tmux is the way in — it parses the escape sequences and keeps a screen model, so
`capture-pane` hands back the rendered screen as plain text, which is what makes it assertable.
Installed by `./installtools`.

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

`script -qfc "stty rows 45 cols 140; <cmd>" /dev/null` is the fallback when tmux is missing: a pty,
but only the raw byte stream, which is redraw *traffic* rather than a screen and so is poor to
assert on. Fine for "does it start and not crash", and for measuring CPU — which needs the delta of
`utime+stime` from `/proc/<pid>/stat` over a window, since `ps %cpu` averages over the whole process
lifetime and hides a spin that starts late.

All of the above is packaged as `TmuxSession` (`gmdE2eTest/Fixtures/`) and driven by the tests in
`gmdE2eTest/Cui/` — see the Testing section.

There are `.bat` equivalents for Windows (`build.bat`, `run.bat`, `log.bat`) — keep them in
sync when changing the shell scripts. Linux/macOS are the primary targets; the Windows
scripts exist mainly for debugging Windows-specific behavior. `./demo` has none, since it drives
gmd through tmux, as the end-to-end tests do.

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
gmd/Git/           One service per git area (log, branch, status, diff, blame, conflict, remote…)
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
  `KeyHints.cs` decides the key-hint line at the bottom, the keys that do something where the
  cursor is, again with no view; `KeyHintBar` draws it. When a key's behavior changes, check the
  hint for it.
  Commands are grouped by area (`RepoCommands`, `BranchCommands`, `BranchCreateCommands`,
  `BranchPushPullCommands`, `CommitCommands`, run through `CommandRunner`), menus into `*Menu.cs`.
  A menu item that can be greyed out gives the reason with `whyNot:` (`MenuItem.WhyNot`, the shared
  reasons in `Why.cs`), which is said on the status line when it is picked anyway, by a click or
  its key. Keys are written as typed: a menu shortcut is `"c"` for the c key and `"Shift-P"` for P,
  the help does the same, and the key-hint line writes a shifted letter as `⇧p`. Item names are
  plain, with no slashes, and end in " ..." only when the item asks for something before it runs.
- `Cui/GraphCreater.cs` + `Graph.cs` + `GraphWriter.cs` — turn a `Repo` into the drawn
  branch graph.
- `Cui/Common/ContentView.cs` — the scrollable list of rows nearly every view is drawn in (log,
  diff, blame, conflict, menus, dialogs). Rows are either handed to the constructor or fetched while
  drawing through a `GetContentCallback`, so a large repo is never materialized as text. Where it is
  scrolled to (`ContentScroll.cs`) and what is selected (`ContentSelection.cs`) are index math with
  no view, so they are unit testable — keep new logic there rather than in the view.
- `Cui/Common/UIDialog.cs` — builds a dialog from the custom views beside it (`UILabel`,
  `UITextField`, `UITextView`, `UIComboTextField`, `BorderView`) and runs it modally.
- `Utils/ClipboardService.cs` — copying, which has no one answer: a chain of writers tried in order,
  first one that works wins (`pbcopy`; `wl-copy`/`xclip`/`xsel`, each only when its display server
  is actually there; `clip.exe`; `WindowsClipboard.cs`), falling back to OSC 52
  (`TerminalClipboard.cs`) — the only one that reaches the clipboard over ssh or from a container.
  Tools get the text through `ICmd.CommandWithStdin`, which unlike `Command` never waits for the
  child's output streams: `xclip` and friends fork a helper that inherits them and would otherwise
  hang gmd (dotnet/runtime#27128). A copy that fails must say so at the call site.

### The three side views: diff, blame, conflict

Each is its own folder under `Cui/`, and each splits the same way: the view drives Terminal.Gui, and
everything that is layout or decision-making sits beside it as a plain class with no view, so it is
unit testable — `*Rows`, `*Columns`, `ConflictResolution`. Each runs through `UI.RunDialog`, which
makes it modal, so a key it does not handle does nothing rather than reaching the log view below;
register a letter with `RegisterLetterHandler`, which takes both cases, as its menu writes it.

- **`Cui/Diff/`** — `DiffService` turns a `CommitDiff` into `DiffRows` (the only user of DiffPlex).
  `DiffContext` holds the `--unified=<n>` levels the `+`/`-` keys step through; `WholeFile` is a
  context larger than any file, since git has no "all". `DiffReload` is a delegate the view is given
  rather than a commit id: it is opened from six places using five different git commands, and
  assuming an id is why refreshing a stash, a range or a file history diff fetched the wrong thing.
- **`Cui/Blame/`** — `BlameService` turns a `Server.Blame` into `BlameRows`, aggregating consecutive
  lines from one commit into the runs the gutter bracket draws. `BlameColumns` is the gutter width
  math, and steps detail down as the terminal narrows.
- **`Cui/Conflict/`** — the two/three-pane resolver. `ConflictResolution` holds what the user decided
  per hunk; it never builds file text, the decisions go back down to be applied. `IConflictView.Show`
  is deliberately **synchronous** — a Terminal.Gui `SynchronizationContext` posts an await's
  continuation to the main loop, so awaiting there would deadlock rather than merely stall — which is
  why the caller (`DiffView`) loads the file.

Below the UI, `Git/ConflictFile.cs` is the model (a `FileLine` keeps its own terminator, so a file
round trips byte for byte however it is written), `ConflictParser` parses and re-emits it, and
`ConflictService` runs the git side — abort / continue / skip a stopped merge, rebase, cherry-pick,
revert or am. The parser's invariant is `ToText(Parse(x)) == x` byte for byte, for every shape a file
can have (mixed line endings, no final newline, a BOM, all three conflict styles); everything the
resolver writes goes through it, so that identity is what stops resolving one conflict from
rewriting the rest of the file.

Layering note: the arrow is clean — nothing below `gmd/Cui/` references it, and nothing below
`gmd/Cui/` references Terminal.Gui (`gmd/Program.cs`, the entry point, does — it is the composition
root, above the layers). The one thing a lower layer needs from the UI is its main loop, and that
goes through `IMainThread` (`gmd/Utils/IMainThread.cs`): `Post` to raise an event on the UI thread,
`RunPeriodically` for a timer, implemented by `MainThread` (`Cui/Common/`) over `UI`. `FileMonitor`
is its only user. Keep it that way: if a lower layer needs something else from the UI, widen that
interface rather than reaching up.

### Dependency injection

Autofac, configured in `gmd/Utils/DependencyInjection.cs`. `RegisterAllAssemblyTypes()`
scans the whole assembly and registers every type `AsSelf().AsImplementedInterfaces()`.
Consequences to remember:

- **New classes are auto-registered** — no registration code to write. Add a constructor
  that takes the interfaces you need.
- Mark a type `[SingleInstance]` (the attribute in `DependencyInjection.cs`) for singletons.
  Services holding state or events (`Git`, `Server`, `AugmentedService`, `Config`) do. Everything
  else is a new instance per consumer, so a stateful type several classes share (`BranchNameService`,
  whose parse cache three pipeline stages read) silently becomes one copy each unless it is marked —
  and `RepoBuilder` wires such classes by hand, so the tests do not catch it.
- Internal and non-public constructors are found via a custom `DefaultConstructorFinder`,
  so `internal` constructors are fine.
- Because registration is by convention, a type implementing an interface that already has
  an implementation will silently take over the resolution — check for an existing
  implementer before adding one.

## Conventions

### Errors: `Result` / `Result<T>`, a union matched on its case types; not exceptions

`gmd/Utils/Result.cs` defines the result type every fallible operation returns: `Result<T>` is a union
of the value `T` and an `Error`, and `Result` one of `Success` and `Error`. Both are custom unions in the
C# 15 sense (`[Union]`, a constructor per case type, an `object? Value`), so a `switch` over one is
exhaustive with its two arms (a missing arm is a build error, CS8509) and a pattern applies to the
contained value. Exceptions are for bugs, not for control flow.

```csharp
// Propagate, with a value: the pattern binds it, and the error is returned when there is none
var result = await git.GetStatusAsync(wd);
if (result is not Status status) return result.Error;

// Propagate, no value
if (await git.SetValueAsync(key, json, wd) is Error e) return e;

// Branch on the outcome: exhaustive, no discard arm
return await server.PullAsync(name, wd) switch
{
    Success => Result.Ok,
    Error e => new Error("Failed to pull", e),
};

// A fallback instead of an error
var tags = await git.GetTagsAsync(wd) is IReadOnlyList<Tag> t ? t : [];

// Create and wrap errors; the constructor records the caller's file and line as Origin
return new Error($"Folder missing: {path}");
return new Error("Failed to merge", inner: e);

// A throwing API, e.g. a file call, becomes a result at the boundary
if (Result.Catch(() => File.Move(src, dst)) is Error e) return e;

// Returning: implicit conversions mean you just return the value or the error
return commits;                       // → Result<IReadOnlyList<Commit>>
return Result.Ok;                     // → Result
```

Things to know:

- `result.Error` is the one accessor that trusts the caller: it throws a `ResultException` on a
  value, so use it only right after a pattern ruled the value out, as above. That exception is
  what every misuse of a result throws, and `Result.Catch` lets it through, since it is a bug and
  not a failure of the API being guarded. A pattern variable declared in an `if` condition is
  scoped to the enclosing block, so a method with several guards names its errors (`pushError`,
  `deleteError`) rather than reusing `e`.
- **Name the exact type in the pattern.** `Commit`, `Status`, `ConflictFile` and friends exist in
  both `gmd.Git` and `gmd.Server`, and a pattern naming the wrong twin compiles and never matches.
  Spell the type as the file already does (`Git.Commit`, `Server.Repo`, the `GitStatus` alias).
- `Value` is the compiler's window into the union, not an accessor: every pattern on a result is
  lowered to a pattern on it, so it returns the `Error` too and never throws. Match on the result
  rather than read it. The one exception is generic code, where `result is T value` is refused
  (CS8780: with a type parameter the compiler cannot tell the struct from its contents), so match
  `result.Value is T value` there. The spec's optional `HasValue` / `TryGetValue` members are left
  out, since they only pay off for a union that keeps value types unboxed.
- An `Error` wraps at most one thing, another `Error` or an exception, so `AllMessages()` is this
  message followed by the wrapped one's messages, outermost first.
- A tuple cannot be bound by a union pattern that declares a variable, so a result carries a small
  record instead (`CloneInfo`, `UpdateAvailability`, `BlameHeader`).
- `ICmd` returns `Result<string>`; a failed command is a `CmdError` with the exit code and both outputs,
  matched as `result is CmdError e && e.Output.Contains("CONFLICT")`. Its `Origin` is the method that
  ran the command, passed down through `ICmd` as caller info. `RunRawAsync` is for the few commands
  whose non-zero exit is an answer rather than a failure.
- A command that does not run for a reason that is no failure (nothing to push, changes to commit
  first) returns a `Notice` (`Cui/Common/StatusLine.cs`), an `Error` the command runner shows on
  the status line at the bottom of the log view rather than in an error box. What a command did is
  said there too, with `IStatusLine.Info`; a key that cannot act says why rather than doing nothing.
- A git command that stops on conflicts returns a `ConflictError` (`Git/ConflictError.cs`, made
  with `ConflictError.ToConflict`). The command runner finds it however deeply it is wrapped,
  refreshes, and shows `RepoCommands.ShowConflicts`, the files and the way on, not the error.
- There is no conversion to `bool`, so `Result<bool>` is a value like any other. `default(Result<T>)` holds
  nothing and matches neither arm, and converting one to `Result` throws rather than passing it off
  as a success.
- The attribute the compiler recognizes the union by is polyfilled in `gmd/Utils/UnionPolyfill.cs`
  while the target framework is net10.0; the C# 15 compiler comes from the .NET 11 SDK pinned in
  `global.json` (MODERNIZATION.md has the GA step).

### Formatting: CSharpier owns it

**Never hand-format C#, and never argue with the formatter.** [CSharpier](https://csharpier.com)
is the single source of truth for layout (line breaks, spacing, wrapping, `using` order), and
it also formats `.csproj` files. Settings are in `.csharpierrc` (`printWidth` is **120** — the
default 100 would explode this codebase's one-line delegation methods into one parameter per
line); `.csharpierignore` and `.gitignore` are both honored, so `obj/`/`bin/` are skipped.
`endOfLine` is `lf`, and `.gitattributes` checks out LF on every platform, because a raw string
literal keeps the line endings of its source file: with a CRLF checkout every multi-line expected
value in the tests becomes `\r\n`, and some 80 of them fail on Windows. A Debug build repairs such
a tree, since the format pass rewrites the endings before compiling.

It runs in four places: on save in VS Code (`editor.formatOnSave` + the `csharpier-vscode`
extension), on build (the `CSharpier.MsBuild` package in both `.csproj` files), on commit
(`.git/hooks/pre-commit`, from `gmd/tools/pre-commit-sample`), and in CI. The MSBuild integration
behaves differently per configuration, which matters:

- **Debug** — *formats* the sources in place before compiling. A `dotnet build` can therefore
  modify files in the working tree. This is intended.
- **Release** — *checks* only, and **fails the build** if anything is unformatted. This is why
  CI runs an explicit `csharpier check` step *before* `./build`: `./build` runs `dotnet test`
  (Debug) first, which would silently format everything and hide the problem from the Release
  check.
- Escape hatch: `dotnet build -p:CSharpier_Bypass=true`.

CSharpier is a local dotnet tool pinned in `.config/dotnet-tools.json`; run `dotnet tool restore`
if `dotnet csharpier` is not found, then `dotnet csharpier format .` / `check .`.
`.editorconfig` deliberately contains **no** whitespace or wrapping rules — only naming rules
and non-layout code style, so it cannot conflict with CSharpier. C# raw string literals
(`$"""…"""`) are indentation-normalized against their closing delimiter, so re-indenting one does
not change the string's value.

### Style as found in this codebase

- Namespaces are file-scoped (`namespace gmd.Git.Private;`), one type family per file, and
  `interface IFoo` usually sits directly above `class Foo` in the same file.
- Fields are `readonly` and *not* prefixed (`readonly IGit git;`), assigned in a plain
  constructor — no primary constructors.
- **Collection expressions (`[]`) are preferred for new and touched code** — `List<string> x = [];`
  and `return [];` rather than `new List<string>()` / `new string[0]`. Most of the codebase
  predates C# 12 and still uses the old form; it is being migrated gradually rather than in one
  sweep, so expect both styles to coexist. The relevant analyzers (IDE0028, IDE0300–IDE0305) are
  enabled as suggestions in `.editorconfig`, so the IDE will point out remaining sites as you work.
- Expression-bodied one-line members are used heavily for delegation (see `Git.cs`).
- Nullable reference types and implicit usings are **on**; `gmd/Usings.cs` holds the global
  usings and `[assembly: InternalsVisibleTo("gmdTest")]` (so tests can reach `internal` types, and
  the same for `gmdE2eTest`).
- Every git-facing method takes a trailing `string wd` — the repo working directory. It is
  threaded through explicitly rather than stored; keep doing that.
- Async methods end in `Async` and return `Task<Result<...>>`. `RunInBackground()`
  (`Utils/TaskExtensions.cs`) is the fire-and-forget helper; on a `Task<Result>` or
  `Task<Result<T>>` it logs an error result as a warning, so a task whose failure is expected
  handles its own result instead, as the background fetch in `RepoView` does.
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

MSTest 4.x + coverlet in `gmdTest/`, mirroring the `gmd/` folder layout — put a test at the path
mirroring its subject, e.g. `gmdTest/Server/Private/Augmented/Private/AugmenterTest.cs`. Tests that
need a real repository use `TempRepo`; **never** run git against this working tree. Growing this
suite is an explicit goal — see the open issues in `MODERNIZATION.md`.

The one exception to that layout is the end-to-end tier, which is a project of its own,
`gmdE2eTest/`, so that it can run in parallel (see `TmuxSession` below). It compiles the fixtures it
shares with `gmdTest` (`TempRepo`, `TempHome`, `Proc`, `ResultAssert`) as linked files rather than
copies, so they stay in `gmdTest/Fixtures/`. `./test` runs both projects. Inside it the same rule
holds: one class per area of the app, placed as the code it reaches is in `gmd/Cui/` —
`Cui/Diff/DiffViewTest.cs`, `Cui/RepoView/BranchTest.cs`, `Cui/WorktreeTest.cs` and so on. The
`Integration` and `E2e` categories are set once for the whole assembly in its `TestSetup.cs`, so a new
class needs nothing but `[TestClass]` to be kept out of the fast run.

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
that set branch colors or branch order. (`FakeMainThread` is separate — it stands in for `UI` in
`FileMonitorTest`, which drives the timer tick by hand.)

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
var commits = AssertOk(await log.GetLogAsync(100, "/wd"));
StringAssert.Contains(cmd.Calls[0].Args, "--max-count=100");
```

**`TempRepo`** (`gmdTest/Fixtures/`) is the opposite end: a throwaway repository in the system
temp folder, driven through the real `git` executable and the real `IGit` services. It is the
canary for git version and output-format drift, which canned output cannot catch, so keep these
few and small — `FakeCmd` is the right tool for anything about parsing:

```csharp
using var repo = await TempRepo.CreateAsync();      // 'main', local config set, no commits yet
var c1 = await repo.CommitFileAsync("file.txt", "text\n", "Initial");
AssertOk(await repo.Git.CreateBranchAsync("dev", true, repo.Path));
await repo.AddOriginAsync();                        // a bare repo next door, for push/fetch
await repo.GitAsync("reset --hard HEAD~1");         // raw git, for what IGit has no method for
```

The repository is deleted on `Dispose`, and nothing outside its temp folder is ever touched —
`Dispose` refuses to delete a path it did not create. `GitIntegrationTest` and
`AugmentedServiceIntegrationTest` both carry `[TestCategory("Integration")]`, which is what the
fast filter in Commands excludes.

`CommitFileAtAsync` / `CommitAtAsync` / `GitAt` pin the author *and* committer dates. Use them for
any fixture whose drawn output is asserted: they fix the time column, make the commit ids
reproducible (a commit object is just its tree, parents, identity, dates and message), and — the
part that is not cosmetic — remove the row-order flake, since `git log --all --date-order` orders by
commit date and has nothing to break a tie with. They go around `IGit` because no `IGit` method takes
environment variables, and `GIT_COMMITTER_DATE` is the only way to set a committer date.

**`TmuxSession`** (`gmdE2eTest/Fixtures/`) is the end-to-end tier: the built binary, real git, a real
pty. tmux keeps a screen model, so `capture-pane` gives back the rendered screen, and that is what
is asserted — the drawing, the layout, the key dispatch and the dialogs, none of which anything
else in the suite reaches. It names no Terminal.Gui type, deliberately, so it is as valid against a
2.x build as a 1.x one. `ScreenText` normalizes a capture, `TempHome` isolates the home directory
and `E2eRepo` builds the fixture repo:

```csharp
using var repo = await E2eRepo.CreateAsync();
using var gmd = TmuxSession.StartGmd(repo);            // 120x40, hermetic env, updater off
ScreenText.AssertEqual("""<a picture of the screen>""", gmd.WaitFor("Initial"), repo.Path);
gmd.Send("Enter");                                     // a key; SendText types into a dialog
gmd.WaitUntilGone("Gmd Help Guide");                   // i.e. "the dialog closed"
```

Colors are assertable too: `gmd.CaptureColors()` keeps them as ANSI, and `ScreenText.ColorsOf` /
`ColorRows` / `BackgroundRows` turn that into one letter per cell lined up under the text, exactly
as `GraphText.ColorsOf` does for the graph column — uppercase for a normal color, lowercase for its
bright variant (`M` magenta, `m` bright magenta, `W` white, `D` dark, `.` black). `BackgroundRows`
is how the current row's highlight is reached, that being a background rather than a foreground.
So is the cursor: `gmd.IsCursorVisible` and `gmd.CursorPosition` come from tmux's pane state, which
is how "the caret is back in the text field after the menu closed" is asserted.

Run them with `./test --filter "TestCategory=E2e"`; they also carry `Integration`, so the fast
filter above excludes them. They run **in parallel**, eight at a time (`[assembly: Parallelize]` in
`gmdE2eTest/TestSetup.cs`), which took the tier from about four minutes to about thirty seconds —
a test is nearly all waiting for a screen to settle, not CPU. What bounds a run now is its longest
test, the thirty second worktree re-read. `-- MSTest.Parallelize.Workers=1` after the other
arguments runs them one at a time again. Eight things they do that matter, and that a new test must
keep doing:

- **A throwaway `$HOME` per session**, seeded with `CheckUpdates: false` — see the `HOME` paragraph
  under "Running the TUI from a non-interactive shell" for why both halves are mandatory. It also
  seeds `ShowKeyHints: false`: the key-hint line is the bottom row, so with it on every snapshot of a
  whole screen would carry thirty blank rows and the hints. `StartGmd(..., isKeyHints: true)` turns
  it on, for the tests about it (`KeyHintTest`).
- **An empty `DISPLAY`, `WAYLAND_DISPLAY` and `WSL_DISTRO_NAME`**, so gmd finds no clipboard tool it
  can reach and copies through the terminal instead (OSC 52). `set-clipboard on` then makes tmux
  keep the sequence as a buffer, which `gmd.Clipboard()` reads back — the only way to assert a copy
  — and a copy on a developer's desktop no longer overwrites their real clipboard.
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
- **Pinned dates when the test lets gmd commit.** `StartGmd(repo, commitTime: …)` puts
  `GIT_AUTHOR_DATE`/`GIT_COMMITTER_DATE` into gmd's environment, which the `git` it shells out to
  inherits, so the commit it makes has a fixed sha and time and is asserted rather than masked —
  what `CommitFileAtAsync` does for fixture commits. Opt in per test: it pins every commit of that
  session to one second, and two of those have nothing to order them by. `E2eRepo` has
  `CreateWithChangesAsync` for a working tree with something to commit; the uncommitted row's own
  time is `DateTime.Now`, so that one row goes through `ScreenText.MaskTimes`.
- **Nothing shared with the other tests**, since several run at once: everything a test touches is
  its own repo, HOME and tmux server, and it changes nothing process-wide — no culture, environment
  variable or current directory. Waiting blocks a thread pool thread for the whole test, which is
  why `TestSetup` raises the pool's minimum; without that, six workers on two cores starved the pool
  and a run took ten minutes.

Seven traps worth knowing before adding one:

- **`Escape` in the log view asks "Quit gmd?", with Yes as the default** — never send a "safety"
  Escape: it leaves the question up, and the next `Enter` quits.
- A modal dialog is drawn *over* the log view rather than replacing it, so the rows behind it still
  match whatever `WaitFor` is looking for. Use `WaitUntilGone` to mean "closed".
- **A status message is drawn over the bottom row for five seconds** after a push, a pull, or a key
  that could not act, since the key hints are off: a whole-screen snapshot taken then has thirty
  blank rows and the message in it. Compare the log with `ScreenText.Rows` and the message with
  `ScreenText.LastLine`, as `PushPullTest` does.
- For the keys that act on the hoovered branch (`s`, `e`, `b`, `m`, `h`, `g`, and `p` / `u`, which
  act on the current branch when nothing is hoovered), **the application bar does not tell you what
  the hoover is on** (the key-hint line does, by name, but it is off in these tests) — it is set both by the hoover and by the current row's
  branch, so an operation that moves the row leaves it naming the wrong one. Press `m` and read the
  `Branch: <name>` menu title; that is the only readout from outside. And expect the hoover to stay
  where it was after a command rather than follow what appeared: after `Enter` opens a branch it is
  still on the branch it was on, which is why `s` straight after looks like a dropped keystroke.
- **A letter sent to an open menu picks the item showing it** (`MenuShortcuts`), as Enter would.
  Drive a menu by the arrows and Enter, or by the letter on purpose, never by typing into it.
- **One key per `Send` when driving a menu**, with a `WaitForStable` after each. `Send("Down",
  "Down", …)` in one call loses keys — a menu redraw drops whatever was sent behind it, so five
  arrived as three, and a miscounted menu runs the wrong command. Same "never send a key into a
  screen that has not settled" rule, and it applies even though no git command is running. It is not
  only menus: in the log view, ten `Down`s sent in one call moved the cursor five rows.
- **Count menu moves against the *fixture*, not the menu source.** `OnCursorDown` skips disabled
  items, so the same item is a different number of moves in a repo with a remote than in one
  without. `BranchTest.TestRenameBranch` is five moves for that reason.

When a snapshot disagrees, `AssertEqual` prints the actual screen ready to paste back in, and
`GMD_E2E_KEEP=1` leaves the session up to attach to.

The same machinery records the README's animation, `gmd/doc/Animation.gif`, which `./demo`
re-records. `gmdE2eTest/Demo/DemoTest.cs` is the script: an end-to-end test in all but asserting,
run only when `./demo` names a cast file for it (skipped otherwise), on `DemoRepo`, a repository
made to look like a team's. `DemoRecording` turns the settled screens into an asciicast, each shown
for as long as the script says rather than as long as it took, and `./demo` renders that with agg,
which `./installtools` installs with its fonts. Everything is pinned, including what would differ
between runs on screen (the temp path, the uncommitted row's `DateTime.Now`), so two recordings are
byte for byte the same and the GIF only changes when what gmd draws does. When gmd's UI changes in
a way the demo passes through, re-record it; when a step of the script goes wrong, `CAST=<path>
./demo` keeps the cast, and `agg --select marker:<label>` renders the frame of one step.

Other things to know:

- `gmdTest/Usings.cs` provides the global usings (`Assert`, `Log`, and `AssertOk` / `AssertError`
  from `gmdTest/Fixtures/ResultAssert.cs`, which return the value or the `Error` they assert), and
  `internal` types are visible to tests, so services can be constructed directly
  (`new BranchNameService()`) — no DI.
- **The test process runs under a throwaway `$HOME`**, set by `gmdTest/TestSetup.cs`
  (`[AssemblyInitialize]`) before anything can log — otherwise `./test` truncates the developer's
  `~/gmd.log`, since any test running a git command goes through `Cmd`, which logs, and
  `ConfigLogger` truncates on first use. `~/gmd.log` during a run is at `/tmp/gmdTest-home-*/gmd.log`.
  `gmdE2eTest/TestSetup.cs` does the same for its own process, since each test assembly runs its own
  `[AssemblyInitialize]`.
- The whole inference chain is constructible by hand and touches no git, disk or terminal:
  `Augmenter` → `BranchStructureService` → its three stage services → `BranchNameService`.
  `RepoBuilder.NewAugmenter()` wires the lot up, so use that rather than repeating it.
- Anything that *draws* needs a driver; constructing and driving a view does not. `ContentViewTest`
  builds a real `ContentView`, sets its `Frame` (which is where its height comes from) and exercises
  everything on it except drawing. Keep logic out of the view classes so it stays reachable this way
  — that is why `ContentScroll`, `ContentSelection`, `Hoover`, `KeyHints`, `BranchFinder`,
  `MenuDimensions`, `MenuRows`, `MenuShortcuts`, `BlameColumns` and `ConflictResolution` exist. `Text.ToString()` flattens styled output to a plain
  string, which is how `GraphText` snapshots `GraphWriter` output with no driver at all.
- Terminal.Gui ships a public `FakeDriver` that works headlessly, so drawing *is* testable without a
  terminal — not adopted by the suite yet; see the headless-drawing note in `MODERNIZATION.md` first.
- `gmdTest` runs sequentially (no `.runsettings`), and has to: run in parallel, 4 of 15 runs failed.
  `LogServiceTest` and `TimeDateExtensionsTest` change `CultureInfo.DefaultThreadCurrentCulture`, and
  `GitIntegrationTest` sets `GIT_EDITOR`. MSTest sets parallelism per assembly (`[Parallelize]` has no
  class form), which is why the end-to-end tests, which can run in parallel, are a project of their own.

Always run `./test` before reporting work done. Prefer adding a regression test with every
bug fix — that is the agreed direction for this repo. When the subject is a parser or the
inference pipeline, write the failing test first; both have already hidden real bugs.

For the inference pipeline the tests are **characterization** tests: they pin down what the code
actually does, not what it ought to do. Do not guess the expected values — discover them, then
assert. The quickest way is a throwaway test that dumps the result and fails
(`Assert.Fail(dumpOfEveryCommitAndBranch)`), read the real output, write the assertions, delete the
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
- **No git process gmd starts can open an editor.** `Cmd.NeverOpenAnEditor` forces `GIT_EDITOR`
  and `GIT_SEQUENCE_EDITOR` on every process started through `Cmd.Command`, which is every git
  call, because a `rebase --continue` opening the user's editor would hang gmd behind the terminal
  it owns. It is done there and not per command line because `GIT_EDITOR` beats
  `-c core.editor=…`, so a flag is silently ineffective for any user who has that set.
- **`gmdSetup.exe` is a prebuilt binary committed to the repo**
  (`gmd/Installation/installer/`). Neither `./build` nor CI builds the Inno Setup installer;
  CI just uploads the committed file. Rebuilding it requires Windows + `BuildSetup.bat`.
- **The `gmd_linux` release asset is a duplicate of `gmd_linux_x64`** kept under the original
  name because the built-in updater falls back to it (`gmd/Installation/Updater.cs`). Do not
  drop it from the release workflow.
- Branch layout: `main` = releases, `dev` = pre-releases; pushing to either publishes a
  GitHub release from CI. Work on feature branches and target `dev` unless told otherwise.
- `.git-blame-ignore-revs` lists the bulk reformat commits; `./installtools` points
  `blame.ignoreRevsFile` at it.
- `Utils/GlobPatterns/` is vendored third-party-style code. CSharpier formats it like
  everything else, but do not restructure its logic; `.editorconfig` keeps analyzers quiet there.

## Working agreements

- **`MODERNIZATION.md` holds the open issues and the findings** from the modernization work: what
  is deferred and why, what is known to be wrong, and the git and Terminal.Gui traps met on the way.
  Read it before starting anything substantial, and add to it (or close items) as work lands.
- **`USABILITY.md` is the usability review**: the findings by principle (safety, discoverability,
  consistency, feedback, workflow fit) and the ranked proposals. Check a new command or key against
  it — above all, that a slip of the finger cannot push, pull or lose work.
- Modernizing this codebase, fixing bugs, adding tests and improving maintainability is the
  active goal — but keep changes reviewable. Prefer a series of focused commits over one
  sweeping refactor, especially around `BranchStructureService` and `RepoView`.
- Do not commit or push unless asked.
- When behavior visible to users changes, check whether `gmd/doc/help.md` (embedded into the
  binary as a resource) needs updating too.
