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

## Trying the login dialog

Gmd asks git's questions, a passphrase, a password or whether to trust a host, in a dialog. Few
setups ever meet one: an ssh agent or a credential helper answers first, and in VS Code's terminal
its own `GIT_ASKPASS` and credential helper do. To see the dialog anyway, make a repository whose
`origin` is served by a stand-in for ssh that asks for a passphrase, as ssh does, through
`SSH_ASKPASS`:

```bash
dir=/tmp/gmd-login && rm -rf $dir && mkdir -p $dir && cd $dir

# A stand-in for ssh: asks through SSH_ASKPASS as ssh does, then serves git from origin.git
cat > ssh <<'END'
#!/bin/sh
[ "$1" = "-G" ] && exit 0
for last; do :; done
answer=$("$SSH_ASKPASS" "Enter passphrase for key '/home/you/.ssh/id_ed25519': ") || exit 255
[ "$answer" = "secret" ] || { echo "Permission denied (publickey)." >&2; exit 255; }
exec sh -c "git ${last#git-}"
END
chmod +x ssh

git init -q --bare origin.git
git init -q -b main repo && cd repo
git commit -q --allow-empty -m "First"
git remote add origin $dir/origin.git && git push -q -u origin main
git remote set-url origin ssh://git@example.invalid$dir/origin.git
git config core.sshCommand $dir/ssh
git commit -q --allow-empty -m "Second"
```

Then run gmd there with no askpass of its own in the way, e.g. from the gmd folder
`env -u SSH_ASKPASS -u GIT_ASKPASS ./run -d /tmp/gmd-login/repo`:

- On opening, the fetch in the background asks nothing; the status line says `r` would.
- `r` fetches and asks: `secret` is the passphrase, anything else is refused, and `Esc` cancels.
- `p` pushes `Second`, and asks the same.

For the other questions, change the stand-in's question: the four lines of ssh's
`The authenticity of host ...` question, answered `yes`, show the Yes or No dialog. Git's user name
and password are asked for over https: an https `origin` that needs a login, e.g. a private
repository on GitHub, with the credential helpers turned off for the repository
(`git config credential.helper ""`), asks for both, the password being a token on GitHub.
