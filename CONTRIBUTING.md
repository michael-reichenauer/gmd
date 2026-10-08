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
written for Claude Code but is just as useful to read yourself. [MODERNIZATION.md](docs/MODERNIZATION.md)
lists the open issues, and [USABILITY.md](docs/USABILITY.md) is a review of the user experience with
proposals for improving it.

## Setting up

The easiest way is the devcontainer, locally in VS Code with Docker or in GitHub Codespaces. It
installs both .NET SDKs, and `./installtools` then adds the tools that the scripts and the
end-to-end tests use (tmux, lnav, agg).

To set up a machine yourself you need:

- The **.NET 11 SDK**, a preview until .NET 11 is released and pinned in `global.json`. Its C# 15
  compiler is what the `Result` union type needs. [UPGRADING.md](docs/UPGRADING.md) has the steps for
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
  `dev` afterwards. A version raised by hand in `gmd/Program.cs`, a new major, is released as it is
  written. Work on a feature branch and target `dev`.
- **Tests** go with every bug fix. The unit tests are in `gmdTest/`, laid out like `gmd/`, and the
  end-to-end tests, which drive the real executable in tmux, are in `gmdE2eTest/`.
- **`CHANGELOG.md` is generated** from the git history by `gmd --updatechangelog`, in CI's release
  commit on `main`, so don't edit it by hand.
- **The help** is [gmd/doc/help.md](gmd/doc/help.md), which the `?` key opens on GitHub, at the
  commit gmd was built from (`Project.HelpUrl`). Update it with any change users can see. The diff,
  blame and conflict views open it at a heading, and its contents link to its headings, so a heading
  renamed must be renamed there too; a test checks both.

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

## Checking a platform by hand

The end-to-end tests run gmd in tmux on Linux, so what only another platform or terminal shows is
checked by hand. Most of the drawing (the graph, `Ϙ`, `ß`, `©`, `◙`, `□`, `┅`, the arrows) was checked
on every platform before the summer of 2026; this list is what has come since, or never was. Note
for each what you see, OK or a screenshot.

Symbols that may be missing from a font, shown as a box or a `?` when they are:

1. `⇧`, for Shift: the key-hint line at the bottom (`⇧→ show branch`), any menu (`m`: Copy Commit
   Message `⇧i`), and the status line after a key that cannot act.
2. `✦`: the top bar, on opening a repository with branches that are hidden (`✦2`).
3. `⌂`: the log, beside a branch checked out in another worktree (`git worktree add ../wt -b wt`,
   then show `wt` with `Shift-→`).
4. `·`: the top bar while a merge or rebase is stopped on a conflict (`Merging 'dev' · 1 conflict`).

Keys and features that depend on the terminal or the system:

5. The commit dialog (`c`): `Tab` into the file list, `Space` unticks a file, `a` all, and `Alt-O`
   commits. On macOS, Alt keys need *Use Option as Meta key* (Terminal) or *Esc+* for the Option key
   (iTerm2); without it `Alt-O` types `ø`.
6. `Shift-→` opens Show Branch, and `Shift-↑` / `Shift-↓` select rows in the log.
7. `i` copies the commit id and `Shift-I` its message: paste them somewhere else.
8. In a repository on GitHub, the commit menu's Open Commit in Browser opens it in the browser.
9. With an ssh key that has a passphrase and is not in the ssh agent, a push or `r` asks for the
   passphrase in a dialog (the Windows side is untried: gmd.exe as ssh's askpass, and a named pipe).
10. `?` opens the help in the browser, and in the diff view at *Diff and Blame*; over ssh it copies the link.
11. A terminal of 80 by 24: the log, the commit dialog and the menus are whole on the screen.

What this list does not cover waits for reports.
