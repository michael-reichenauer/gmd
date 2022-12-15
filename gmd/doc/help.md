# gmd Help


## Keyboard Shortcuts
Some especially useful keyboard shortcuts are:

| Key        | Description                                                 |
| ---------- | ------------------------------------------------------------|
| M          | Shows the main menu with all available commands             |
| RightArrow | Shows menu to open/show and switch branch                   |
| LeftArrow  | Show menu to close branches                                 |
| Esc        | Close a menu or a dialog                                    |
| Esc        | Quit the application in repo view                           |
| Tab        | Switch between repo and commit details views (if shown)     |
|            |                                                             |
| C          | Shows the commit dialog                                     |
| D          | Shows the commit diff view in repo and commit               |
| Ctrl+D     | Shows the commit diff in commit dialog                      |
| Enter      | Shows commit details                                        |
| Ctrl+O     | To trigger click on 'OK' buttons in dialogs                 |

More shortcut keys are available and and indicated in the menus.


## Branches Graphs
The branches graph on the left side visualizes the selected
branches. More or less branches can be shown by clicking the
keys `RightArrow`, `LeftArrow` or '`M`'.


### Indicators for not Shown Branches
Seemingly cutoff start or end branch indicators '`╮`' and '`╯`' to the right
of a branch, indicates a merge in or branch out of not yet shown branches. 
Use the `RightArrow` on such a commit line to show the branches menu,
and select to open.


### Remote and Local Branches
When there are both local and their corresponding remote (origin) branches,
it is indicated by a 'double' branch with e.g. '`╂┸`' tip. The left is the 
remote branch and the right the local branch. They are on the same row
if synced and on different rows e.g:
* If remote has 'unpulled' commits 
  (indicated with blue subject lines and preceded by a `'▼'`)
* If local has 'unpushed' commits
  (indicated with green subject lines and preceded by a `'▲'`)

Use push and update/pull to sync 
(keys `'p'` and `'u'` will push, update/pull all shown branches)


### Current Commit/Branch
A `'●'` indicates the current commit and current branch.

### Branch Tips
Branch tips are shown to the right of the subject. Long branch names are
truncated. If branches are synced, the `'^|'` in e.g. `(^|main)`, indicates
the origin/remote branch and the rest the local branch. Full names are
shown in commit details view. A `'~'` indicates that the branch was deleted,
i.e. no longer an active git branch, but still possible to open and se.


## Some Notable Commands
* Toggle Details ... (`'Enter'`)\
  Shows some more commit details at the bottom. E.g. to se full commit message.\
  Use tab to focus details to scroll and tab to focus log view again.
* Commit ... (`'C'`)\
  Commit uncommitted changes. A warning for large of binary files is shown.
* Commit Diff ... (`'D'`)\
  Shows a side-by-side diff of all changes in the commit.
* Undo | Clean/Restore Working Folder:\
  Ensures a working folder is as if folder just has been checked out. 
  I.e. restores all uncommitted files and remove all files not tracked
  by git uses:\
  `> git reset --hard`\
  `> git clean -fxd`
* Undo | Restore Uncommitted File:\
  Restores an uncommitted file uses:\
  `> git checkout --force -- <file-path>`
* Undo | Undo Commit:\
  Creates a new commit, which is the 'opposite' of the selected commit, uses:\
  `> git revert --no-commit <commit-sha>`
* Undo | Uncommit Last Commit\
  The last (unpushed) commit can be uncommitted, e.g. to adjust commit
  message or change committed files. Uses:\
  `> git reset HEAD~1`
*  Merge into 'current-branch' from\
   Select the branch (or commit) to merge into the current branch.\
   After merge, use `Commit` to commit the merge\
   (must be done manually after merge).

More commands are available in the main menu `'M'`.

  