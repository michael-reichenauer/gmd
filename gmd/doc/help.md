# Gmd Help Guide

What is on the screen comes first, then the everyday tasks, then the
reference. Scroll with ↑↓, PgUp and PgDn, Space or the mouse wheel. `m`
lists the sections to jump to, `]` and `[` go to the next and the previous
one, and Esc closes. Opened in the diff, blame or conflict view, the help
starts at the part about it.

- Reading the Log
- Showing and Hiding Branches
- Everyday Tasks
- Keys
- Symbols
- Remote and Local Branches
- Undo and Recovery
- More Commands
- Diff and Blame
- Resolving Conflicts
- Worktrees
- How Gmd Picks a Commit's Branch
- Problems and Feedback


## Reading the Log

Each branch is drawn in a column and a color of its own, with main running
down the left. Git does not record which branch a commit was made on, so
gmd works it out (see How Gmd Picks a Commit's Branch, near the end).

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
on the same commit (see Remote and Local Branches).

`Enter` shows the details of the commit below the log, and `Tab` moves
into them and back.

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

    Gmd ┅/acme-store, ●main, ©2, ▼1, ▲1, ✦3, ß1, ⌂1  (dev) [Ϙ Search] ? X

Each item can be clicked:

-------------------------------------------------------------------------
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
| (dev)          | The highlighted branch, or the row's: show a branch  |
| Ϙ Search ? X   | Search (also f), help (also ?), and quit             |
-------------------------------------------------------------------------

The line at the bottom shows the keys that do something where the cursor
is, and changes as it moves; the diff, blame and conflict views have one of
their own. **Config ...** in the repo menu turns them off.
While a commit, push or pull runs it says what is being done, in cyan. For
a few seconds after a command it says what the command did, or why a key
or a greyed out menu item did nothing, and in red when a fetch failed.


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
- `h` hides the highlighted branch, and the branches made from it.
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
  branch or tag, or `file:` and part of a path.
- **Show another branch:** `Shift-→`, and type part of its name.
- **Undo a mistake:** **Undo** in the commit menu (`m`) takes back the last
  change of the branch you are on, e.g. a commit, a merge or a pull.
- **Fix a commit not pushed yet:** `m` on it, then **Amend <id> ...** for a
  new message, or the changes you tick, or both, or **Drop <id>** under
  **Undo** to take it out of the branch. `a` amends the last commit.


## Keys

The most used keys of the log view. The menus show the key of every command
that has one, and pressing it in an open menu picks that item. Keys are
written as they are typed: `c` is the c key, and `Shift-P` is P, which the
menus and the line at the bottom write `⇧p`, ⇧ being Shift.

---------------------------------------------------------------------------
| Key        | Description                                                |
| ---------- | ---------------------------------------------------------- |
| m          | Menu of the highlighted branch, or of the commit           |
| Shift-M    | The repo menu, e.g. to continue or abort a rebase          |
| ← →        | Highlight the branch to the left or right, or the commit   |
| Shift-→    | Show Branch menu: type part of a name to find a branch     |
| Enter      | Toggle commit details (on a branch: show/hide branches)    |
| Tab        | Move between the log and the commit details                |
| c          | Commit the uncommitted changes                             |
| a          | Amend the last commit, while it is not pushed              |
| d          | Diff of the commit (on a branch: diff the branch to ...)   |
| Ctrl-D     | Diff of the commit or the selected rows, even on a branch  |
| s          | Switch to the highlighted branch                           |
| b          | Create a branch from the highlighted branch or the commit  |
| e          | Merge the highlighted branch into the current branch       |
| Shift-E    | Merge the current branch into the highlighted branch       |
| h          | Hide the highlighted branch                                |
| Backspace  | Undo the last show or hide of branches                     |
| g          | Change the color of the highlighted branch                 |
| p          | Push the highlighted branch, or the current one            |
| u          | Pull the highlighted branch, or the current one            |
| Shift-P    | Push all shown branches                                    |
| Shift-U    | Pull all shown branches                                    |
| t          | Add a tag to the commit                                    |
| f, /       | Search and filter the commits                              |
| n, Shift-N | The next and the previous match of the last search         |
| r, F5      | Refresh, and fetch from the remote                         |
| w          | Open the worktrees dialog                                  |
| o          | Open, clone or init a repository                           |
| y          | Go to the current branch, showing it if it is hidden       |
| PgUp, PgDn | A page up or down (Space too), in every list and view      |
| Home, End  | The first or the last row                                  |
| Shift-↑↓   | Select rows in the log, diff and blame views               |
| Ctrl-C     | Copy the selected rows to the clipboard                    |
| i          | Copy the commit id to the clipboard                        |
| Shift-I    | Copy the commit message to the clipboard                   |
| ?, F1      | Open this help page                                        |
| Esc        | Close a menu, dialog or view                               |
| Esc, q     | Quit, in the log view (Esc asks first)                     |
---------------------------------------------------------------------------

In dialogs and text fields:

---------------------------------------------------------------------------
| Key        | Description                                                |
| ---------- | ---------------------------------------------------------- |
| Alt-O      | OK, e.g. to commit while typing in the message box         |
| Ctrl-D     | Show the diff of what is committed, in the commit dialog   |
| Ctrl-A     | After a merge, add the merged commits' subjects            |
| Space, a   | Untick or tick a file, or all, in the commit file list     |
| F7         | Spelling suggestions (also Ctrl-G)                         |
| Shift-F10  | Text menu: spelling, copy, paste (also right-click)        |
| Esc        | Cancel                                                     |
---------------------------------------------------------------------------

The mouse works too: hovering highlights a branch, right-click opens the
menu of a branch or a commit (and the menu of the diff, blame and conflict
views), double-click switches to a branch (or toggles the details of a
commit), and middle-click merges a branch into the current branch, after
asking. The items in the top bar can be clicked as well: `▲` and `▼` open
a menu to push or pull the current branch or all of them.


## Symbols

-------------------------------------------------------------------------
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
| ^      | Abbreviation for 'origin' in branch names                    |
| ~      | Deleted branch (inactive, drawn in gray)                     |
| o      | Branch shown in the graph (in branch menus)                  |
| ✦      | Hidden branches with something new (top bar)                 |
| ⇓      | Available update to download (top bar)                       |
| ┅      | Truncated name/text                                          |
| ┌ │ └  | Blame lines from the same commit (see Diff and Blame)        |
| ╺      | A blame run of one single line                               |
-------------------------------------------------------------------------


## Remote and Local Branches

A branch and its branch on origin are drawn as two columns, origin's on the
left. On the same commit they are `┣─┺`, with the tips `(^)(● main)`. When
they differ, each column has the commits only it has, and the subjects say
which way they have to go:

     ╭┺ ●▲Add zeta                   (● main)
    ┣╯    Add delta            (^/main)[v1.0]

- `▲` and a green subject: a commit to push.
- `▼` and a blue subject: a commit to pull.

`p` and `u` push and pull the highlighted branch, or the current one when
none is, as **Push** and **Pull** in its branch menu do. A push leaves the
uncommitted changes where they are; a pull needs them committed or stashed
first. Switching to a branch where git would overwrite them offers to stash
them, switch, and put them back. `Shift-P` and `Shift-U` push or update all
shown branches.

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


## Undo and Recovery

The ways back are under **Undo** in the commit menu (`m`). The items that
throw changes away for good ask first.

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
- **Discard Changes in a File** (also `u` in the diff of the uncommitted
  changes): `git checkout --force HEAD -- <file>`, staged changes too, or
  a new file is unstaged and deleted
- **Revert Commit**: `git revert --no-commit <commit-sha>`
- **Drop <commit-sha>**: takes a commit not pushed yet out of the branch,
  `git rebase --onto <commit-sha>~ <commit-sha>`, asked first. The commits
  after it are rewritten without it, and Undo brings it back. Not for a
  merge, or past one, nor with another branch or a tag on it or after it,
  which would keep the old commits.
- **Uncommit Last Commit**: `git reset HEAD~1`, the changes stay uncommitted
- **Uncommit <commit-sha> and Newer**: `git reset --soft <commit-sha>~`, so
  that commit and the ones after it are uncommitted
- **Discard All Changes**: `git reset --hard` and `git clean -fd`
- **Discard All Changes and Ignored Files**: `git reset --hard` and
  `git clean -fxd`, i.e. the folder as a fresh clone of the last commit
  has it, to check that nothing left over makes a build or a test pass.
  The files git ignores go too, also those kept on purpose, such as a
  .env file of secrets, so the question lists everything it deletes.


## More Commands

- **Commit ...** (`c`):
  Commit the uncommitted changes, every file ticked in the dialog's list,
  with warnings for large or binary files. While a merge, a revert or the
  like is in progress there is no list: git commits its result whole.
  After a merge made in gmd, `Ctrl-A` in the dialog adds the subjects of
  the merged commits, one per line, and for a merged merge the list in its
  message.
- **Amend ...** (`a`) and **Amend <commit-sha> ...**:
  `a` amends the last commit while it is not pushed: its message, and the
  files ticked in the dialog. The commit menu of an older commit not pushed
  yet has an Amend of its own, with that commit's message and the changed
  files, none ticked at first: a new message, the ticked changes, or both.
  It is git's own way: an "amend!" commit of them, folded in by
  `git rebase -i --autosquash`, with the files left unticked put aside and
  back. Undo takes it back. It has the limits Drop has, in Undo and
  Recovery. A later commit that conflicts with the changes stops the rebase
  there: resolve and continue, or abort, which leaves them in the "amend!"
  commit on top.
- **Search ...** (`f` or `/`):
  Type to filter the log down to the commits that match, by id, message
  (the subject and the body), branch, author, date (yyyy-mm-dd) or tag.
  Every word has to match, a "quoted phrase" matches as a whole, and case
  does not matter. `file:` and a path, e.g. `file:Program.cs`, matches the
  commits that changed a file with that in its path. `Enter` shows the
  selected commit and its branch in the log, and `Esc` goes back to where
  you were. After that, `n` and `Shift-N` go to the next and the previous
  match in the log.
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
  squash them into one commit with a new message.
- **Stash** (in the commit menu, and `ß` in the top bar):
  **Stash Changes ...** puts the uncommitted changes aside, new files too.
  **Stash Pop** brings a stash back and drops it, **Stash Apply** brings it
  back and keeps it, e.g. for the same changes on another branch as well,
  and **Stash Diff** and **Stash Drop** show it and throw it away.
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
  (see How Gmd Picks a Commit's Branch).

Find more commands in the menus (the `m` key).


## Diff and Blame

**Commit Diff** (`d`) shows the changes of a commit side by side. `+` shows
more of the file the cursor is on around its changes and `-` shows less,
stepping from the 6 lines of context it starts with to 15, and then to the
whole file (and `=` does what `+` does, without the shift). It applies to
that one file, so the rest of the commit stays as it was, and the header of
a widened file says what it is showing. The menu has the same two, as
**More Context** and **Less Context**, naming the file they would act on
and what it would then show.

Within the diff: `r` re-reads it from git, `s` scrolls to a file, `u`
discards the changes of an uncommitted file, `c` commits, `Enter` resolves
a conflicted file, `m` or a right-click opens the menu, `←` `→` scroll the
two columns sideways and pick which one a selection copies from, `Ctrl-C`
copies the selected lines, and `Esc` or `q` closes the view. **Diff File**
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
diff of the current line's commit, `p` blames the version before it (so a
reformat or a rename can be stepped past to the change that actually
matters) and `Backspace` steps back out again, `g` cycles how much of each
commit the left column names, `←` `→` scroll the code while the left column
stays put, `i` copies the current line's commit id and `Shift-I` its
message, as in the log, `Ctrl-C` copies the selected lines, `m` opens the
menu, and `Esc` or `q` closes the view.


## Resolving Conflicts

When a merge, rebase, cherry pick, revert or pull that gmd runs stops on
conflicts, a box says so and names the files; its **Resolve Conflicts**
opens the diff described below. For as long as the operation lasts, the top
bar says what is in progress, e.g. `Merging: 1 conflict` in red, and in
yellow once the conflicts are resolved. A click on it, or `Shift-M` for the
repo menu, offers Resolve Conflicts, Continue, Skip and Abort.

To resolve the conflicts, open the diff of the uncommitted changes (`d` on
the `©` row) and press `Enter` on a conflicted file. That opens the file
the cursor is on, or the list of the conflicted files if the cursor is
elsewhere. The same list is under **Resolve Conflicts** in the diff menu,
and it names every conflicted file, including those the diff cannot show,
such as one a side deleted, or a binary file. **Run External Merge Tool**
in the diff menu opens a file in the tool git is configured with instead
(`git mergetool`).

The two sides are shown beside each other, titled with the names git wrote
into the markers (`HEAD` and `topic`, or a commit id during a rebase)
rather than "ours" and "theirs", since during a rebase those two mean the
opposite of what you would expect. A pane below shows what the conflict
under the cursor resolves to as you decide.

The whole file is shown, not just its conflicts, since the text around a
conflict is what tells you what it is part of. The view opens on the first
conflict rather than at the top of the file, and `]` and `[` move to the
next and previous one from wherever the cursor is.

---------------------------------------------------------------------------
| Key        | Description                                                |
| ---------- | ---------------------------------------------------------- |
| 1  2       | Take the left or the right side                            |
| 3  4       | Take both, left first or right first                       |
| 0          | Take the common ancestor, i.e. undo both sides' changes    |
| u          | Clear the decision on this conflict                        |
| e          | Edit the result of this conflict by hand                   |
| ]  [       | Next or previous conflict (n and p do the same)            |
| b          | Show the version both sides started from                   |
| a          | Whole file: take one side, or put the conflicts back       |
| s          | Save and mark the file resolved                            |
| m          | Open the menu, which lists all of these                    |
| ←  →       | Scroll all the columns sideways                            |
| Esc, q     | Close the resolver                                         |
---------------------------------------------------------------------------

Nothing is written until `s`, so closing without saving leaves the file as
it was, and closing with decisions unsaved asks first.

`b` shows the version both sides started from, which is usually what
settles which change to keep, and `0` resolves the conflict *to* it, the
answer when neither change should have happened here. Git records that
version in the file only when `merge.conflictStyle` is `diff3` or `zdiff3`;
otherwise gmd works it out on demand from the staged versions, without
touching your files, and shows it as it takes it. A file both sides created
has no such version, and says so. Where both sides added lines that were
not there before, the ancestor is empty, so `0` removes the region, which
is also how to drop a conflict you want gone.

`e` is for the merge that is neither side but something of both. A box
opens holding what the conflict resolves to now, or both sides if you have
not chosen yet, and it says which of the two it gave you. Emptying the box
is how to delete the conflicted region outright. `Tab` moves from the box
to the buttons, since Enter inside it is a newline.

A file with no text to merge is not shown in columns; it asks the one
question it can, and which question depends on which sides still have the
file. A binary file both sides changed asks which version to use. A file
only one side has (one deleted it, or each renamed it differently) asks
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
the next one, and it says so. **Skip This Commit** drops the commit it
stopped on and carries on with the rest. **Abort** throws the whole
operation away and puts the working folder back as it was.

*Continue* is offered for whatever a commit does not finish, and only a
rebase or an `am` has commits to skip, so those items appear only where
they apply. A merge has no *Continue*, since committing is what finishes
one, and neither has gmd's own **Cherry Pick** or **Revert Commit**, which
stage one change for the commit dialog with nothing queued behind it.
Pressing `c` during a rebase, an `am`, a cherry pick started outside gmd,
or a revert of several commits offers **Continue** instead: committing
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

    https://github.com/michael-reichenauer/gmd/issues

with the version, from **About** in the repo menu or `gmd --version`. About
shows where the log is, too. The log is begun anew on every start of gmd,
so copy it before starting gmd again.

When git needs a password, a passphrase or whether to trust a host, gmd
asks in a dialog, for a push, a pull or a fetch (`r`) you asked for. The
fetch gmd runs in the background asks nothing, and says that `r` would.
An ssh key added to the ssh agent (`ssh-add`), or a git credential helper,
e.g. Git Credential Manager, lets git log in without asking at all.
