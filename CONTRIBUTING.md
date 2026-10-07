# Contributing to Gmd

Gmd is written in C# for .NET 10 on [Terminal.Gui](https://github.com/gui-cs/Terminal.Gui) 1.x.
It runs the `git` command line for everything and uses no Git library. The code is layered, and
each layer calls only the one below it:

```text
gmd/Cui/                         Terminal.Gui views, dialogs, menus and the branch graph
gmd/Server/                      The repository model the UI shows, i.e. the branches you chose
gmd/Server/Private/Augmented/    Works out which branch each commit belongs to
gmd/Git/                         One service per area of git, each running the git command line
```

[CLAUDE.md](CLAUDE.md) describes the architecture, the conventions and the tests in depth. It is
written for Claude Code but is just as useful to read yourself. [MODERNIZATION.md](MODERNIZATION.md)
lists the open issues, and [USABILITY.md](USABILITY.md) is a review of the user experience with
proposals for improving it.

## Setting up

The easiest way is the devcontainer, locally in VS Code with Docker or in GitHub Codespaces. It
installs both .NET SDKs, and `./installtools` then adds the tools that the scripts and the
end-to-end tests use (tmux, lnav, agg).

To set up a machine yourself you need:

- The **.NET 11 SDK**, a preview until .NET 11 is released and pinned in `global.json`. Its C# 15
  compiler is what the `Result` union type needs. [UPGRADING.md](UPGRADING.md) has the steps for
  when .NET 11 is released.
- The **.NET 10 runtime**, since gmd targets `net10.0` and the tests run on it.
- **git**, and **tmux** for the end-to-end tests.

## Scripts

| Script             | What it does                                                               |
| ------------------ | -------------------------------------------------------------------------- |
| `./run [args]`     | Run gmd from source                                                        |
| `./test`           | Run all tests (about a minute and a half); `--filter "TestCategory!=Integration"` runs only the fast ones (a few seconds) |
| `./build`          | Run the tests, audit the packages and publish the executables for every platform |
| `./build -l`       | The same, but publish only for Linux (x64 and arm64), which is much faster |
| `./log`            | Follow gmd's runtime log, `~/gmd.log`, in lnav                             |
| `./updatepackages` | List outdated NuGet packages (`-u` upgrades minor versions, `-m` major versions too) |
| `./installtools`   | Set up the devcontainer: tools and dotnet local tools                      |
| `./demo`           | Re-record the README's animation, by running a scripted session in tmux    |

On Windows, `run.bat`, `build.bat` (`-w` builds Windows only) and `log.bat` do the same.

`./build` publishes each platform as a self-contained, single-file executable, for example:

```bash
dotnet publish gmd/gmd.csproj -c Release -r linux-x64 -p:PublishReadyToRun=true --self-contained true -p:PublishSingleFile=true
```

The executable ends up in `gmd/bin/Release/net10.0/<runtime>/publish/`. The runtimes released are
`linux-x64`, `linux-arm64`, `osx-arm64` and `win-x64`.

## Conventions

- **Formatting** belongs to [CSharpier](https://csharpier.com). VS Code formats on save, a Debug
  build formats the code, and CI checks it. Don't format by hand.
- **Branches**: `main` holds the releases and `dev` the pre-releases, and CI publishes a GitHub
  release on every push to either one. A release from `main` gets the next minor version, in a
  release commit CI adds to `main`, so pull `main` before merging into it and merge it back into
  `dev` afterwards. Work on a feature branch and target `dev`.
- **Tests** go with every bug fix. The unit tests are in `gmdTest/`, laid out like `gmd/`, and the
  end-to-end tests, which drive the real executable in tmux, are in `gmdE2eTest/`.
- **`CHANGELOG.md` is generated** from the git history by `gmd --updatechangelog`, in CI's release
  commit on `main`, so don't edit it by hand.
- **The help** the `?` key shows is [gmd/doc/help.md](gmd/doc/help.md), embedded in the
  executable. Update it with any change users can see, and keep its lines within 77 columns, which
  a test checks.
