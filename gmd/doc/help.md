# Gmd Help Guide

## Keyboard Shortcuts

The most used keys of the log view. The menus show the key of every command
that has one, and pressing it in an open menu picks that item. Keys are
written as they are typed: `c` is the c key, and `Shift-P` is P.

---------------------------------------------------------------------------
| Key        | Description                                                |
| ---------- | ---------------------------------------------------------- |
| m          | Menu of the highlighted branch, or of the commit           |
| Shift-M    | The repo menu, e.g. to continue or abort a rebase          |
| ← →        | Highlight the branch to the left or right, or the commit   |
| Shift-→    | Show Branch menu, to choose which branches are shown       |
| Enter      | Toggle commit details (on a branch: show/hide branches)    |
| Tab        | Move between the log and the commit details                |
| c          | Commit the uncommitted changes                             |
| a          | Amend the last commit, while it is not pushed              |
| d          | Diff of the commit (on a branch: diff the branch to ...)   |
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
| Shift-↑↓   | Select rows in the log, diff and blame views               |
| Ctrl-C     | Copy the selected rows to the clipboard                    |
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
| Ctrl-A     | After a merge, add the merged commits' messages            |
| F7         | Spelling suggestions (also Ctrl-G)                         |
| Shift-F10  | Text menu: spelling, copy, paste (also right-click)        |
| Esc        | Cancel                                                     |
---------------------------------------------------------------------------

The mouse works too: hovering highlights a branch, right-click opens the
menu of a branch or a commit, double-click switches to a branch (or toggles
the details of a commit), and middle-click merges a branch into the current
branch, after asking. The items in the top bar can be clicked as well: `▲`
and `▼` open a menu to push or pull the current branch or all of them.

The line at the bottom of the log view shows the keys that do something
where the cursor is, and changes as it moves. **Config ...** in the repo
menu turns it off. For a few seconds after a command it says what the
command did, or why a key or a greyed out menu item did nothing, and in red
when a fetch failed.


## Symbols in Views

-------------------------------------------------------------------------
| Symbol | Description                                                  |
| ------ | ------------------------------------------------------------ |
| ●      | Current commit and branch                                    |
| ©      | Uncommitted changes (in yellow)                              |
| *      | Detached commit (commit checked out, not a branch)           |
| ⌂      | Commit and branch checked out in another worktree            |
| ^      | Abbreviation for 'origin' in branch names                    |
| ~      | Deleted branch (inactive, drawn in gray)                     |
| o      | Branch shown in the graph (in branch menus)                  |
| ▼      | Commit not yet pulled (blue subject)                         |
| ▲      | Commit not yet pushed (green subject)                        |
| ▽      | New commits on hidden branches, since last shown (top bar)   |
| ß      | Stash based on commit                                        |
| ⇓      | Available update to download (use menu)                      |
| ┅      | Truncated name/text                                          |
| ┣╮ ┣╯  | Hidden branch merging in or branching out at commit          |
| Φ      | Manually set branch for that commit                          |
| ╂┸     | Synced remote and local branch tips                          |
| ┌ │ └  | Blame lines from the same commit (see 'Blame File ...')      |
| ╺      | A blame run of one single line                               |
-------------------------------------------------------------------------


## Branch Graph

The graph on the left shows the branches you have chosen to show. Move
between them with `←` and `→`, and open the menu of the highlighted branch
with `m`. The same menus are reachable without highlighting a branch, under
**Branches** in the commit menu: it lists every branch shown in the graph,
the current branch and its parent branches first, and below them the items
to show and hide branches, and to pull/update or push all of them.

### Showing and Hiding Branches

Which branches are shown is up to you, which is how gmd gives a clean log
without rebasing or squashing. Showing a branch also shows the branches it
was made from, and the main branch is always shown.

- `Shift-→` opens **Show Branch**: first the branches that merge in or
  branch out at the current commit, then the Recent, Active, My Active
  (where the last commit is yours), Active and Deleted, and Ambiguous
  branches.
- Typing in that menu, or in its sub menus, opens **Find Branch** with what
  was typed. The list narrows as more of the name is typed, every word has
  to be in it, and the names it starts a part of come first. `Enter` or a
  click shows the branch.
- ┣╮ and ┣╯ beside a commit mean that a hidden branch merges in or branches
  out there. `Enter` on the branch, or a click, shows it (or opens a menu
  when there are several), and hides it again.
- `h` hides the highlighted branch, and the branches made from it.
  **Hide All Branches** goes back to showing just the main branch.
- `Backspace` undoes the last show or hide, going back to the branches shown
  before it, one step at a time. **Undo** under **Branches** in the commit
  menu does the same, and names what it would undo.
- ▽ in the top bar counts the commits pushed to hidden branches since you
  last had them shown. A click lists those branches, and **New Commits** at
  the top of **Show Branch** does too; showing one is seeing it. **Mark All
  as Seen** clears the rest without showing them.
- The `<=` and `=>` items in a branch menu move the branch to the left or
  the right of a branch it overlaps in the graph.

The shown branches, their colors and their order are remembered per
repository.

### Remote and Local Branches

A 'double' branch with the ╂┸ tip indicates both local and remote branches.
The left side is the remote branch, and the right side the local branch.
They are on the same row if synced, and on different rows if there are
commits that can be pulled or pushed.

- Remote has unpulled commits (▼ and a blue subject)
- Local has unpushed commits (▲ and a green subject)

`p` and `u` push and pull the highlighted branch, or the current one when
none is, as **Push** and **Pull** in its branch menu do. A push leaves the
uncommitted changes where they are; a pull needs them committed or stashed
first. Switching to a branch where git would overwrite them offers to stash
them, switch, and put them back. `Shift-P` and
`Shift-U` push or update all shown branches.

A branch with both unpulled and unpushed commits can only be pushed by
force, which `p` asks about first, and is not updated by `Shift-U`, which
only fast-forwards the branches it is not on. Switch to the branch and pull
it (`u`) to join the two sides. The first time, gmd asks whether to merge
them or to rebase your commits on top, unless git's `pull.rebase` already
says, and saves the answer there, for git on the command line as well.

**Open in Browser** in a branch menu opens the branch on the site hosting
the remote (GitHub, GitLab, Bitbucket, Azure DevOps or Gitea), and **Create
Pull Request in Browser** the page for a pull request into the branch it
was made from. The commit menu has **Open Commit in Browser**, and the repo
menu **Open Repository in Browser**. `$BROWSER` is used when it is set, and
with no browser to open, e.g. over ssh, the link is copied instead.

### Current Commit/Branch

Symbols:

- '●' marks the current commit and branch.
- '©' denotes uncommitted changes and a yellow subject (red if conflicts).
- '*' indicates a detached current commit.
- '⌂' marks a commit and branch checked out in another worktree.

### Branch Tips

Branch tips appear on the right of the subject. Long branch names are
shortened, and the full names can be viewed in the commit details
(toggled with `Enter`).
The symbol `~` highlights a deleted but still accessible branch. Such a
branch is no longer active, so it is drawn in gray and its color cannot be
changed.


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


## Noteworthy Commands

- **Commit Details** (`Enter`):
  Displays additional commit details.
- **Commit ...** (`c`):
  Commit all uncommitted changes, with warnings for large or binary files.
  After a merge made in gmd, `Ctrl-A` in the dialog adds the messages of
  the merged commits.
- **Commit Diff** (`d`):
  View a side-by-side diff of commit changes.
  Within the view: `+` shows more of the file the cursor is on around its
  changes and `-` shows less, stepping from the 6 lines of context it
  starts with to 15, and then to the whole file (and `=` does what `+`
  does, without the shift). It applies to that one file, so the rest of the
  commit stays as it was, and the header of a widened file says what it is
  showing. The menu has the same two, as **More Context** and
  **Less Context**, naming the file they would act on and what it would
  then show.
  `r` re-reads the diff from git, `s` scrolls to a file, `u` restores an
  uncommitted file, `c` commits, `Enter` resolves a conflicted file, `m`
  opens the menu, `←` `→` scroll the two columns sideways and pick which one
  a selection copies from, `Ctrl-C` copies the selected lines, and `Esc` or
  `q` closes the view.
- **Search ...** (`f` or `/`):
  Type to filter the log down to the commits that match, by id, message
  (the subject and the body), branch, author, date (yyyy-mm-dd) or tag.
  Every word has to match, a "quoted phrase" matches as a whole, and case
  does not matter. `file:` and a path, e.g. `file:Program.cs`, matches the
  commits that changed a file with that in its path. `Enter` shows the
  selected commit and its branch in the log, and `Esc` goes back to where
  you were. After that, `n` and `Shift-N` go to the next and the previous
  match in the log. `*` finds the ambiguous branch tips, and `$` the
  commits whose branch was set manually.
- **Squash ...** (under **Rebase** in the commit menu):
  Select a range of commits on the current branch with `Shift-↑↓`, and
  squash them into one commit with a new message.
- **Undo** (in the commit menu, and `u` in the diff of the uncommitted
  changes). The items that throw changes away for good ask first:
  - **Discard Changes in a File**: `git checkout --force HEAD -- <file>`,
    staged changes too, or a new file is unstaged and deleted
  - **Revert Commit**: `git revert --no-commit <commit-sha>`
  - **Uncommit Last Commit**: `git reset HEAD~1`, the changes stay uncommitted
  - **Uncommit <commit-sha> and Newer**: `git reset --soft <commit-sha>~`, so
    that commit and the ones after it are uncommitted
  - **Discard All Changes**: `git reset --hard` and
    `git clean -fd`
- **Clean Working Folder** (in the repo menu, asks first):
  `git reset --hard` and `git clean -fxd`, which also deletes the files git
  ignores.
- **Blame File ...**:
  Pick a file and see which commit last changed each of its lines.
  Consecutive lines from the same commit are bracketed together with
  `┌ │ └` (`╺` for a run of a single line) and that commit is named once per
  run rather than on every line, which is what makes this readable where
  the console `git blame` is not. The bracket and the short id are shaded
  by age, from yellow for the newest commit through to gray for the oldest,
  and lines that are not committed yet are bright yellow and marked `©`.
  Within the view: `Enter` toggles the commit details of the current line,
  the same pane the log view shows, which follows the cursor as you move
  down the lines (`Tab` moves into it to scroll a long message). `d` shows
  the diff of the current line's commit, `p` blames the version before it
  (so a reformat or a rename can be stepped past to the change that
  actually matters) and `Backspace` steps back out again, `i` cycles how
  much of each commit the left column names, `←` `→` scroll the code while
  the left column stays put, `c` copies the current line's commit id,
  `Ctrl-C` copies the selected lines, `m` opens the menu, and `Esc` or `q`
  closes the view.
- **Merge**:
  Highlight a branch and merge it into the current branch (`e`).
  Use `Commit` post-merge, or **Abort Merge** at the top of the repo menu
  to back out.
- **Merge to** (`Shift-E`):
  The other direction, i.e. merge the current branch into the highlighted
  one. Git can only merge into the branch that is checked out, so gmd
  switches to the target branch, merges, opens the commit dialog there,
  and switches back once the merge is committed. Cancelling the commit, or
  a merge that conflicts, leaves you on the target branch, which is where
  the merge has to be finished.
- **Rename Branch ...**:
  Renames the branch with `git branch -m`, which also works on the current
  branch, without checking anything out. A published branch is renamed on
  the remote as well, by pushing the new name and then deleting the old
  remote branch. Note that deleting it affects everyone: other clones lose
  track of the branch and a pull request made from the old name is closed.
- **Set Commit Branch Manually ...**:
  For commits where the branch is ambiguous, this command resolves the
  uncertainty.

Find more commands in the menus (the `m` key).


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


## Ambiguous Branches

In Git, branch tips are the only items stored. A commit isn't inherently
tied to a specific branch. Gmd analyzes branch structures and merge
messages to find a commit's most likely branch. When it cannot decide, the
branch is drawn white and its tip is labeled `(~ambiguous)`.
**Set Commit Branch Manually ...** sets the right branch for a commit,
which is then marked `Φ`. Search for `*` to find the ambiguous branch tips,
and for `$` to find the commits whose branch was set manually.
