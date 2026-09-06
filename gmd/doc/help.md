# Gmd Help Guide

### Keyboard Shortcuts

Here are some essential keyboard shortcuts:

-------------------------------------------------------------------------
| Key      | Description                                                |
| -------- | ---------------------------------------------------------- |
| M        | Display command menu (varies based on highlighted item)    |
| ←        | Highlight the branch on the left                           |
| →        | Highlight the branch on the right or a commit              |
| Esc      | Close a menu or dialog                                     |
| Esc / Q  | Exit application in log view                               |
| Tab      | Toggle between repo and commit details views               |
| C        | Open commit dialog                                         |
| D        | Display commit diff                                        |
| Ctrl+D   | Show commit diff within commit dialog                      |
| F7       | Spelling suggestions in commit dialog (also Ctrl+G)        |
| Enter    | Show commit details                                        |
| Ctrl+O   | Activate 'OK' buttons in dialogs                           |
| ? / F1   | Open this help page                                        |
| Shift+↑↓ | Select commit rows in the log and diff views               |
| Ctrl+C   | Copy the selected rows to the clipboard                    |
| W        | Open the worktrees dialog                                   |
-------------------------------------------------------------------------
*More shortcuts are visible within the application menus.*


### Symbols in Views
-------------------------------------------------------------------------
| Symbol | Description                                                  |
| ------ | -------------------------------------------------------------|
| ●      | Current commit and branch                                    |
| ©      | Uncommitted changes (in yellow)                              |
| *      | Detached commit (commit checked out, not a branch)           |
| ^      | Abbreviation for 'origin' in branch names                    |
| ~      | Deleted branch (inactive, drawn in gray)                     |
| o      | Branch displayed in menus                                    |
| ▼      | Commit not yet pulled (blue subject)                         |
| ▲      | Commit not yet pushed (green subject)                        |
| ß      | Stash based on commit                                        |
| ⇓      | Available update to download (use menu)                      |
| ┅      | Truncated name/text                                          |
| ┣╮ ┣╯  | Unseen branch merging in or branching out at commit          |
| Φ      | Manually set branch for that commit                          |
| ╂┸     | Synced remote and local branch tips                          |
| ┌ │ └  | Blame lines from the same commit (see 'Blame File ...')      |
| ╺      | A blame run of one single line                               |
| ⌂      | Branch checked out in another worktree (see Worktrees)        |
-------------------------------------------------------------------------


## Branches Graphs

The branch graph on the left visualizes the selected branches. Navigate 
between branches using the `←` and `→` keys and open the branch-specific 
menu with `M` when a branch is highlighted. The same menu is also reachable 
without highlighting a branch first, under **Branches** in the commit menu: 
it lists every branch currently shown in the graph, the current branch and 
its parent branches first, and below them the options to show/hide branches 
and to pull/update or push all branches.

### Indicators for Hidden Branches

Symbols ┣╮ and ┣╯ next to a branch hint at unseen branches merging in or 
branching out. Use the mouse or `Enter` key to toggle their visibility.

### Remote and Local Branches

A 'double' branch with the ╂┸ tip indicates both local and remote branches.
The left side represents the remote branch, and the right, the local branch.
They are on same row if synced, and on different rows if there are commits
that can be pulled or pushed.

- Remote has unpulled commits (▼ and a blue subjects)
- Local has unpushed commits (▲ and a green subjects)

Use menu options or the `P` and `U` keys to synchronize. 
`Shift-P` and `Shift-U` keys can push or update all displayed branches.

A branch with both unpulled and unpushed commits cannot be pushed, nor updated
by `Shift-U`, which only fast-forwards the branches it is not on. Switch to the
branch and pull it (`U`) to merge the two sides.

### Current Commit/Branch

Symbols:

- '●' marks the current commit and branch.
- '©' denotes uncommitted changes and a yellow subject (red if conflicts).
- '*' indicates a detached current commit.

### Branch Tips

Branch tips appear on the right of the subject. Long branch names are
shortened, and the full names can be viewed in the commit details
(toggled with `Enter`).
The symbol `~` highlights a deleted but still accessible branch. Such a branch
is no longer active, so it is drawn in gray and its color cannot be changed.


## Worktrees

A worktree is a folder with a checkout of the repository. The repository's
own folder is the main worktree; `git worktree add` (or Claude Code's
`--worktree`) adds linked ones, which share the commits and branches but each
have their own checked out branch and uncommitted changes. Gmd shows one
worktree at a time: what is shown, committed, diffed and pushed is the folder
gmd was started in, or opened since.

- `⌂` after a branch tip means that branch is checked out in another
  worktree. Git allows a branch in one worktree only, so it cannot be checked
  out, pulled or deleted from here; the `S` key opens that worktree instead
  of switching to the branch.
- `⌂N` in the top bar counts the other worktrees. It turns yellow when one of
  them has uncommitted changes, which is checked every thirty seconds.
- `W` opens the worktrees dialog: one row per worktree with its branch,
  changes, whether it is in use (locked, e.g. by a running Claude Code
  session) or missing (its folder is gone), and whether its branch is merged.
  From there a worktree can be opened (`Enter`), added, removed or pruned,
  and its path copied.
- **Add...** creates a worktree for an existing branch or a new one, beside
  the repository (`<repo>-<branch>`), in Claude Code's `.claude/worktrees/`
  or in `.worktrees/`. The two inside the repository are added to
  `.gitignore`, else the main worktree shows them as untracked files.
- **Remove...** removes a worktree and offers to delete its branch with it,
  checked when the branch is merged. Uncommitted changes in the worktree are
  only discarded with *Force*. A worktree in use cannot be removed.
- **Prune** forgets the worktrees whose folders were deleted by hand.

Every worktree of a repository shares one gmd config: which branches are
shown, their colors and their order.


## Noteworthy Commands:

- **Toggle Details ...**
  (`Enter`): Displays additional commit details.
- **Commit ...** (`C`):
  Commit any uncommitted changes with warnings for large 
  or binary files.
- **Commit Diff ...** (`D`):
  View a side-by-side diff of commit changes.
  Within the view: `+` shows more of the file the cursor is on around its
  changes and `-` shows less, stepping 6 lines of context, then 15, then the
  whole file (and `=` does what `+` does, without the shift). It applies to
  that one file, so the rest of the commit stays as it was, and the header of
  a widened file says what it is showing. The menu has the same two, as
  **More Context** and **Less Context**, naming the file they would act on
  and what it would then show.
  `R` re-reads the diff from git, `S` scrolls to a file, `U` restores an
  uncommitted file, `C` commits, `Enter` resolves a conflicted file, `M` opens
  the menu, `←` `→` scroll the two columns sideways and pick which one a
  selection copies from, `Ctrl-C` copies the selected lines, and `Esc` or `Q`
  closes the view.
- **Undo Options**:
  - **Restore Uncommitted File**: `git checkout --force -- <file-path>`
  - **Undo Commit**: `git revert --no-commit <commit-sha>`
  - **Uncommit Last Commit**: `git reset HEAD~1`
  - **Clean/Restore Working Folder**: Reset with `git reset --hard` 
    and clean using `git clean -fxd`.
- **Blame File ...**:
  Pick a file and see which commit last changed each of its lines. Consecutive
  lines from the same commit are bracketed together with `┌ │ └` (`╺` for a run
  of a single line) and that commit is named once per run rather than on every
  line, which is what makes this readable where the console `git blame` is not.
  The bracket and the short id are shaded by age, from yellow for the newest
  commit through to gray for the oldest, and lines that are not committed yet
  are bright yellow and marked `©`.
  Within the view: `Enter` toggles the commit details of the current line, the
  same pane the log view shows, which follows the cursor as you move down the
  lines (`Tab` moves into it to scroll a long message). `D` shows the diff of
  the current line's commit, `P` blames the version before it (so a reformat or
  a rename can be stepped past to the change that actually matters) and
  `Backspace` steps back out again, `I` cycles how much of each commit the left
  column names, `←` `→` scroll the code while the left column stays put, `C`
  copies the current line's commit id, and `Ctrl-C` copies the selected lines.
- **Merge**: 
  Highlight a branch and merge into the current branch (`E`). 
  Use `Commit` post-merge, or **Abort Merge** in the repo menu to back out.
- **Merge to** (`Shift-E`): 
  The other direction, i.e. merge the current branch into the highlighted 
  one. Git can only merge into the branch that is checked out, so gmd 
  switches to the target branch, merges, opens the commit dialog there, and 
  switches back once the merge is committed. Cancelling the commit, or a 
  merge that conflicts, leaves you on the target branch, which is where the 
  merge has to be finished.
- **Resolve Conflicts** (`Enter` in the diff view):
  Opens the conflicted file the cursor is on, or the list of them if the cursor
  is elsewhere. The same list is under **Resolve Conflicts** in the diff menu,
  and it names every conflicted file — including those the diff cannot show,
  such as one a side deleted, or a binary file.

  The two sides are shown beside each other, titled with the names git wrote
  into the markers (`HEAD` and `topic`, or a commit id during a rebase) rather
  than "ours" and "theirs" — during a rebase those two mean the opposite of
  what you would expect. A pane below shows what the conflict under the cursor
  resolves to as you decide.

  The whole file is shown, not just its conflicts, since the text around a
  conflict is what tells you what it is part of. The view opens on the first
  conflict rather than at the top of the file, and `]` and `[` move to the next
  and previous one from wherever the cursor is.

-------------------------------------------------------------------------
| Key      | Description                                                |
| -------- | ---------------------------------------------------------- |
| 1  2     | Take the left or the right side                            |
| 3  4     | Take both, left first or right first                       |
| 0        | Take the common ancestor, i.e. undo both sides' changes    |
| U        | Un-decide this conflict                                    |
| E        | Edit the result of this conflict by hand                   |
| ]  [     | Next or previous conflict (N and P do the same)            |
| B        | Show the version both sides started from                   |
| A        | Whole file: take one side, or put the conflicts back       |
| S        | Save and mark the file resolved                            |
| M        | Open the menu, which lists all of these                    |
| ←  →     | Scroll all the columns sideways                            |
| Esc / Q  | Close the resolver                                         |
-------------------------------------------------------------------------

  Nothing is written until `S`, so closing without saving leaves the file as it
  was, and closing with decisions unsaved asks first.

  `B` shows the version both sides started from, which is usually what settles
  which change to keep, and `0` resolves the conflict *to* it — the answer when
  neither change should have happened here. Git records that version in the file
  only when `merge.conflictStyle` is `diff3` or `zdiff3`; otherwise gmd works it
  out on demand from the staged versions, without touching your files, and shows
  it as it takes it. A file both sides created has no such version, and says so.
  Where both sides added lines that were not there before, the ancestor is empty,
  so `0` removes the region — which is also how to drop a conflict you want gone.

  `E` is for the merge that is neither side but something of both. A box opens
  holding what the conflict resolves to now, or both sides if you have not
  chosen yet, and it says which of the two it gave you. Emptying the box is how
  to delete the conflicted region outright. `Tab` moves from the box to the
  buttons, since Enter inside it is a newline.

  A file with no text to merge is not shown in columns; it asks the one
  question it can, and which question depends on which sides still have the
  file. A binary file both sides changed asks which version to use. A file only
  one side has — one deleted it, or each renamed it differently — asks whether
  to keep it or accept the deletion. A file neither side has any longer can
  only be removed, and says so rather than offering a version that is not there.

- **Continue / Skip / Abort**:
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
  rebase has commits to skip, so those items appear only where they apply. A
  merge has no *Continue* — committing is what finishes one — and neither has
  gmd's own **Cherry Pick** or **Undo Commit**, which stage one change for the
  commit dialog with nothing queued behind it.
  Pressing `C` during a rebase, an `am`, a cherry pick started outside gmd, or
  a revert of several commits offers **Continue** instead: committing there
  would make the one commit git stopped on and leave the rest unapplied.
  Both *Commit* and *Continue* check the conflicts first. A file that is still
  unresolved stops them, naming it. So does a file marked resolved that still
  contains `<<<<<<<` — marking resolved is only `git add`, and git does not
  look at what it stages, so without the check those markers go into history.
  That one can be overridden if it is really what you want.
- **Rename Branch ...**:
  Renames the branch with `git branch -m`, which also works on the current
  branch, without checking anything out. A published branch is renamed on the
  remote as well, by pushing the new name and then deleting the old remote
  branch. Note that deleting it affects everyone: other clones lose track of
  the branch and a pull request made from the old name is closed.
- **Set Commit Branch Manually**: 
  For commits where the branch is ambiguous, this command resolves the
  uncertainty.

*Find more commands in the menu (`M` key).*


## Ambiguous Branches

In Git, branch tips are the only items stored. A commit isn't inherently
tied to a specific branch. Gmd analyzes branch structures and merge messages to 
ascertain a commit's most likely branch. When it's challenging to decide, 
the branch appears white, labeled as "ambiguous". 
The `Set Commit Branch Manually` command lets users manually set the 
correct branch for a commit.

