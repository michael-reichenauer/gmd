# gmd Help

## Keyboard Shortcuts

Some useful keyboard shortcuts are:

| Key        | Description                                     |
| ---------- | ------------------------------------------------|
| M          | Shows the main menu with all available commands |
| RightArrow | Shows menu to show and switch branch            |
| LeftArrow  | Show menu to hide branches                      |
| Esc        | Close a menu or a dialog                        |
| Esc        | Quit the application in repo view               |
| Tab        | Switch between repo and commit details views    |
|            |                                                 |
| C          | Shows the commit dialog                         |
| D          | Shows the commit diff view in repo and commit   |
| Ctrl+D     | Shows the commit diff in commit dialog          |
| Enter      | Shows commit details                            |
| Ctrl+O     | To trigger click on 'OK' buttons in dialogs     |

More shortcut keys are available and and mentioned in the
menus.

## Branches Graph

The branches graph on the left side visualizes the selected
branches. More or less branches can be selected by using the
menus ('`M`', `RightArrow` or `LeftArrow`) or clicking in the
graph.

### Indicators for not Shown Branches

Seemingly cutoff start or end branch gray indicators '`╮`' and '`╯`' to the right
of a branch, indicates a merge in or branch out of a currently not yet
shown branch. Use the `RightArrow` on such a commit line to show the
branches menu, and make it easy open them.

## Commands

* Clean/Restore Working Folder:\
  Ensures a working folder is as if folder just has been checked out. 
  I.e. restores all uncommitted files and remove all files not tracked
  by git using:\
  `> git reset --hard`\
  `> git clean -fxd`
* Undo Restore Uncommitted File:\
  Restores an uncommitted file using:\
  `> git checkout --force -- <file-path>`
* Undo Commit:\
  Creates a new commit, which is the 'opposite' of the selected commit
  using:\
  `> git revert --no-commit <commit-sha>`
