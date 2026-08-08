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
| Enter    | Show commit details                                        |
| Ctrl+O   | Activate 'OK' buttons in dialogs                           |
| ? / F1   | Open this help page                                        |
| Shift+↑↓ | Select commit rows in the log and diff views               |
| Ctrl+C   | Copy the selected rows to the clipboard                    |
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
-------------------------------------------------------------------------


## Branches Graphs

The branch graph on the left visualizes the selected branches. Navigate 
between branches using the `←` and `→` keys and open the branch-specific 
menu with `M` when a branch is highlighted. The same menu is also reachable 
without highlighting a branch first, under **Branches** in the commit menu: 
it lists every branch currently shown in the graph, and below them the 
options to show and hide branches.

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
  uncommitted file, `C` commits, `M` opens the menu, `←` `→` scroll the two
  columns sideways and pick which one a selection copies from, `Ctrl-C`
  copies the selected lines, and `Esc` or `Q` closes the view.
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
  Use `Commit` post-merge.
- **Merge to** (`Shift-E`): 
  The other direction, i.e. merge the current branch into the highlighted 
  one. Git can only merge into the branch that is checked out, so gmd 
  switches to the target branch, merges, opens the commit dialog there, and 
  switches back once the merge is committed. Cancelling the commit, or a 
  merge that conflicts, leaves you on the target branch, which is where the 
  merge has to be finished.
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

