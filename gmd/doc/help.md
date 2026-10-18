# Gmd Help Guide

What is on the screen comes first, then the everyday tasks, then the
reference. `?` or F1 in gmd opens this page, the version of it that goes
with the gmd you run, and in the diff, blame or conflict view it opens at
the part about it.

- [Reading the Log](#reading-the-log)
- [Showing and Hiding Branches](#showing-and-hiding-branches)
- [Everyday Tasks](#everyday-tasks)
- [Keys](#keys)
- [Symbols](#symbols)
- [Remote and Local Branches](#remote-and-local-branches)
- [Undo and Recovery](#undo-and-recovery)
- [More Commands](#more-commands)
- [Diff and Blame](#diff-and-blame)
- [Resolving Conflicts](#resolving-conflicts)
- [Worktrees](#worktrees)
- [How Gmd Picks a Commit's Branch](#how-gmd-picks-a-commits-branch)
- [Problems and Feedback](#problems-and-feedback)


## Reading the Log

Each branch is drawn in a column and a color of its own, with main running
down the left. Git does not record which branch a commit was made on, so
gmd works it out (see [How Gmd Picks a Commit's Branch](#how-gmd-picks-a-commits-branch), near the end).

A repository opens showing main and the branch you are on. The other
branches are hidden, and marked where they meet a shown one; the first
time, the line at the bottom says how many are hidden:

    ┣  ● Add delta                    (● main)[v1.0]
    ┣╮   Merge branch 'dev' into main
    ┣    Add gamma
    ┣╯   Add beta
    ┗    Initial

The dark `╮` means that a hidden branch was merged in at that commit, and
`╯` that one was made from it; here both are dev. To show it, highlight
main on that row with `←` and press `Enter`, or click it. Dev then gets a
column of its own, right of the branch it was made from:

    ┣   ● Add delta                   (● main)[v1.0]
    ┣╮    Merge branch 'dev' into main
    ┣│    Add gamma
    ┃╰╊   More dev work                        (dev)
    ┃╭┺   Work on dev
    ┣╯    Add beta
    ┗     Initial

The thin lines between the columns join a commit to a parent in another
column: `╮` down to `╰` where the branch on the right was merged into the
one on its left, and `╭` down to `╯` where it was made from the one on its
left, or had that one merged into it. After the graph, each row has:

- the subject, after `●` for the commit checked out, `©` for uncommitted
  changes, or `▲` and `▼` for a commit not yet pushed or pulled
- the branch tips, e.g. `(● main)` for the branch you are on, `(dev)`, or
  `(^/main)` for main on origin; and the tags, e.g. `[v1.0]`
- the commit id, the author and the date

With a remote, main's first row is drawn `┣─┺`: main on origin and yours,
on the same commit (see [Remote and Local Branches](#remote-and-local-branches)).

`Enter` shows the details of the commit below the log, and `Tab` moves
into them and back: its full id and tags, its branch, its author, its
parents and children (with their branch where it is another, so a merge
says what it merged), its message, and the files it changed with the lines
added and deleted in each. For the uncommitted changes they list the files,
the conflicted first.

### The Highlighted Branch

`←` and `→` highlight a branch on the cursor's row: its column is shaded,
and the line at the bottom starts with its name. Moving past the last one
on the right goes back to the commit. The mouse highlights the branch
under it too.

Most keys act on the highlighted branch: `m` opens its menu, `s` switches
to it, `e` merges it into the current branch, `d` diffs it, `b` makes a
branch from it and `h` hides it. With no branch highlighted, `m`, `d` and
`b` act on the commit instead, and `p` and `u` on the current branch.

### The Top Bar

    Gmd ┅/acme-store, ●main, ©2, ▼1, ▲1, ✦3, ß1, ⌂1        [Ϙ Search] ? X

Each item can be clicked:

| Item           | What it is, and what a click does                    |
| -------------- | ---------------------------------------------------- |
| ⇓              | A new version of gmd: update to it                   |
| Gmd            | The repo menu (also Shift-M)                         |
| Merging: ...   | A merge, rebase, ... in progress: continue or abort  |
| ┅/acme-store   | The repository: open another one                     |
| ●main          | The branch you are on: go to it (also y)             |
| ©2             | Uncommitted changes: commit them (also c)            |
| ▼1  ▲1         | Commits to pull, and to push, on the shown branches  |
| ✦3             | Hidden branches with something new: list them        |
| ß1             | Stashes: the stash menu                              |
| ⌂1             | Other worktrees, yellow if one has changes (also w)  |
| commit on dev  | With key hints off, the row's commit: show a branch  |
| Ϙ Search ? X   | Search (also f), help (also ?), and quit             |

An update downloads the new version and checks it before it replaces gmd,
which quits then; the new version runs when gmd is started again. **Cancel**
in the box shown meanwhile stops it, after asking, and leaves gmd as it is,
up to the moment the new version is put in place. A gmd installed with
Homebrew is updated by Homebrew instead, so ⇓ then says the command to run,
`brew update && brew upgrade gmd`.

The line at the bottom shows the keys that do something where the cursor
is, and changes as it moves; the diff, blame and conflict views have one of
their own. It starts with what the keys act on: the highlighted branch,
`dev:`, or else the commit on the row and the branch it is on,
`commit on dev:` (`changes on main:` on the uncommitted row).
**Config ...** in the repo menu turns the lines off, and the top bar then
names the commit on the row instead, at its right.
While a commit, push or pull runs it says what is being done, in cyan. For
a few seconds after a command it says what the command did, or why a key
or a greyed out menu item did nothing, and in red when a fetch failed.

### Colors

Gmd draws in colors of its own: a dark palette, or a light one when the
terminal's background is light, which gmd asks the terminal as it starts.
Branches get twelve colors, picked by their names, and `g` changes the
color of the highlighted one. **Colors** in **Config ...** in the repo menu
chooses: `auto` (dark or light by the terminal), `dark`, `light`, or `16`,
the 16 colors of the terminal's own theme on black, with five branch colors,
which is also what a terminal of 16 colors gets. A terminal without 24-bit
color, e.g. macOS Terminal before macOS 26, needs `16`.


## Showing and Hiding Branches

Which branches are shown is up to you, which is how gmd gives a clean log
without rebasing or squashing. Showing a branch also shows the branches it
was made from, and main is always shown. So is the branch you are on, once
it becomes current: switched to in gmd, checked out in a terminal or by
another tool, or the one gmd or a worktree opens on. Hidden again, it stays
hidden until it next becomes current.

- `Shift-→` opens **Show Branch**: first the hidden branches that merge in
  or branch out at the cursor's commit, then New (see ✦ below), Recent,
  Active, My Active (where the last commit is yours), Active and Deleted,
  and Ambiguous. The branch menus have the same list, as a submenu.
- Typing in that menu, or in its sub menus, opens **Find Branch** with what
  was typed. The list narrows as more of the name is typed, every word has
  to be in it, and the names it starts a part of come first. `Enter` or a
  click shows the branch.
- `Enter` on a highlighted branch where it has a `╮` or `╯` beside it, or a
  click there, shows that hidden branch, or opens **Show/Hide Branch** when
  there are several. The same hides it again.
- `h` hides the highlighted branch, and the branches made from it. The main
  branch is always shown, so it cannot be hidden.
  **Hide All Branches** goes back to showing just main.
- `Backspace` undoes the last show or hide, going back to the branches
  shown before it, one step at a time. **Undo** under **Branches** in the
  commit menu does the same, and names what it would undo.
- `y` goes to the branch you are on, showing it if it is hidden.
- ✦ in the top bar counts the hidden branches with something new since you
  last saw them: commits pushed to them, or the branch itself, pushed by
  someone else or made here by another tool, e.g. in a worktree of its own.
  A click lists those branches, and **New** at the top of the Show Branch
  menu does too. Listed is seen: the ✦ goes, and the branches stay hidden
  unless you pick one to show it.
- The `<=` and `=>` items in a branch menu move the branch to the left or
  the right of a branch it overlaps in the graph.

The shown branches, their colors and their order are remembered per
repository.


## Everyday Tasks

- **Start a branch:** `b` on the branch or the commit to start from.
  *Checkout* switches to the new branch, and *Publish* pushes it to origin.
- **Commit:** `c`, type the message, then `Alt-O` or OK. The dialog lists
  the changed files, new ones too, all ticked: `Tab` into the list and
  `Space` unticks the file under the cursor, which is then left as it is,
  uncommitted, and `a` unticks or ticks all. `Ctrl-D` shows the diff.
- **Commit part of a file:** in the diff of the uncommitted changes (`d`
  on the `©` row), `x` leaves the change the cursor is on, or the lines
  selected with `Shift-↑↓`, out of the commit, and `x` again puts them
  back. The commit then leaves them in the working tree, uncommitted.
- **Push and pull:** `p` and `u` for the branch you are on, or for the
  highlighted one; `Shift-P` and `Shift-U` for every shown branch.
- **Open a pull request:** `m` on the branch, then **Create Pull Request in
  Browser**, which proposes it into the branch it was made from.
- **Bring main into your branch:** highlight main and press `e`, then
  commit the merge in the dialog that opens.
- **Merge your branch into main:** highlight main and press `Shift-E`. Gmd
  switches to main, merges, and switches back once the merge is committed.
- **See what changed:** `d` on a commit, or on the `©` row for what is not
  committed yet. On a highlighted branch, `d` diffs it to another branch.
- **Find a commit:** `f` or `/`, and type part of its message, author, id,
  branch or tag, `file:` and part of a path, or `change:` and a text the
  changes added or removed.
- **Show another branch:** `Shift-→`, and type part of its name.
- **Undo a mistake:** **Undo** in the commit menu (`m`) takes back the last
  change of the branch you are on, e.g. a commit, a merge or a pull.
- **Switch repository:** `o`, then the number of a recent one, or browse to,
  clone or create one. Started outside a repository, gmd shows a start page
  instead of the log: `Enter` opens the most recent repository, `1` to `9`
  any one listed, which includes those in the folder gmd was started in, and
  `Delete` takes one off the recent list, leaving its folder as it is.
  `gmd -m` shows the start page inside a repository too.
- **Clean up after pull requests:** **Delete Merged Branches ...** in the repo
  menu (`Shift-M`) lists the local branches merged into main, by a squash or
  a rebase on GitHub too, marked `✓` in the log, and deletes them.
- **Committed on the wrong branch,** e.g. on main: `m` on the oldest commit
  of them, then **Move to New Branch ...**, which moves it and the newer
  ones while they are not pushed yet.
- **Fix a commit:** `m` on it, then **Amend <id> ...** for a new message,
  or the changes you tick, or both, or **Drop <id>** under **Undo** to take
  it out of the branch. `a` amends the last commit. Once the commit is on
  origin, `p` then replaces it there (see [When You Rewrote a Pushed
  Branch](#when-you-rewrote-a-pushed-branch)).


## Keys

The most used keys of the log view. The menus show the key of every command
that has one, and pressing it in an open menu picks that item. Keys are
written as they are typed: `c` is the c key, and `Shift-P` is P, which the
menus and the line at the bottom write `⇧p`, ⇧ being Shift.

| Key        | Description                                                |
| ---------- | ---------------------------------------------------------- |
| m          | Menu of the highlighted branch, or of the commit           |
| Shift-M    | The repo menu, e.g. to continue or abort a rebase          |
| ← →        | Highlight the branch to the left or right, or the commit   |
| Shift-→    | Show Branch menu: type part of a name to find a branch     |
| Enter      | Toggle commit details (on a branch: show/hide branches)    |
| Tab        | Move between the log and the commit details                |
| c          | Commit the uncommitted changes                             |
| a          | Amend the last commit                                      |
| d          | Diff of the commit (on a branch: diff the branch to ...)   |
| Ctrl-D     | Diff of the commit or the selected rows, even on a branch  |
| s          | Switch to the highlighted branch                           |
| b          | Create a branch from the highlighted branch or the commit  |
| e          | Merge the highlighted branch into the current branch       |
| Shift-E    | Merge the current branch into the highlighted branch       |
| h          | Hide the highlighted branch                                |
| Backspace  | Undo the last show or hide of branches                     |
| g          | Change the color of the highlighted branch (not main's)    |
| p          | Push the highlighted branch, or the current one            |
| u          | Pull the highlighted branch, or the current one            |
| Shift-P    | Push all shown branches                                    |
| Shift-U    | Pull all shown branches                                    |
| t          | Add a tag to the commit                                    |
| f, /       | Search and filter the commits                              |
| n, Shift-N | The next and the previous match of the last search         |
| r, F5      | Refresh, and fetch from the remote                         |
| w          | Open the worktrees dialog                                  |
| o          | Open, clone or init a repository; 1-9 opens a recent one   |
| y          | Go to the current branch, showing it if it is hidden       |
| PgUp, PgDn | A page up or down (Space too), in every list and view      |
| Home, End  | The first or the last row                                  |
| Shift-↑↓   | Select rows in the log, diff and blame views               |
| Ctrl-C     | Copy menu: the id, message, link ... of the commit or rows |
| i          | Copy the commit id, or the ids of the selected rows        |
| Shift-I    | Copy the commit message, or those of the selected rows     |
| ?, F1      | Open this help in the browser                              |
| Esc        | Close a menu, dialog or view                               |
| Esc, q     | Quit, in the log view (Esc asks first)                     |

In dialogs and text fields:

| Key        | Description                                                |
| ---------- | ---------------------------------------------------------- |
| Alt-O      | OK, e.g. to commit while typing in the message box         |
| Ctrl-D     | Show the diff of what is committed, in the commit dialog   |
| Ctrl-A     | After a merge, add the merged commits' subjects            |
| Ctrl-A     | Otherwise select all; Ctrl-C, -X, -V copy, cut and paste   |
| Ctrl-Z     | Undo typing (Ctrl-Y redo)                                  |
| Space, a   | Untick or tick a file, or all, in the commit file list     |
| F7         | Spelling suggestions (also Ctrl-G)                         |
| Shift-F10  | Text menu: spelling, copy, paste (also right-click)        |
| Esc        | Cancel                                                     |

The mouse works too: hovering highlights a branch, right-click opens the
menu of a branch or a commit (and the menu of the diff, blame and conflict
views), double-click switches to a branch (or toggles the details of a
commit), and middle-click merges a branch into the current branch, after
asking. The items in the top bar can be clicked as well: `▲` and `▼` open
a menu to push or pull the current branch or all of them.

On macOS, the Terminal app sends `Shift-↑` and `Shift-↓` as a plain `↑` and
`↓`, so they move the cursor rather than select rows. Add the two keys in
Terminal → Settings → Profiles → Keyboard, with `+`: Cursor Up with Shift,
Send Text `\033[1;2A`, and Cursor Down with Shift, `\033[1;2B` (press `Esc`
in the text field for the `\033`). `cat -v` shows what a key sends:
`^[[1;2A` once it is set, `^[[A` before. Dragging over rows with the mouse
selects them too, in any terminal.


## Symbols

| Symbol | Description                                                  |
| ------ | ------------------------------------------------------------ |
| ┣╮ ┣╯  | A hidden branch merged in, or made, at the commit (dark)     |
| ┬ ┴    | The same two, where a line passes by                         |
| ┣─┺    | A branch and its remote on the same commit, origin's left    |
| Φ      | Manually set branch for that commit                          |
| ●      | Current commit and branch                                    |
| ©      | Uncommitted changes (in yellow, red with conflicts)          |
| *      | Detached commit (commit checked out, not a branch)           |
| ⌂      | Commit and branch checked out in another worktree            |
| ß      | Stash based on commit                                        |
| ▲      | Commit not yet pushed (green subject)                        |
| ▼      | Commit not yet pulled (blue subject)                         |
| ✓      | Branch squash or rebase merged on origin, deleted there      |
| ^      | Abbreviation for 'origin' in branch names                    |
| ~      | Deleted branch (inactive, drawn in gray)                     |
| o      | Branch shown in the graph (in branch menus)                  |
| ✦      | Hidden branches with something new (top bar)                 |
| ⇓      | Available update to download (top bar)                       |
| ┅      | Truncated name/text                                          |
| ┌ │ └  | Blame lines from the same commit (see Diff and Blame)        |
| ╺      | A blame run of one single line                               |


## Remote and Local Branches

A branch and its branch on origin are drawn as two columns, origin's on the
left. On the same commit they are `┣─┺`, with the tips `(^)(● main)`. When
they differ, each column has the commits only it has, and the subjects say
which way they have to go:

     ╭┺ ●▲Add zeta                   (● main)
    ┣╯    Add delta            (^/main)[v1.0]

- `▲` and a green subject: a commit to push.
- `▼` and a blue subject: a commit to pull.
- A dark subject with neither, in origin's column: the old version of a
  branch you rewrote (see [When You Rewrote a Pushed
  Branch](#when-you-rewrote-a-pushed-branch)), which a force push replaces.

`p` and `u` push and pull the highlighted branch, or the current one when
none is, as **Push** and **Pull** in its branch menu do. A push leaves the
uncommitted changes where they are; a pull needs them committed or stashed
first. Switching to a branch where git would overwrite them offers to stash
them, switch, and put them back. `Shift-P` and `Shift-U` push or update all
shown branches.

A branch deleted on origin, e.g. by GitHub once its pull request was
merged, and then by a fetch here, is said to be in the details pane,
`(deleted on origin)`, and `p` says so rather than pushing it: **Push** in
its branch menu makes it again on origin, after asking.

Merged by a squash or a rebase first, as GitHub's squash merge and rebase
merge do, the branch is not merged as git counts it, since what went into
main are copies of its commits. Gmd tells it by content, once the repo is
shown: merging the branch into main, or into the branch it was made from,
as origin has them, would change nothing. Its tip is then marked `✓`, e.g.
`(✓ feature)`, the details pane says `(merged into main on origin)`, and
the worktree list counts it merged, and the status line says so once,
pointing at **Delete Merged Branches ...** (see [More
Commands](#more-commands)). A branch whose lines main changed again since
is not told.

A branch with both unpulled and unpushed commits can only be pushed by
force, which `p` asks about first, and is not updated by `Shift-U`, which
only fast-forwards the branches it is not on. Switch to the branch and pull
it (`u`) to join the two sides. The first time, gmd asks whether to merge
them or to rebase your commits on top, unless git's `pull.rebase` already
says, and saves the answer there, for git on the command line as well.

### When Origin Was Force Pushed

Someone rebased or amended a branch and pushed it by force: then your
branch has the old version and origin the new one, and a merge would put
every commit in twice. Gmd tells this apart from new commits by the remote
branch's reflog, says so on the status line when it first sees it, and a
pull asks first, then moves your own commits onto the new version and
leaves the old one out, whatever `pull.rebase` says (as `git pull --rebase`
would). A branch that is not checked out is pulled that way, and by
`Shift-U`, when it has no commits of its own. The question names any
commit the force push dropped.

A force push that only dropped commits leaves nothing to pull, but the
branch still has them, so a plain push would put them back on origin: `p`
asks first, and `Shift-P` leaves the branch out. Pull (`u`) takes the new
version, as above.

If the force push was a mistake, **Restore origin/... from before the Force
Push ...** in the branch menu puts the old version back on origin. That is
a force push too, for everyone, so it asks first, and it is refused if
anything was pushed since the last fetch (`--force-with-lease`) or on top
of the rewrite. It also takes gmd's own **Rebase**, which force pushes,
back on origin after **Undo Rebase** took it back here.

### When You Rewrote a Pushed Branch

The mirror of the above: you squashed, amended, dropped or rebased commits
that are on origin already, in gmd or not, e.g. a coding agent did. Then your branch
has the new version and origin the old one. That looks like new commits on
both sides, but origin has nothing new: a pull would merge the old version
back in, putting every change in twice. Gmd tells this apart by the
branch's own reflog, which says it was where origin is now, and says so on
the status line when it first sees it, e.g. right after a squash. The old
version is drawn dark, with no `▼`, and there is nothing to pull.

`p` then pushes the branch with force, after a question that names the
commits it replaces on origin, for everyone who has the branch. It is
leased on origin as shown, so it is refused if anyone pushed since the last
fetch, and `Shift-P` leaves the branch out, to be asked about on its own.
Recover Lost Commits finds the old version afterwards.

Someone else's commits pushed on top of the old version make origin a
commit your branch never was at: the branch is then shown as diverged, as
new commits on both sides.

Amend, Drop and Squash rewrite commits on origin as they do the others, on
one rule: not while origin has commits the branch has not, which the force
push would drop, so those are pulled first, and not a commit origin has on
another branch, e.g. one merged into main. On a branch others build on
(main, an integration branch such as develop, or a release branch), or when
some of the commits on origin are someone else's, they ask first, with No
the default. Uncommit is for commits not pushed only.

The push can be made with the rewrite: Amend and Squash have a box, *Push
to 'origin/...' with force when done*, ticked on a branch of your own and
unticked once they asked first, and Drop's and **Rebase onto**'s questions
have **Drop and Push** and **Rebase and Push** beside Drop and Rebase. A
rewrite that stops on a conflict pushes nothing; once it is continued, `p`
pushes it.


## Undo and Recovery

The ways back are under **Undo** in the commit menu (`m`). The items that
throw changes away ask first, and what they throw away gmd keeps, for
**Restore Discarded Changes**.

- **Undo** the last change of the current branch, named after it, e.g.
  **Undo Rebase** or **Undo Commit 'Fix'**: the branch goes back to where
  it was before, as git's reflog of the branch has it, whether the change
  was made in gmd or not. Nothing uncommitted is lost: a commit or amend
  undone leaves its changes uncommitted (`git reset --mixed`), and any
  other change takes the files back with it (`git reset --keep`), which
  needs a clean tree. It asks nothing, since undoing again redoes it
  (**Redo ...**). A commit already pushed is not undone, and an undo never
  pushes: what origin has stays there.
- **Undo** in a branch menu does the same for that branch. A branch that is
  not checked out is just moved back (`git update-ref`), and only if
  nothing has moved it since; one checked out in another worktree is
  undone there.
- **Recover Lost Commits ...**: the commits no branch, tag or stash has
  any more, which git's reflogs still know of: what a `reset --hard`, a
  rebase or an amend left behind, the work of a deleted branch, commits
  made on a detached HEAD. One row per line of work, with the branch it
  was made on and what took it out of the history; older versions of
  commits that are still there, which an amend or a rebase leaves, are
  listed last, dark. `Enter` or `d` shows the diff, and `b` creates a
  branch at it, which brings it back. Git keeps them for about 30 days.
- **Restore Deleted Branch ...**: the branches gmd deleted, newest first,
  with the side each was deleted on: local, remote or both. Git deletes a
  branch's reflog with the branch, so gmd records where it was when it
  deletes one. `Enter` or `r` brings it back where it was, tracking what it
  tracked. A branch deleted on origin asks which sides to restore first,
  since restoring origin's is a push, and origin's is left alone if someone
  pushed a branch of the name since. A branch deleted outside gmd is not
  listed: Recover Lost Commits finds its work, if no other branch has it.
- **Restore Discarded Changes ...**: what the discards below threw away,
  and the stashes dropped, newest first. Uncommitted changes were never in
  git's history, so gmd keeps them itself: the files before and after each
  discard, as commits on a ref of its own, `refs/gmd/discarded`, whose
  reflog keeps them for about 30 days, as git keeps lost commits. `d`
  shows what was discarded, and `r` puts it back in the working tree, new
  files untracked again and nothing staged, or a stash back in the list of
  stashes. Git refuses, whole, to put back changes whose files have changed
  since; the diff is still there to copy from. The files git ignores are
  not kept, being none of the uncommitted changes.
- **Discard Lines** (`u` in the diff of the uncommitted changes, on a
  change, or with lines selected with `Shift-↑↓`): **This Change**, at the
  top of the menu, takes back the change the cursor is on, and **Selected
  Lines** those lines: removed lines are put back and added lines taken
  out, and the rest of the file's changes stay as they are. It asks first,
  and Restore Discarded Changes brings them back. It is refused if the file has changed since the
  diff was read, rather than taking back other lines (`r` reads it again),
  and is not for a file with conflicts, nor while whitespace is hidden
  (`w`), which are discarded whole. A renamed file stays renamed: its
  lines are taken back where it is now.
- **Discard Changes in a File** (also `u` in the diff of the uncommitted
  changes): `git checkout --force HEAD -- <file>`, staged changes too, or
  a new file is unstaged and deleted
- **Revert Commit**: `git revert --no-commit <commit-sha>`
- **Drop <commit-sha>**: takes a commit out of the current branch,
  `git rebase --onto <commit-sha>~ <commit-sha>`, asked first. The commits
  after it are rewritten without it, and Undo brings it back. Not for a
  merge, or past one, nor with another branch or a tag on it or after it,
  which would keep the old commits.
- **Uncommit Last Commit**: `git reset HEAD~1`, the changes stay uncommitted
- **Uncommit <commit-sha> and Newer**: `git reset --soft <commit-sha>~`, so
  that commit and the ones after it are uncommitted
- **Reset Branch to <commit-sha> ...**: the current branch moved to any
  commit, of it or another, after a question that says how many commits
  leave the branch, and that a pull brings back those origin has. **Keep
  Changes**, `git reset --mixed`, leaves what they changed as uncommitted
  changes, and **Discard Changes**, `git reset --keep`, takes the files back
  to the commit too, which git refuses rather than overwrite an uncommitted
  change. Undo takes it back, and Recover Lost Commits finds the commits.
- **Discard All Changes**: `git reset --hard` and `git clean -fd`
- **Discard All Changes and Ignored Files**: `git reset --hard` and
  `git clean -fxd`, i.e. the folder as a fresh clone of the last commit
  has it, to check that nothing left over makes a build or a test pass.
  The files git ignores go too, also those kept on purpose, such as a
  .env file of secrets, so the question lists everything it deletes.
  Restore Discarded Changes brings back the rest, but not those.


## More Commands

- **Commit ...** (`c`):
  Commit the uncommitted changes, every file ticked in the dialog's list,
  with warnings for large or binary files. While a merge, a revert or the
  like is in progress there is no list: git commits its result whole.
  Lines left out with `x` in the diff of the uncommitted changes are drawn
  dark there, with a `╎` where a change has `┃`, counted on the file's
  header, e.g. `(3 lines left out)`, and in the dialog, beside the file:
  the commit takes the rest of the file and leaves those lines as they
  are, uncommitted, and nothing staged. `x` on a file's header puts every
  line of it back. A line left out that is not found as it was shown, the
  file having changed since, stops the commit, with nothing committed,
  rather than going in: the diff finds a line again where only its number
  moved, and says how many it does not find. A renamed file's lines go in
  with its rename, and with every line left out the rename alone does: the
  move without the edits. Not while something is in progress, nor for a
  file with conflicts, nor while whitespace is hidden (`w`); and an older
  commit cannot be amended with a file that has lines left out.
  After a merge made in gmd, `Ctrl-A` in the dialog adds the subjects of
  the merged commits, one per line, and for a merged merge the list in its
  message.
- **Amend ...** (`a`) and **Amend <commit-sha> ...**:
  `a` amends the last commit: its message, and the files ticked in the
  dialog. The commit menu of an older commit of the current branch has an
  Amend of its own, with that commit's message and the changed
  files, none ticked at first: a new message, the ticked changes, or both.
  It is git's own way: an "amend!" commit of them, folded in by
  `git rebase -i --autosquash`, with the files left unticked put aside and
  back. Undo takes it back. It has the limits Drop has, in [Undo and
  Recovery](#undo-and-recovery). A later commit that conflicts with the changes stops the rebase
  there: resolve and continue, or abort, which leaves them in the "amend!"
  commit on top.
- **Search ...** (`f` or `/`):
  Type to filter the log down to the commits that match, by id, message
  (the subject and the body), branch, author, date (yyyy-mm-dd) or tag.
  Every word has to match, a "quoted phrase" matches as a whole, and case
  does not matter. `file:` and a path, e.g. `file:Program.cs`, matches the
  commits that changed a file with that in its path, and `change:` and a
  text, e.g. `change:RetryCount`, the commits whose changes added or
  removed it, which finds where code came from or went. `Enter` shows the
  selected commit and its branch in the log, and `Esc` goes back to where
  you were. After that, `n` and `Shift-N` go to the next and the previous
  match in the log.
- **Copy** (`Ctrl-C`, or **Copy** in the commit menu):
  Asks what to copy of the commit, or of the rows selected with
  `Shift-↑↓`: the id, the message, the subject, the id and subject as
  `git log --oneline` writes them, the short id, or the link to the commit
  on the site hosting the remote. `i` copies the id and `Shift-I` the
  message straight away. Selected commits are copied oldest first, the
  order `git cherry-pick` takes them in, with the ids on one line, and the
  menu has their range too, `<oldest>^..<newest>`, which `git log`,
  `git diff` and `git cherry-pick` read as the commits from the oldest to
  the newest.
- **Merging** (`e` and `Shift-E`):
  `e` merges the highlighted branch into the current branch, and `Shift-E`
  the current branch into the highlighted one. The menus name both
  branches: in the menu of dev, while main is current, `e` is **Merge dev
  into main** and `Shift-E` **Merge main into dev**. On the current branch
  itself they are **Merge into main** and **Merge main into**, each a list
  of the shown branches to pick the other one from. A merge opens the
  commit dialog, and **Abort Merge** at the top of the repo menu backs out
  of it.
  Git can only merge into the branch that is checked out, so `Shift-E`
  switches to the target branch, merges, opens the commit dialog there,
  and switches back once the merge is committed. Cancelling the commit, or
  a merge that conflicts, leaves you on the target branch, which is where
  the merge has to be finished. **Undo Merge** in the target's branch menu
  takes it back.
- **Squash ...** (in the commit menu):
  Select a range of commits on the current branch with `Shift-↑↓`, and
  squash them into one commit with a new message, which starts as the
  oldest commit's, with the subjects of the others listed below it, as
  GitHub lists them in a squash merge. The commits after them
  are put back on top as they were, merges and authors too, and **Undo**
  takes the squash back. Nothing changes until the squash is made. Commits
  on origin already can be squashed too, and `p` then pushes the squash with
  force: see [When You Rewrote a Pushed
  Branch](#when-you-rewrote-a-pushed-branch).
- **Tags** (in the commit menu's **Tag** menu):
  **Add Tag ...** (`t`) puts a tag on the commit, and pushes it to origin
  unless its box is unticked. **Tags ...** lists every tag, newest commit
  first, and whether origin has it: **Show** moves the log to the tag's
  commit, **Push** pushes a tag origin does not have, and **Remove** removes
  it, on origin too when origin has it, asked first.
- **Stash** (in the commit menu, and `ß` in the top bar):
  **Stash Changes ...** puts the uncommitted changes aside, new files too.
  **Stash Pop** brings a stash back and drops it, **Stash Apply** brings it
  back and keeps it, e.g. for the same changes on another branch as well,
  and **Stash Diff** and **Stash Drop** show it and throw it away, which
  Restore Discarded Changes, under Undo, takes back.
- **Cherry Pick into <branch>** (commit menu): the commit's changes are
  staged on the current branch, for the commit dialog, with its message.
  With the commits of another branch selected with `Shift-↑↓`, one line of
  them, the item counts them, **Cherry Pick 3 Commits into main**, and
  picks each as it was made, its author and message kept, with git's own
  `cherry-pick`; **Undo** takes them all back at once. One that conflicts
  stops the pick for the resolver, and **Continue** picks the rest.
- **Reorder** (commit menu): **Move Above '...'** and **Move Below '...'**
  move the commit past the next one of the current branch, or the one
  before it, each item naming the commit it is moved past. The two are
  swapped by git's own interactive rebase, on the rules Amend has, and Undo
  takes it back. Two commits that change the same lines conflict once
  swapped, which stops the rebase, to be resolved and continued, or
  aborted.
- **Move to New Branch ...** (commit menu):
  Moves the commits of the current branch from this one up, not pushed
  yet, to a new branch, e.g. ones made on main by mistake. The new branch
  is made where they are and checked out, so the files and any changes
  stay as they are, and published when its box is ticked; the branch they
  were on goes back to the commit before them, which **Undo** in its
  branch menu takes back.
- **Delete Merged Branches ...** (repo menu, `Shift-M`):
  Lists the local branches with no branch on origin, deleted there or never
  pushed, whose work is in main or an integration branch already: merged
  as git counts it, or squash or rebase merged on origin (marked `✓`, see
  [Remote and Local Branches](#remote-and-local-branches)). `Enter` or `d`
  deletes the one selected, and `a` all of them, after asking, as git's
  `branch -D` would, which a squash merge needs. A branch checked out, here
  or in another worktree, is listed but left. Each one deleted is kept for
  **Restore Deleted Branch**. A branch made with no commits of its own yet
  is not listed, though git counts it merged.
- **Rename Branch ...**:
  Renames the branch with `git branch -m`, which also works on the current
  branch, without checking anything out. A published branch is renamed on
  the remote as well, by pushing the new name and then deleting the old
  remote branch. Note that deleting it affects everyone: other clones lose
  track of the branch and a pull request made from the old name is closed.
- **Open in Browser** (branch menu):
  Opens the branch on the site hosting the remote (GitHub, GitLab,
  Bitbucket, Azure DevOps or Gitea), and **Create Pull Request in Browser**
  the page for a pull request into the branch it was made from. The commit
  menu has **Open Commit in Browser**, and the repo menu **Open Repository
  in Browser**. `$BROWSER` is used when it is set, and with no browser to
  open, e.g. over ssh, the link is copied instead.
- **Set Commit Branch Manually ...** (branch menu):
  Sets the branch of a commit where gmd guessed wrong or could not decide
  (see [How Gmd Picks a Commit's Branch](#how-gmd-picks-a-commits-branch)).

Find more commands in the menus (the `m` key).


## Diff and Blame

**Commit Diff** (`d`) shows the changes of a commit side by side, below the
commit's id and subject, who made it when, and a list of its files: each
with a letter, `M` modified, `A` added, `D` deleted, `R` renamed and `C`
conflicted, and the lines added and deleted in it, headed by how many there
are of each, e.g. `3 files changed  1 added, 2 modified  +25 -7`; the files
are left out of it, that row kept, while they are listed beside the diff
(below). Each file
starts with a row naming it, underlined in blue. Where unchanged lines are
left out between the parts that changed, a dark line with `⋯` where the line
numbers are says so, with the function or heading git finds the next part in, when it is not
the one before it. Git finds it by a pattern for the file's language, set
in `.gitattributes`, e.g. `*.cs diff=csharp`; with none, it takes the
nearest line above that is not indented, which in C# is the class for every
part. A changed line has a bar
beside its number, red for a line removed and green for a line added, and
where a removed line and the added line beside it are mostly alike, the
words that differ are marked. The cursor is the line number drawn reversed.
A line whose line end alone changed, e.g. from LF to CRLF, has a `␍` on the
side with the CR. `+` shows
more of the file the cursor is on around its changes and `-` shows less,
stepping from the 6 lines of context it starts with to 15, and then to the
whole file (and `=` does what `+` does, without the shift). It applies to
that one file, so the rest of the commit stays as it was, and the header of
a widened file says what it is showing. The menu has the same two, as
**More Context** and **Less Context**, naming the file they would act on
and what it would then show.

Rows selected with `Shift-↑↓` and `d` diff the commits of a range on one
branch. Any two commits, wherever they are, are diffed with **Mark for
Diff** in the commit menu of one, and **Diff with <id>** in the menu of the
other: the changes from the older of the two to the newer.

In a view narrower than 110 columns, the diff is in one column instead,
each change's removed lines above its added ones, as git writes them, and
a conflict's two sides one after the other, each named. `v` switches
between the two, for the diff it is pressed in.

On a terminal 150 columns wide or more, a diff has its files listed beside
it, one file too, the one the cursor is in marked, each with its lines
added and deleted, and e.g. `╎3` for three lines left out of the
commit. A file history lists its commits instead, which its top then leaves
out as the diff of a commit leaves out its files. `Tab` moves the keys to
the list, where `↑` and `↓` go from file to file, and `Tab`, `Enter` or
`Esc` back to the diff; a click on a file goes to it. `l` hides the list,
for the width it takes, and shows it again; the diffs after open as it was
left.

`h` hides the lines of the file the cursor is in, a file that is generated,
say, which is then drawn as its header alone, and `h` on it shows them
again, for the diff it is pressed in.

`]` and `[` go to the next and the previous change, a run of changed lines
or a conflict, and `}` and `{` to the next and the previous file, and the
line at the bottom says which it went to, e.g. `Change 3 of 9`. The top row
names the commit, and once the header of the file the view is scrolled into
is out of sight, that file, e.g. `f44b73c6  VAT rate › M Pricing.cs  +5 -5`.
`s` opens **Go to**, the same moves and every file, or
in a file history every commit.

Within the diff: `r` re-reads it from git, `u`
discards uncommitted changes (the change the cursor is on, the lines
selected, a file or all of them, see [Undo and Recovery](#undo-and-recovery)), `x` leaves the change the
cursor is on, or the lines selected, out of the next commit, or puts them
back (see Commit in [More Commands](#more-commands)), `c` commits, `Enter` resolves
a conflicted file, `m` or a right-click opens the menu, `←` `→` scroll the
two columns sideways, their line numbers staying where they are, and pick
which one a selection copies from, `Ctrl-C`
copies the selected lines, and `Esc` or `q` closes the view.

`f` or `/` searches the lines for a text, as it is typed and in any case,
and puts the cursor on the first line with it from the cursor down, on the
side it is on, scrolled sideways to it where it is further right. Every
place it is found is marked in black on yellow. `n` and `Shift-N` then go to
the next and the previous one from wherever the cursor is, and the line at
the bottom says which one it is, e.g. `Match 2 of 7`.

`w` hides the changes of whitespace alone, as GitHub's *Hide whitespace*
does, and `w` again shows them. A line whose spaces alone changed is then
unchanged, and the summary at the top says `whitespace hidden`. A file whose
changes are all whitespace is still listed, as `whitespace only` and with
no lines, so that it is not missed in what is committed, and can still be
discarded. It is for the diff it was pressed in: the next one shows
whitespace again. **Diff File**
in the menu opens an uncommitted file in the diff tool git is configured
with (`git difftool`).

**Blame File ...** in the commit menu picks a file and shows which commit
last changed each of its lines. Consecutive lines from the same commit are
bracketed together with `┌ │ └` (`╺` for a run of a single line) and that
commit is named once per run rather than on every line, which is what makes
this readable where the console `git blame` is not. The bracket and the
short id are shaded by age, from yellow for the newest commit through to
gray for the oldest, and lines that are not committed yet are bright yellow
and marked `©`.

Within the blame: `Enter` toggles the commit details of the current line,
the same pane the log view shows, which follows the cursor as you move down
the lines (`Tab` moves into it to scroll a long message). `d` shows the
diff of the current line's commit, or for a line not committed yet the
uncommitted changes, to read (they are committed and discarded from the
log), `l` the history of the lines selected with `Shift-↑↓`, or of the
current one: every commit that changed them, with the diff of those lines
alone (and no more context, which git does not give it), `p` blames the version before it (so a
reformat or a rename can be stepped past to the change that actually
matters) and `Backspace` steps back out again, `g` cycles how much of each
commit the left column names, `←` `→` scroll the code while the left column
stays put, `i` copies the current line's commit id and `Shift-I` its
message, as in the log, `f` or `/` searches the code and `n` and `Shift-N`
step through what it found, as in the diff, `Ctrl-C` copies the selected
lines, `m` opens the menu, and `Esc` or `q` closes the view.


## Resolving Conflicts

When a merge, rebase, cherry pick, revert or pull that gmd runs stops on
conflicts, a box says so and names the files; its **Resolve Conflicts**
opens the resolver described below on the first of them. For as long as the
operation lasts, the top bar says what is in progress, e.g.
`Merging: 1 conflict` in red, and in yellow once the conflicts are
resolved. A click on it, or `Shift-M` for the repo menu, offers Resolve
Conflicts, Continue, Skip and Abort.

The files are resolved one after the other. Saving a file with every
conflict decided marks it resolved and opens the next one still conflicted,
and `}` and `{` go to the next or previous one without saving, asking first
if there are decisions to keep. The header says which file it is, e.g.
`file 1 of 3`, and a terminal at least 150 columns wide lists the files at
the left, the resolved ones ticked; `Tab` moves into the list, where `↑`
`↓` and `Enter` open another, and `l` hides it and shows it again, as in the
diff, which the files after keep. A file with no text to merge, such as one a
side deleted, or a binary file, asks its question as its turn comes (see
below), and Cancel passes it over until the resolver is opened again. Once
no conflict is left, a box asks whether to finish: **Commit** opens the
commit dialog for a merge, a cherry pick or a revert, and **Continue** goes
on with a rebase. **Later** leaves it for the commit (`c`) or the repo
menu. Conflicts with nothing in progress, e.g. of a stash popped, have
nothing to finish, and no box asks.

The diff of the uncommitted changes (`d` on the `©` row) shows a conflicted
file as one row saying how many conflicts it has, since its sides are in
the resolver, where `Enter` on it opens it. With the cursor elsewhere,
`Enter` offers the list of the conflicted files, which is also under
**Resolve Conflicts** in the diff menu. **Run External Merge Tool** in the
diff menu opens a file in the tool git is configured with instead
(`git mergetool`).

The two sides are shown beside each other, titled by name rather than
"ours" and "theirs", since during a rebase those two mean the opposite of
what you would expect: the branch HEAD is on, e.g. `main (HEAD)`, and the
branch merged in, or during a rebase the commit being applied, each with
the subject of its commit, which says what it was doing. Each side is shown as what it did to
the version both started from, the common ancestor, which is what says why
they conflict: `+` a line it added, `~` a line it changed, with the words
it changed marked in its color, `-` a line of the ancestor's it removed, in
dark, and the lines it kept in dark too. A line is numbered as its side has
it, and the text between conflicts as the left side has it.

A pane below, **Result**, shows the file as it will be saved, as your
decisions so far make it: each decided conflict's lines with a bar in the
color of the side they came from, magenta where written by hand, and each
undecided one as a row saying which keys decide it. It goes along with the
cursor: the line the cursor is on is marked in it, at the same place in the
file, or the lines of the conflict the cursor is in, a third of the way down
the pane. Its line numbers are the saved file's. `Shift-↑` `↓` or the mouse
wheel over it scroll it on its own, until the cursor moves again.

The whole file is shown, not just its conflicts, since the text around a
conflict is what tells you what it is part of. The view opens on the first
conflict rather than at the top of the file, and `]` and `[` move to the
next and previous one from wherever the cursor is.

| Key        | Description                                                |
| ---------- | ---------------------------------------------------------- |
| 1  2       | Take the left or the right side                            |
| 3  4       | Take both, left first or right first                       |
| c          | Combine both sides' changes, where they touch other words  |
| 0          | Take the common ancestor, i.e. undo both sides' changes    |
| u          | Clear the decision on this conflict                        |
| e          | Type the result of this conflict by hand, Esc when done    |
| ]  [       | Next or previous conflict (n and p do the same)            |
| b          | Show the version both sides started from                   |
| a          | Whole file: take one side, or put the conflicts back       |
| s          | Save and mark the file resolved, then on to the next file  |
| }  {       | Next or previous file still conflicted                     |
| Tab        | Into the list of files, and back                           |
| l          | Hide or show the list of files                             |
| m          | Open the menu, which lists all of these                    |
| ←  →       | Scroll all the columns sideways                            |
| Shift-↑  ↓ | Scroll the result below, as the mouse wheel over it does   |
| Esc, q     | Close the resolver                                         |

Nothing is written until `s`, so closing without saving leaves the file as
it was, and closing or going to another file with decisions unsaved asks
first. A file saved with
conflicts still undecided keeps their markers, and is marked resolved once
none is left.

`b` shows the version both sides started from in a pane of its own,
between them, and `0` resolves the conflict *to* it, the answer when
neither change should have happened here. Git records that version in the
file only when `merge.conflictStyle` is `diff3` or `zdiff3`; otherwise gmd
works it out from the staged versions as the file opens, without touching
your files. Where it cannot, the sides are shown as they are, in their
colors. A file both sides created
has no such version, and says so. Where both sides added lines that were
not there before, the ancestor is empty, so `0` removes the region, which
is also how to drop a conflict you want gone.

`c` combines both sides' changes, for the conflict git could not merge
only because the lines the two changed touch: one side added a line above a
line the other changed, or each changed other words of the same line. Each
line takes the change made to it, and a line both changed takes the words
each changed. Where they change the same words, or both write lines in one
place, there is no one combination, and `c` says so; the result pane offers
it for each conflict it can be had for. It is never taken by itself, and `u`
takes it back.

`e` is for the merge that is neither side but something of both. It is
typed in the result pane, in place of the conflict's lines, with the file
around it and both sides in sight above. It starts from what the conflict
resolves to now, or if you have not chosen yet from both sides' changes
combined where `c` can combine them, and from both sides where not, and the
pane's title says which. The lines after it are numbered as they will be as
you type. `Esc` is done, as is `Tab` or a click on the sides, and what you
typed is the conflict's decision; with nothing typed it is left as it was.
`Ctrl-Z` undoes, and `u` afterwards clears the decision. Emptying the text
is how to delete the conflicted region outright.

A file with no text to merge is not shown in columns; it asks the one
question it can, and which question depends on which sides still have the
file. A binary file both sides changed asks which version to use. A text
file whose conflict markers were taken out by hand, in an editor, is marked
resolved as it is, or one side is taken after all. A file only one side has (one deleted it, or each renamed it differently) asks
whether to keep it or accept the deletion. A file neither side has any
longer can only be removed, and says so rather than offering a version that
is not there.

### Continue, Skip or Abort

When a merge, rebase, cherry pick or revert stops on conflicts, the repo
menu grows a section at the top naming what is in progress, how far it has
got and how many conflicts are left, e.g.
`Rebase 'dev' (1 of 2)  ·  1 conflict`.

**Continue** carries on once every conflicted file is resolved and marked
resolved (`git add`); a rebase over several commits may then stop again on
the next one, which is shown as the first stop was, with the files and the
way on. **Skip This Commit** drops the commit it
stopped on and carries on with the rest. **Abort** throws the whole
operation away and puts the working folder back as it was.

*Continue* is offered for whatever a commit does not finish, and only a
rebase or an `am` has commits to skip, so those items appear only where
they apply. A merge has no *Continue*, since committing is what finishes
one, and neither has gmd's own **Cherry Pick** of one commit or **Revert
Commit**, which stage one change for the commit dialog with nothing queued
behind it. Pressing `c` during a rebase, an `am`, a cherry pick of several
commits, or a revert of several commits offers **Continue** instead: committing
there would make the one commit git stopped on and leave the rest
unapplied.

Both *Commit* and *Continue* check the conflicts first. A file that is
still unresolved stops them, naming it. So does a file marked resolved that
still contains `<<<<<<<`, since marking resolved is only `git add`, and git
does not look at what it stages, so without the check those markers go into
history. That one can be overridden if it is really what you want.


## Worktrees

A worktree is a folder with a checkout of the repository. The repository's
own folder is the main worktree; `git worktree add` (or Claude Code's
`--worktree`) adds linked ones, which share the commits and branches but
each have their own checked out branch and uncommitted changes. Gmd shows
one worktree at a time: what is shown, committed, diffed and pushed is the
folder gmd was started in, or opened since.

- `⌂` in the margin and after a branch tip means that branch is checked out
  in another worktree. Git allows a branch in one worktree only, so it
  cannot be checked out, pulled or deleted from here; the `s` key opens
  that worktree instead of switching to the branch.
- `⌂N` in the top bar counts the other worktrees. It turns yellow when one
  of them has uncommitted changes, which is checked right after the
  repository is read and every thirty seconds after that.
- `w` opens the worktrees dialog: one row per worktree with its branch,
  changes, whether it is in use (locked, e.g. by a running Claude Code
  session) or missing (its folder is gone), and whether its branch is
  merged. From there a worktree can be opened (`Enter`), added, removed or
  pruned, and its path copied.
- **Add ...** (or **Create Worktree ...** in a branch menu) creates a
  worktree for an existing branch or a new one, beside the repository
  (`<repo>-<branch>`), in Claude Code's `.claude/worktrees/` or in
  `.worktrees/`. The two inside the repository are added to `.gitignore`
  by default, else the main worktree shows them as untracked files.
- **Remove ...** removes a worktree and offers to delete its branch with it,
  checked when the branch is merged. Uncommitted changes in the worktree
  are only discarded with *Force*. A worktree in use cannot be removed.
- **Prune** forgets the worktrees whose folders were deleted by hand.

Every worktree of a repository shares one gmd config: which branches are
shown, their colors and their order.


## How Gmd Picks a Commit's Branch

Git stores only where each branch is now, its tip. A commit is not tied to
a branch, so once branches are merged and deleted, git cannot say which
branch a commit was made on. Gmd works it out, so that every branch can
keep a column of its own:

- Where git's local reflog still says which branch a commit was made on,
  or which branch a branch was started from, that decides, and is kept in
  the branch metadata for when the reflog has expired.
- Merge messages name the branches that were merged, e.g. "Merge branch
  'dev' into main", which brings back branches deleted long ago.
- Otherwise, where branches meet, the one the others were started from
  goes on: main, master or trunk, then an integration branch such as
  develop or dev, then a release, hotfix or support branch, then a branch
  that clearly more other branches were merged into. A repo's own
  integration branches, e.g. staging, can be named in **Config ...** in
  the repo menu.

When it cannot decide, the branch is drawn white and its tip is labeled
`(~ambiguous)`. **Set Commit Branch Manually ...** in the branch menu sets
the right branch for a commit, which is then marked `Φ`. **Ambiguous** in
the Show Branch menu lists the ambiguous branches.

The branches set by hand can be shared: **Config ...** in the repo menu
can push them with the repository, so that everyone sees the same graph.


## Problems and Feedback

Gmd writes what it does to `gmd.log` in your home folder, which is the
first place to look when something goes wrong. Report a problem, or
suggest something, at
[github.com/michael-reichenauer/gmd/issues](https://github.com/michael-reichenauer/gmd/issues),
with the version, from **About** in the repo menu or `gmd --version`. About
shows where the log is, too. The log is begun anew on every start of gmd,
so copy it before starting gmd again.

When git needs a password, a passphrase or whether to trust a host, gmd
asks in a dialog, for a push, a pull or a fetch (`r`) you asked for. The
fetch gmd runs in the background asks nothing, and says that `r` would.
An ssh key added to the ssh agent (`ssh-add`), or a git credential helper,
e.g. Git Credential Manager, lets git log in without asking at all.
