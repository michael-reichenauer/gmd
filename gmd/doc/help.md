# Gmd Help


## Keyboard Shortcuts
Some especially useful keyboard shortcuts are:

| Key        | Description                                                 |
| ---------- | ------------------------------------------------------------|
| m          | Shows the main menu with all available commands             |
| RightArrow | Shows menu to open/show and switch branch                   |
| LeftArrow  | Show menu to close branches                                 |
| Esc        | Close a menu or a dialog                                    |
| Esc        | Quit the application in repo view                           |
| Tab        | Switch between repo and commit details views (if shown)     |
|            |                                                             |
| c          | Shows the commit dialog                                     |
| d          | Shows the commit diff view in repo and commit               |
| Ctrl+d     | Shows the commit diff in commit dialog                      |
| Enter      | Shows commit details                                        |
| Ctrl+o     | To trigger click on 'OK' buttons in dialogs                 |
----------------------------------------------------------------------------

More shortcut keys are available and indicated in the menus.


## Branches Graphs
The branches graph on the left side visualizes the selected
branches. More or less branches can be shown by clicking the
'`RightArrow`', '`LeftArrow`' or '`m`' key.


### Indicators for not Shown Branches
Seemingly cutoff start or end branch indicators '╮' and '╯' to the right
of a branch, indicates a merge in or branch out for not yet shown branches. 
Use the '`RightArrow`' key on such a commit line to show the branches menu,
and select branch to open.


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
(keys `'p'` and `'u'` key will push, update/pull current branch)\
(keys `'P'` and `'U'` key will push, update/pull all shown branches)


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
* *Commit ...* (`'c'`)\
  Commit uncommitted changes. A warning for large or binary files is shown.
* *Commit Diff ...* (`'d'`)\
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

In those cases, the user can use the `Resolve Ambiguity` command in the
main menu. 

  