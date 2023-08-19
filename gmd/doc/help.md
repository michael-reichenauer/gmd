# Gmd Help Guide

## Keyboard Shortcuts

Here are some essential keyboard shortcuts:

| Key      | Description                                                |
| -------- | ---------------------------------------------------------- |
| M        | Display command menu (varies based on highlighted item)    |
| ←        | Highlight the branch on the left                           |
| →        | Highlight the branch on the right or a commit              |
| Esc      | Close a menu or dialog                                     |
| Esc/Q    | Exit application in log view                               |
| Tab      | Toggle between repo and commit details views               |
| C        | Open commit dialog                                         |
| D        | Display commit diff                                        |
| Ctrl+D   | Show commit diff within commit dialog                      |
| Enter    | Reveal commit details                                      |
| Ctrl+O   | Activate 'OK' buttons in dialogs                           |
| ? / F1   | Open this help page                                        |

*More shortcuts are visible within the application menus.*

### Symbols in Views

| Symbol | Description                                           |
| ------ | ----------------------------------------------------- |
| ●      | Current commit and branch                             |
| ©      | Uncommitted changes (in yellow)                       |
| *      | Detached commit (commit checked out, not a branch)    |
| ^      | Abbreviation for 'origin' in branch names             |
| ~      | Deleted branch (inactive)                             |
| o      | Branch displayed in menus                             |
| ▼      | Commit not yet pulled (blue subject)                  |
| ▲      | Commit not yet pushed (green subject)                 |
| ß      | Stash based on commit                                 |
| ⇓      | Available update to download (use menu)               |
| ┅      | Truncated name/text                                   |
| ╯/╮    | Unseen branch branching out or merging at commit      |
| ╂┸     | Synced remote and local branch tips                   |
| Φ      | Manually set branch for that commit                   |

## Branches Graphs

The branch graph on the left visualizes the selected branches. Navigate between branches using the `←` and `→` keys and open the branch-specific menu with `M` when a branch is highlighted.

### Indicators for Hidden Branches

Symbols '╮' and '╯' next to a branch hint at unseen branches merging in or branching out. Use the mouse or `Enter` key to toggle their visibility.

### Remote and Local Branches

A 'double' branch with the '╂┸' tip indicates both local and remote branches. The left side represents the remote branch, and the right, the local branch. They align if synced, and misalign if:

- Remote has unpulled commits (blue lines with a '▼')
- Local has unpushed commits (green lines with a '▲')

Use menu options or the `P` and `U` keys to synchronize. `Shift-P` and `Shift-U` can push or update all displayed branches.

### Current Commit/Branch

Symbols:

- '●' marks the current commit and branch.
- '©' denotes uncommitted changes.
- '*' indicates a detached current commit.

### Branch Tips

Branch tips appear on the right of the subject. Long branch names are shortened, and the full names can be viewed in the commit details (toggled with `Enter`). The symbol `~` highlights a deleted but still accessible branch.

## Noteworthy Commands:

- **Toggle Details ...** (`Enter`): Displays additional commit details.
- **Commit ...** (`C`): Commit any uncommitted changes with warnings for large or binary files.
- **Commit Diff ...** (`D`): View a side-by-side diff of commit changes.
- **Undo Options**:
  - **Restore Uncommitted File**: `git checkout --force -- <file-path>`
  - **Undo Commit**: `git revert --no-commit <commit-sha>`
  - **Uncommit Last Commit**: `git reset HEAD~1`
  - **Clean/Restore Working Folder**: Reset with `git reset --hard` and clean using `git clean -fxd`.
- **Merge**: Highlight a branch and merge into the current branch. Use `Commit` post-merge.
- **Set Commit Branch Manually**: For commits where the branch is ambiguous, this command resolves the uncertainty.

*Find more commands in the menu (`M` key).*

## Ambiguous Branches

In Git, branch tips are the only items stored. A commit isn't inherently tied to a specific branch. Gmd analyzes branch structures and merge messages to ascertain a commit's most likely branch. When it's challenging to decide, the branch appears white, labeled as "ambiguous". The `Set Commit Branch Manually` command lets users manually set the correct branch.

