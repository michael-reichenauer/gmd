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

[CLAUDE.md](../CLAUDE.md) describes the architecture, the conventions and the tests in depth. It is
written for Claude Code but is just as useful to read yourself. The working notes are in `.notes/`:
[MODERNIZATION.md](../.notes/MODERNIZATION.md) lists the open issues, and
[USABILITY.md](../.notes/USABILITY.md) is a review of the user experience with proposals for improving
it.

## Setting up

The easiest way is the devcontainer, locally in VS Code with Docker or in GitHub Codespaces. It
installs both .NET SDKs, and `scripts/installtools` then adds the tools that the scripts and the
end-to-end tests use (tmux, lnav, agg). It has the GitHub CLI too, which reads the CI runs
(`gh run view <id> --log-failed`): log it in once with `gh auth login`, and the login is kept in a
volume across rebuilds.

To set up a machine yourself you need:

- The **.NET 11 SDK**, a preview until .NET 11 is released and pinned in `global.json`. Its C# 15
  compiler is what the `Result` union type needs. [UPGRADING.md](UPGRADING.md) has the steps for
  when .NET 11 is released.
- The **.NET 10 runtime**, since gmd targets `net10.0` and the tests run on it.
- **git**, and **tmux** for the end-to-end tests.

## Scripts

They are in `scripts/`, and each runs at the repository root wherever it is started from.

| Script                   | What it does                                                               |
| ------------------------ | -------------------------------------------------------------------------- |
| `scripts/run [args]`     | Run gmd from source, on this repository (`-d <folder>` opens another)      |
| `scripts/test`           | Run all tests (about a minute and a half); `--filter "TestCategory!=Integration"` runs only the fast ones (a few seconds) |
| `scripts/build`          | Run the tests, audit the packages and publish the executables for every platform |
| `scripts/build -l`       | The same, but publish only for Linux (x64 and arm64), which is much faster |
| `scripts/log`            | Follow gmd's runtime log, `~/gmd.log`, in lnav                             |
| `scripts/updatepackages` | List outdated NuGet packages (`-u` upgrades minor versions, `-m` major versions too) |
| `scripts/installtools`   | Set up the devcontainer: tools and dotnet local tools                      |
| `scripts/demo`           | Re-record the README's animation, by running a scripted session in tmux    |
| `scripts/brewformula <version>` | Write the Homebrew formula of a release, which the release workflow pushes to the tap |

On Windows, `scripts\run.bat`, `scripts\build.bat` (`-w` builds Windows only) and `scripts\log.bat`
do the same.

`scripts/build` publishes each platform as a self-contained, single-file executable, for example:

```bash
dotnet publish gmd/gmd.csproj -c Release -r linux-x64 -p:PublishReadyToRun=true --self-contained true -p:PublishSingleFile=true
```

The executable ends up in `gmd/bin/Release/net10.0/<runtime>/publish/`, and `scripts/build` copies
each one into `artifacts/` under the name it is released as. The runtimes released are
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
- **The help** is [gmd/doc/help.md](../gmd/doc/help.md), which the `?` key opens on GitHub, at the
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
`env -u SSH_ASKPASS -u GIT_ASKPASS scripts/run -d /tmp/gmd-login/repo`:

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
6. `Shift-→` opens Show Branch, and `Shift-↑` / `Shift-↓` select rows in the log. macOS Terminal
   sends the last two as plain arrows until its profile maps them to `\033[1;2A` / `\033[1;2B` (the
   *Keys* section of the help says how); `cat -v` shows what a key sends.
7. `i` copies the commit id and `Shift-I` its message: paste them somewhere else.
8. In a repository on GitHub, the commit menu's Open Commit in Browser opens it in the browser.
9. With an ssh key that has a passphrase and is not in the ssh agent, a push or `r` asks for the
   passphrase in a dialog (the Windows side is untried: gmd.exe as ssh's askpass, and a named pipe).
10. `?` opens the help in the browser, and in the diff view at *Diff and Blame*; over ssh it copies the link.
11. A terminal of 80 by 24: the log, the commit dialog and the menus are whole on the screen.

What this list does not cover waits for reports.

## The Homebrew tap

`brew install michael-reichenauer/tap/gmd` installs gmd from the tap, the repository
[michael-reichenauer/homebrew-tap](https://github.com/michael-reichenauer/homebrew-tap), whose
`Formula/gmd.rb` installs the release's binaries as they are. For every release from `main`, the
release workflow writes the formula with `scripts/brewformula` and pushes it there (*Update the
Homebrew tap*), with a deploy key of the tap that this repository holds as the secret
`HOMEBREW_TAP_KEY`. Previews are not put there. A gmd installed from the tap is updated by
Homebrew rather than by itself (`gmd/Installation/PackageManager.cs`).

To set up the key, or to replace it:

1. Make a key pair with no passphrase: `ssh-keygen -t ed25519 -N "" -C "gmd release workflow" -f tap_key`.
2. In the tap, *Settings → Deploy keys → Add deploy key*: paste `tap_key.pub` and tick *Allow
   write access*.
3. In this repository, *Settings → Secrets and variables → Actions → New repository secret*: name
   it `HOMEBREW_TAP_KEY` and paste the whole of `tap_key`. Then delete both files.

Without the secret, the step warns and the release goes on. To put a release in the tap by hand,
e.g. the first, run `scripts/brewformula <version> > Formula/gmd.rb` in a clone of the tap, then
commit and push.

To try a change to the formula before a release puts it there, install from a clone of the tap with
the change committed:

```bash
brew untap --force michael-reichenauer/tap   # --force uninstalls gmd too; untap refuses without it
brew tap michael-reichenauer/tap ~/src/homebrew-tap
brew install michael-reichenauer/tap/gmd && brew test gmd
brew info --json=v2 --variations michael-reichenauer/tap/gmd   # the download on every OS and CPU
```

Then untap it the same way and `brew tap michael-reichenauer/tap` with no path, to be back on GitHub's.

## The dev container feature

`ghcr.io/michael-reichenauer/gmd/gmd`, which a `devcontainer.json` names in its `"features"`, is
`devcontainer-feature/src/gmd/`: `devcontainer-feature.json`, its metadata and version, and
`install.sh`, which installs the latest release when the container is built. It puts the binary in
the container user's `~/.local/share/gmd` and links `/usr/local/bin/gmd` to it, so that gmd can
update itself there: in the home, since on a Linux host the user's id is changed to the host user's
as the container starts, and only the home is given the new id. On Debian and Ubuntu it installs what
gmd needs that the image lacks.

The workflow *Dev container feature* (`.github/workflows/devcontainer-feature.yml`) runs on every
push that changes the feature. It builds a container with the feature on a full and a bare image,
x64 and arm64, and runs `devcontainer-feature/test/gmd/test.sh` in each. From `main` it then
publishes the feature, at the version in `devcontainer-feature.json`, so a change of the feature is
published only with a new version there.

The package is linked to this repository, whose workflow published it, so it is public as the
repository is: no step by hand is needed, and anyone can fetch it with no login (checked when 1.8
first published it).

To try a change before it is pushed, with Docker:

```bash
npx @devcontainers/cli@0.89.0 features test --project-folder devcontainer-feature --features gmd --base-image debian:bookworm-slim
```
