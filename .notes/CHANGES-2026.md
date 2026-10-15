# Gmd in 2026: an overview of the changes

What changed in gmd this year, grouped by theme, the larger and the noticeable changes only. Work
resumed on 2026-07-25, after nothing since 2025-11-22, and this overview covers up to 2026-10-14.
All of it is released. The last release, 1.7 (2026-10-14), brought a review of every layer, which is
listed on its own at the end, and installing with Homebrew.

The detailed records are elsewhere, and this file points to them rather than repeating them:

- `CHANGELOG.md` lists every release (generated from the merge commit messages).
- `.notes/MODERNIZATION.md` has what was done to the code, every bug fixed, and the open issues.
- `.notes/USABILITY.md` has the usability review (2026-09-25), the product review (2026-10-07) and
  their proposals, with the ones done ticked off.
- `gmd/doc/help.md` describes every feature as a user sees it.

**In numbers**

| | Start of the year | 2026-10-14 |
| --- | --- | --- |
| Version | 0.91 | 1.7 (1.0 released 2026-10-08) |
| Releases from `main` this year | | 21 |
| Commits this year (not counting merges) | | about 485 |
| C# lines in `gmd/` | about 20,500 | about 40,900 |
| Test methods | 2 | about 1,810 (about 1,590 in `gmdTest`, about 220 end to end) |
| .NET | 8 | 10 (built with the .NET 11 SDK for C# 15) |

---

## Timeline

| When | Release | What happened |
| --- | --- | --- |
| 07-26 to 08-02 | | **Modernization**: CLAUDE.md, CSharpier, .NET 10, the first tests, the large classes split |
| 08-03 to 08-18 | 0.91.1388 | Terminal.Gui 1.19 (fixed the 100% CPU), end-to-end tests in tmux, copy rewritten, **blame**, **conflict resolver**, diff context, Merge Into, Rename Branch |
| 09-02 to 09-09 | 0.91.1406, 0.91.1410 | **Spell check** in the message fields, **worktrees** |
| 09-10 to 09-24 | 0.91.1425 | `Result` became a C# 15 union type, end-to-end tests run in parallel, the README animation is recorded by a script |
| 09-25 to 09-30 | 0.91.1429, 0.94 | **Usability review**: the safety fixes, the key-hint line, the status line, Find Branch, search, Open in Browser; the second round of the branch inference; refresh performance |
| 10-06 to 10-07 | 0.95 | **A way back**: Undo from the reflog, Recover Lost Commits, Restore Deleted Branch, force push handling; ✦ for the news on hidden branches |
| 10-07 to 10-08 | 0.96, 0.97 | **Product review**: help and README rewritten, file checklist in the commit dialog, amend and drop of older commits, the login dialog, stash apply, the tag list, diff of any two commits, line history; the GitHub page tidied |
| 10-08 | **1.0** | Help on GitHub, force push leased on what was shown, a broken config no longer stops gmd; scripts moved into `scripts/` |
| 10-13 | 1.5, 1.6 | **Start page** in place of the start menu; the update offer shown the same way everywhere |
| 10-13 to 10-14 | **1.7** | **A review of every layer** (Common, Git, Utils, Server, Installation, Cui), cancelling an update, a safer squash, **Homebrew**, the hang at start on macOS fixed (see "1.7" at the end) |

---

## The larger new features

**Conflict resolution** (August and September). A merge, rebase, cherry pick, revert or `am` that stops on
conflicts used to put git's output in a red error box. Now gmd says what stopped and which files,
the top bar shows the operation for as long as it lasts (red with conflicts, yellow without), and
the repo menu (`Shift-M`) offers Continue, Skip and Abort. The resolver shows the whole file with the
two sides side by side, named as git names them rather than "ours" and "theirs". It has the common
ancestor on demand (`b`), even when git did not write it into the file, a pane with the result, and
an edit box for a hand-made merge. It handles files that are deleted, added or binary on one side.
A commit or Continue refuses files that are still unresolved and markers left in a file.

**Blame** (August). Lines from the same commit are bracketed together in the gutter and the commit
is named once per run, shaded by age. `p` blames the version before a commit, to step past a
reformat or a rename. `l` shows the history of the selected lines: every commit that changed them,
with the diff of those lines alone (`git log -L`).

**Worktrees** (September). gmd opens inside a linked worktree. A branch checked out in another
worktree is marked `⌂`, and `s` on it opens that worktree, since git would refuse the checkout.
`⌂N` in the top bar counts the other worktrees, in yellow when one has uncommitted changes. `w`
lists them, with their changes, whether they are in use (by a running Claude Code session, say),
missing or merged. Worktrees can be added (beside the repo, in `.claude/worktrees/` or in
`.worktrees/`), removed with their branch, and pruned. The README now has a section on gmd and AI
coding agents, which work in worktrees and branches of their own.

**A way back** (October). Before, gmd could only discard changes. Now:

- **Undo** of the last change of any branch, a commit, a merge, a rebase or a reset, named after
  what the branch's reflog says it was, whether it was made in gmd or not. Undo again redoes it.
  A squash is undone in one step. Nothing uncommitted is lost.
- **Recover Lost Commits**: the lines of work no branch, tag or stash has any more, which the
  reflogs still mention, each with the branch it was made on and a diff, and brought back as a
  branch.
- **Restore Deleted Branch**: git deletes a branch's reflog with the branch, so gmd keeps its own
  record of every branch it deletes, and restores the branch where it was and tracking what it
  tracked, with origin's copy too if it was deleted there.
- **Force push on origin**: gmd tells when someone rewrote a branch on origin, pulls it by moving
  the local commits onto the new version rather than merging the old one back in, and can put
  origin back from before the force push.

**Rewriting commits not pushed yet** (October). **Amend** and **Drop** of any commit not pushed yet,
from its commit menu, done with git's own `rebase --autosquash` and recorded for Undo. Squash was
rewritten in 1.7 (2026-10-14), see the end.

**The commit dialog** (September and October). Spell checking of the message, with suggestions, a
dictionary of the user's own words, and a hint on the frame edge counting the misspelled words.
A checklist of the files to commit, all ticked at first, so a commit can leave files out. After a
merge, `Ctrl-A` adds the subjects of the merged commits.

**The start page** (2026-10-13, 1.5). Started outside a repository, gmd used to show a menu in the
corner of a black screen. It now shows a page in place of the log: the recent repositories and the
ones in the folder gmd was started in, by name and `~/...` path, opened with `Enter` (the most
recent) or `1` to `9`, and the actions Open, Clone and Init on a letter each, explained to someone
who is new to gmd. Init, Clone and the folder browser start in the folder gmd was started in.
The open menu (`o`) lists the repositories the same way, so `o 1` goes back to the previous one.

**Logging in** (2026-10-07). A git command that wanted a passphrase, a password or whether to
trust a host used to ask on gmd's own terminal: the question was drawn over the UI and took the
keys typed for gmd. Git now has no terminal to ask on. Git and ssh run gmd itself as their askpass,
which asks the running gmd over a named pipe, and that gmd shows a login dialog. The background fetch
never asks. It says that a login is needed, and `r` fetches and asks.

**Search and finding things** (September and October).

- A search matches the whole commit message, not only the subject. `file:<path>` finds the commits
  that changed a file, and `change:<text>` the commits whose changes added or removed a text
  (`git log -S`).
- `n` and `Shift-N` step through the matches in the log after a search.
- **Find Branch**: typing in the Show Branch menu (`Shift-→`) narrows a list of branches by name.
- **Diff any two commits**: Mark for Diff on one, then Diff with it on the other.
- **Diff context** per file: `+` and `-` step from 6 lines to 15 and then to the whole file.
- **Open in Browser**: a branch, a commit, the repository, or a new pull request into the branch it
  was made from, on GitHub, GitLab, Bitbucket, Azure DevOps or Gitea. With no browser, as over ssh,
  the link is copied instead.

**Following hidden branches** (September and October). This part is gmd's distinguishing feature:
choosing which branches are shown.

- ✦ in the top bar counts the hidden branches with something new since they were last seen: new
  commits, or a new branch such as an agent's. A click lists them.
- `Backspace` undoes the last show or hide, one step at a time.
- The branch that becomes current is shown, whether it was checked out in gmd, in a terminal or
  by another tool.
- A hidden branch with no commits of its own is marked where it branches out, as hidden branches
  already were where they merge in.

**Smaller additions**: Merge Into (`Shift-E`, the current branch into the highlighted one), Rename
Branch, Stash Apply, a Tags list to push or remove tags from, copying the commit id and message
with `i` and `Shift-I` (in the log and in blame), and deleted branches drawn in gray.

---

## Easier to learn: what is shown and said

Most of this came from the usability review (2026-09-25) and the product review (2026-10-07).

- **The key-hint line** at the bottom of the log lists the keys that do something where the cursor
  is, and the repo's state (`p push` only with something to push, `c continue` during a rebase).
  The diff, blame, conflict views and the start page have key-hint lines of their own. One class,
  `LogKeys`, decides both what a key does and what its hint says, so the two cannot disagree.
- **The status line**: a key that cannot act says why, rather than doing nothing. A push or a pull
  with nothing to do says so, rather than in a red error box. A commit, push or pull says what it is
  doing while git works. A greyed-out menu item says why it is greyed out when it is picked.
- **Menus**: an item's key picks it in an open menu. Shifted keys are written `⇧p`, as on the
  key-hint line. Item names are plain (no slashes), and the duplicate items are gone.
- **Keys written as typed** everywhere: `c` is the c key, `Shift-P` is P. Menus and the help used to
  write a plain key in upper case, which led users to press the shifted key, a different command.
- **The help** was restructured (the screen first, then the tasks, then the reference), and opens on
  GitHub at the commit gmd was built from, rather than in a dialog. The README was rewritten for new
  users, with the development part moved to `docs/CONTRIBUTING.md`. The demo animation is recorded by
  a script (`scripts/demo`) and has captions.
- **A crash** used to end gmd without a word and with exit code 0. Now gmd says what failed, where
  the log is and where to report it.
- **Small terminals**: gmd is usable at 80 by 24 columns and rows. On a narrow terminal the branch
  tips, tags and graph are shown whole. The columns stay in place with Chinese, Japanese and emoji
  text.
- Dates have the year in full, and the current row is highlighted across the graph too.

---

## Safer: a slip of the finger can no longer push, pull or lose work

Fixed in the usability review (Tier 1, 2026-09-25), and after it:

- A key that the diff, blame or conflict view did not use fell through to the log view: `Shift-P` in
  a diff pushed every branch, and `c` in the resolver closed it with the decisions unsaved. These
  views are modal now.
- `p` force-pushed every branch that had a remote. It is a plain push now, and a force push only
  when Force Push is chosen. A force push is leased on the remote tip gmd showed, so it never
  overwrites commits that a background fetch brought in after the screen was drawn. Rebase and Push
  onto asks first.
- `Esc` in the log view quit at once. It asks now. A click on ▲ or ▼ in the top bar pushed or
  pulled every shown branch. It opens a menu now. A middle click merged a branch without a
  question. It asks now.
- Discarding changes, dropping a stash and removing a tag ask first, and the safe answer is the
  default (Cancel on Delete Branch and on Binary Files Detected).
- The uncommitted diff used to stage everything with `git add .` and then reset, which wiped
  whatever the user had staged with another tool. It now stages into a copy of the index and never
  writes the real one.
- Git never asks on the terminal and never opens an editor, and runs in English, so gmd recognizes
  its messages under every locale.
- Pulling a diverged branch failed for anyone without `pull.rebase` set. gmd now asks once, merge
  or rebase, and saves the answer in the repository's git config.
- A push is allowed with uncommitted changes, and a switch that git refuses because of them offers
  to stash them, switch, and bring them back.
- A broken `~/.gmdconfig` no longer stops gmd on every start, and two running gmd instances no
  longer overwrite each other's changes to it. A `~/gmd.log` that gmd cannot write no longer hangs
  it.
- The branch choices shared through the repository are merged three ways, so a background fetch no
  longer undoes a choice made locally, and a push no longer wipes out one pushed by another clone.

---

## The branch inference

This is the code that works out which branch each commit belongs to, gmd's core, and it is held to a
before and after comparison over real repositories (`InferenceDumpTest`), not only to the tests.

- **The first round** (2026-09-26) reviewed the rules. Among the fixes: branch points of features
  that were merged back and kept, a `git checkout -b` at a published branch's tip, pull merges made
  by hand, GitHub's `owner:branch` merge subjects and foxtrot merges. Before it, a cycle between a
  branch and its remote copy that made reading a repo never end was fixed, and so was every deleted
  branch being named `branch(n)` rather than after its merge subject.
- **The second round** (2026-09-26) reads the reflog as evidence: where a commit was made, and which
  branch a branch was started from. A fact from the reflog decides only between the branches a
  commit could be on, and is kept in the metadata, because the reflog expires. A repository can name
  its own integration branches in Config.
- Earlier, in August, the 989-line `BranchStructureService` was split into a pipeline of stage and
  rule classes, and verified as pure movement by dumping a repo of about 1,750 commits before and
  after.

---

## Updates and releases

**Installing**: with Homebrew on macOS and Linux, `brew install michael-reichenauer/tap/gmd` (1.7),
from a tap that the release workflow writes the formula into at every release. Without it,
`install.sh` downloads the binary for the OS and CPU and checks it against the release's checksums.

**The updater** was split into four classes and tested (from no tests to 49), in 1.7 apart from the
last item. It now:

- Downloads over a slow connection. Below about 8 Mbit/s every update used to fail.
- Checks the new binary against the release's `SHA256SUMS`, and that it runs, before it replaces
  gmd.
- Replaces gmd in one atomic step on Linux and macOS, and never leaves gmd missing when an update
  fails.
- Installs an automatic update once, rather than again every hour.
- Can be cancelled, after asking, up to the moment gmd is replaced.
- Shows the change log before an update.
- Leaves updating to Homebrew when Homebrew installed gmd, and says the command, `brew update &&
  brew upgrade gmd`.
- Offers a new version the same way on the start page, in the menus and in the top bar (⇓), which
  now appears when a release is found after gmd started (1.1 and 1.6).

**Releases**: a push to `main` makes a release commit that raises the minor version and regenerates
`CHANGELOG.md` from the merge commit messages. Each release has notes, checksums and `gmd.exe`, and
only the latest five previews are kept. The version is computed from the build time in UTC, so the
update check is right in every time zone. The release action is pinned to a commit. A release from
`main` also writes the Homebrew formula into the tap, with a deploy key of the tap's (1.7).

**The GitHub page**: badges and a quick install in the README, a security policy, a pull request
template, issue forms (also linked from the crash message), Dependabot, and a social preview image.

---

## Under the hood

- **Toolchain**: .NET 8 to .NET 10, built with the .NET 11 SDK for the C# 15 compiler.
  Terminal.Gui 1.17 to 1.19, which fixed the 100% CPU spin on Linux and macOS. CSharpier formats on
  save, on build and in CI. CI runs on every branch. `docs/UPGRADING.md` has the steps for when
  .NET 11 is released.
- **Errors**: `Result` / `Result<T>` are a C# 15 union type, matched on their case types, with an
  exhaustive `switch`. About 480 `Try(out …)` sites were rewritten (2026-09-11).
- **Structure**: the layers are clean, with nothing below `Cui/` referencing Terminal.Gui. The large
  classes are split, so the part that decides can be tested without a terminal (`RepoView`,
  `ContentView`, `Menu`, `UIDialog`, `AugmentedService`, the updater). `Cui/` is grouped into a
  folder per feature. `gmd.Git` is kept a general git library, and a test enforces that.
- **Tests**, from 2 to about 1,800, with five fixtures: `FakeCmd` (canned git output), `RepoBuilder`
  (a repo in a few lines, run through the real inference), `GraphText` / `DiffText` / `ScreenText`
  (drawn output as a picture), `TempRepo` (real git in a temp folder) and `TmuxSession` (the built
  binary in a real terminal, run in parallel, eight at a time). Every test runs under a throwaway
  `$HOME`.
- **Performance**: the git log is parsed as spans, and the view repo and the graph layout are faster
  with many branches shown. Each change was measured first (`RefreshTimingTest`).
- **Copying** works on more platforms: a chain of clipboard tools, ending in OSC 52 through the
  terminal, which works over ssh and from a container. macOS had no copy at all before, and `xclip`
  hung gmd.
- **Correctness** fixes from the reviews: the log under `ar-SA` and Thai or Persian calendars, file
  names that are not ASCII, `\"` in messages, authors with `|`, SHA-256 repositories, the user's git
  config changing the output gmd parses, a fetch that fetched tags only, and opening a repo that
  deleted local tags.
- **The repository**: the scripts moved into `scripts/`, the build output into `artifacts/`, the
  working notes into `.notes/` and the contributor documentation into `docs/`. CLAUDE.md describes
  the codebase for Claude Code.

---

## 1.7 (2026-10-14): a review of every layer, and Homebrew

Released as v1.7.1445.1049, everything on `dev` since v1.6.1444.518: a review of every layer
(2026-10-13), each finding checked against the code before it was acted on, and the work after it.

- **Homebrew**: `brew install michael-reichenauer/tap/gmd` on macOS and Linux, from a tap the
  release workflow writes. A gmd that Homebrew installed leaves updating it to Homebrew, and ⇓ says
  the command. Tried on Linux and on macOS 27 before the release.
- **gmd hung at its start on macOS 27**, now and then, with nothing drawn: a deadlock in macOS's C
  library between curses setting the locale and git started for its version at the same moment.
  Found by sampling the hung process (`sample`), and fixed by starting no program until
  Terminal.Gui has started.

- **Git**: the output gmd parses no longer changes with the user's git config (`diff.noprefix`,
  `color.ui`, `log.showSignature`, …). Files with names that are not ASCII can be shown, committed,
  discarded and blamed. Messages can have `\"` in them. Authors can have `|` in their names.
  SHA-256 repositories work.
- **Utils**: a `~/gmd.log` that gmd cannot write hung it on a blank screen. Text is told from binary
  by its bytes, as git does it.
- **Server**: gmd can open a repo in the middle of merging a commit no branch has. A background fetch
  no longer undoes a branch choice made locally. Merge and rebase go onto the side of a branch that
  has the commits, not the one with the newer time.
- **Installation**: the updater fixes listed under Updates and releases.
- **Cui**: six ways gmd could stop, among them a click in an empty menu and `m` after a branch was
  hidden. A keyboard that stayed dead after a menu was opened during a refresh. Branch and tag names
  with a space. Keys and menus that did not do what their hints said. Rebase and Push onto asks
  first. A file stays conflicted until all of its conflicts are resolved. Branch tips, tags and the
  graph are shown whole on a narrow terminal, and the graph is laid out faster. `Cui/` is grouped by
  feature.
- The main branch can never be hidden, and a refresh no longer brings back an older repo.
- Squash rewritten: whole or not at all, keeping a merge among the commits after the squashed ones,
  and their authors and dates.
- The shared branch choices are pushed without wiping out ones another clone pushed meanwhile.
- A change of line ends alone is shown in the diff, as `␍` on the side with the CR. The diff's file
  list is headed by how many files were added, modified and removed, and how many lines.
- The columns stay in place with Chinese, Japanese and emoji text. The resolver's result scrolls.

---

## Postponed or left out on purpose

Each is in `.notes/MODERNIZATION.md` (under Deferred) or `.notes/USABILITY.md`, with the reasoning:

- **Terminal.Gui 2.x**: when, not if. It is what light themes, `NO_COLOR` and more than five branch
  colors wait for.
- **A second remote** (an `upstream` for a fork), until the need is understood from use.
- **Measuring the inference** on public repositories and publishing the numbers, to go with the
  next work on the inference.
- **More packages**: Homebrew came in 1.7, and in 1.8 a PowerShell install for Windows, which meets
  no SmartScreen warning since no browser downloads it, and a dev container feature. winget was left
  out: Defender in its validation blocks an unsigned program few have run, and gmd is not signed.
  Scoop, AUR and an Intel Mac build maybe later (`.notes/USABILITY.md`, item 24).
- **Staging of single lines or hunks**, left out: the file checklist in the commit dialog was judged
  the right size for gmd.
