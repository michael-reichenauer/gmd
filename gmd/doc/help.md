# Gmd Help


## Keyboard Shortcuts
Some especially useful keyboard shortcuts are:

| Key        | Description                                                 |
| ---------- | ------------------------------------------------------------|
| M          | Shows a menu with available commands when commit or         |  
|            | branch is highlighted                                       |
| LeftArrow  | Highlights branch to the left                               |
| RightArrow | Highlights branch to the right (or commit)                  |
| Esc        | Close a menu or a dialog                                    |
| Esc or Q   | Quit the application in log view                            |
| Tab        | Switch between repo and commit details views (if shown)     |
| C          | Shows the commit dialog                                     |
| D          | Shows the commit diff view in log view                      |
| Ctrl+D     | Shows the commit diff in commit dialog                      |
| Enter      | Shows commit details                                        |
| Ctrl+O     | To trigger click on 'OK' buttons in dialogs                 |
| ? or F1    | Show this help page                                         |
----------------------------------------------------------------------------

More shortcut keys are available and indicated in the menus.\

Some signs/characters used in the views:

| Sign | Description                                                       |
| -----| ------------------------------------------------------------------|
| ●    | The current commit and current branch                             |  
| *    | The current commit is detached (checked out a commit)             |
| ^    | Short for 'origin' in branch names, e.g. '^/main' = 'origin/main' |
| ~    | Indicates the branch is deleted                                   |
| ▼    | Indicates the commit has not yet been pulled (blue line)          |
| ▲    | Indicate the commit has not yet been pushed (green line)          |
| ©    | Indicates current uncommitted changes (yellow)                    | 
| ⇓    | Indicates an updated release is available to download (use menu)  |
| ┅    | Indicates the name/text has been truncated.                       |
| ╯    | A branch not yet shown is branching out at the commit in graph    |
| ╮    | A Branch not yet shown is merging in at the commit int graph      |
| ╂┸   | Remote and local branch tips are synced (no need to pull/push)    |
| Φ    | The branch for that commit has been manually set                  |
----------------------------------------------------------------------------


## Branches Graphs
The branches graph on the left side visualizes the selected
branches. Highlight branches using the '`LeftArrow`' and '`RightArrow`' keys.
Show branch specific menu using the '`M`' key.


### Indicators for not Shown Branches
Seemingly cutoff start or end branch indicators '╮' and '╯' to the right
of a branch, indicates a merge in or branch out for not yet shown branches.
Click using the mouse or highlight the commit in the branch and press '`Enter`'
to show/hide branch. Also top and bottom branch commits will hide the branch.


### Remote and Local Branches
When there are both local and their corresponding remote (origin) branches,
it is indicated by a 'double' branch with e.g. a '╂┸' tip. The left is the 
remote branch and the right the local branch. They are on the same row
if synced and on different rows:
- If remote has 'unpulled' commits 
  (indicated with blue subject lines and preceded by a '▼')
- If local has 'unpushed' commits
  (indicated with green subject lines and preceded by a '▲')

Use push and update/pull in main menu to sync 
(keys `'P'` and `'U'` key will push, update/pull current branch)\
(keys `'Shift-P'` and `'Shift-U'` key will push, update/pull all shown branches)


### Current Commit/Branch
A '●' indicates the current commit and current branch.\
A '*' indicates the current commit is detached (checked out a commit).


### Branch Tips
Branch tips are shown to the right of the subject. Long branch names are
truncated. If branches are synced, the `'^|'` in e.g. `(^)(main)`, indicates
the origin/remote branch and the rest the local branch. Full names are
shown in commit details view. A `'~'` indicates that the branch was deleted,
i.e. no longer an active git branch, but still possible to open and see and
will be restored/resumed if user switches to that branch.


## Some Notable Commands:
* *Toggle Details ...* (`'Enter'`)\
  Shows some more commit details at the bottom. E.g. to see full commit
  message. Use tab to focus details to scroll and tab to focus log view again.
* *Commit ...* (`'C'`)\
  Commit uncommitted changes. A warning for large or binary files is shown.
* *Commit Diff ...* (`'D'`)\
  Shows a side-by-side diff of all changes in the commit.
* *Undo | Clean/Restore Working Folder*:\
  Ensures a working folder is as if folder just has been checked out. 
  I.e. restores all uncommitted files and remove all files not tracked
  by git uses:\
  `> git reset --hard`\
  `> git clean -fxd`
* *Undo | Restore Uncommitted File*:\
  Restores an uncommitted file uses:\
  `> git checkout --force -- <file-path>`
* *Undo | Undo Commit*:\
  Creates a new commit, which is the 'opposite' of the selected commit, uses:\
  `> git revert --no-commit <commit-sha>`
* *Undo | Uncommit Last Commit*\
  The last (unpushed) commit can be uncommitted, e.g. to adjust commit
  message or change committed files. Uses:\
  `> git reset HEAD~1`
* *Merge into 'current-branch' from*\
  Select the branch (or commit) to merge into the current branch.\
  After merge, use `Commit` to commit the merge\
  (must be done manually after merge).
* *Resolve Ambiguity*\
  For branches with commits, where branch is ambiguous (white branch line)
  commits,  it is possible to select the actual branch and resolve the 
  ambiguity. It is also possible to undo/regret a previous resolved commit
  (marked with a 'Ф' after the subject for a resolved commit).

More commands are available in the main menu (`'m'` key).


## Ambiguous Branches
Git only stores branch tips and according to git, a commit does not really
'belong' to a specific branch, it is part of all branches with a tip 
reachable from that commit. Yet, at the time of the commit, the commit
was committed to a branch, but that info is then lost. Visualizing branches
is therefore difficult. Most git client just visualize a commit as part of a
one of the possible branches and that can change for every change to a
repo. This is confusing and not really how most people think about the repo.

Gmd tries to analyze the branch structure and merge messages to determine
which branch a commit most likely belongs to. If Gmd is confident, the commit
get a branch color. But in some cases, it is hard to determine and thus the
branch color will be white and commits and branch is marked as `ambiguous`;

In those cases, the user can use the `Set Branch Manually` command in the
branch menu. 

  