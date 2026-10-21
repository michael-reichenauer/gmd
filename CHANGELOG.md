# Change Log for Gmd

What changed for users in each release, newest first. A release includes the previews before it.
Releases with no change users notice are left out.

## [Unreleased]

### Improved
- Step the context with `+` and `-`, and hide a file's lines with `h`, on the first file where a diff opens, without moving to a file first
- Group the diff menu, and leave **Commit**, **Discard Changes** and the conflict items out of the diff of a commit
- Say in the diff of a merge that it is against its first parent
- Name the last commit's amend **Amend Last Commit of main ...** in the menu of another commit
- Add a merge's subjects to its message with `Ctrl-L`, as the commit dialog says, and only once; `Ctrl-A` selects all there, as in other fields
- Cut a long folder path at its start in the folder browser, so its last folder shows
- Start the note of a branch merged on origin with **Delete Merged Branches**, which a narrow terminal cut off before
- Hint `d resolve…` on the uncommitted row with conflicts, since `d` asks first
- Label the name field of **Create Branch** and **Rename Branch**

### Changed
- Remove the `--changelog` command line option; the change log is here, and Gmd's update question links to it

### Fixed
- Typing a branch name fast after `Shift-→` opened a Find Branch dialog per letter
- The lines of the resolver's result below an undecided conflict were numbered unlike its first side

## [v1.14.1452.819] - 2026-10-21

### New
- Publish a branch that is not on origin yet with **Publish** in its branch menu, e.g. one created with its Publish box unticked
- Put a branch deleted on origin back with **Publish** in its branch menu, after asking

### Improved
- Step the diff's context 15 lines at a time with `+` and `-`, starting from 6 lines, around the changes of the file at the cursor; the diff says when the whole file is shown. The diff menu's **Show More Context** and **Show Less Context** do the same, **Show All** shows the whole file, and `-` goes back to the context before it
- Show more context with `+` on the `⋯` between two parts of a file; `Enter` no longer does
- Show the row counting the files of a diff, e.g. `4 files changed  4 modified  +8 -8`, right under the author and date, so every diff starts one row higher

### Changed
- `p` no longer publishes a branch; it says to use **Publish** instead

### Fixed
- **Push** in a branch menu is greyed out when there is nothing to push, rather than pushing nothing

## [v1.13.1450.1146] - 2026-10-19

### New
- Draw in Gmd's own colors: twelve branch colors, with a dark or light palette picked from the terminal's background, or the terminal's own 16 colors. Choose with **Colors** in the Config dialog
- Color the code in the diff, the blame and the conflict resolver by language, as VS Code's Dark+ and Light+ themes do, for about fifty languages. It can be turned off in Config
- Amend, drop and squash commits that are already pushed, and push the branch with force along with it. Gmd asks first on main, develop or a release branch, and for someone else's commits
- Move commits made on the wrong branch, e.g. main, to a new branch with **Move to New Branch** in the commit menu, and move a commit above or below its neighbor with **Reorder**
- Reset the current branch to any commit, keeping the changes uncommitted or discarding them, from **Undo** in the commit menu
- Mark branches squash or rebase merged on origin with `✓`, and delete local branches merged into main, including by a squash on origin, with **Delete Merged Branches** in the repo menu
- Resolve all the conflicted files of a merge, rebase or other operation in one view, where the next file opens when one is done. Unsaved decisions are kept for each file until the view closes, `↑` and `↓` in the file list show each file at once, and **Unresolve This File** (`a`) puts a resolved file's conflicts back
- Blame the file at the cursor's line and commit with `b` in a diff, and show a file's history at a blamed line's commit with `Shift-L`; `Esc` goes back
- Pick the file for **Blame File** and **File History** in a new file browser: the files the commit changed first, then all its files as a tree, and type any part of a path to find a file
- Pick a folder to open, clone into, init or put a worktree in with a similar folder browser, as large as the terminal, which lists the folders of a typed path as a shell completes it
- Mark the commit chosen for a diff with `±` in the log, its row tinted, and remove the mark with **Unmark for Diff**; a diff of selected commits says how many in its title

### Improved
- Keep the color a branch is first drawn in, chosen to differ from its parent and its neighbors, so showing, hiding, pushing or merging branches never changes it
- Draw emoji in commit subjects as emoji
- Offer to push with force when `p` is pressed after a squash, amend or rebase of pushed commits, made in Gmd or by another tool, rather than pull back the old version origin still has
- **Rebase and Push onto** is now **Rebase onto**, which asks whether to push with force too
- Start a squash's message with the oldest commit's message and list the others below it, as GitHub writes a squash merge
- Cherry pick the commits selected with `Shift-↑` and `Shift-↓` as they were made, keeping their authors, and take them all back with one **Undo**
- Offer **Abort Merge**, **Abort Rebase** and so on first in the commit menu's **Undo** while one is in progress
- Tell a branch deleted on origin from one never pushed, so `p` no longer publishes it again unasked
- Ask, when `d` is pressed on the uncommitted row with conflicts, whether to resolve them or show the diff; `Ctrl-D` shows the diff at once
- Show more of the lines left out of a diff with `Enter` on its `⋯`, as `+` does, and keep the cursor's line in place on the screen as `+` and `-` change the context
- Mark removed and added lines with `-` and `+` in a one-column diff, and a file's header with a bright cyan line
- Scroll the diff, the blame and the resolver sideways with a mouse's sideways wheel, or with `Shift` and the wheel, which no longer scrolls the log and lists up and down
- Group the commit and branch menus by what their items do, and the **Undo** menu by undoing the last change, undoing commits, discarding and bringing back. **Set Commit Branch Manually** is in the commit menu, **Show Branch** in the repo menu, and **Full File History** is now **File History**
- Make **Discard All Changes** one item, whose question lists the ignored files and offers to delete them too
- Edit the commit message with `Ctrl-A`, `Ctrl-C`, `Ctrl-X`, `Ctrl-V`, `Ctrl-Z` and `Ctrl-Y`, as in the subject field
- Keep the branches set by hand in `.git/gmd-metadata.json` rather than in a ref in the repository, moved there at the first change
- Push only what concerns commits origin already has when the branch structure is shared, so the names of unpushed branches and unpushed work stay on your machine; Config says what sharing pushes before it is turned on
- List in the README everything Gmd stores and sends

## [v1.12.1448.496] - 2026-10-17

### New
- Go to the next or previous change with `]` and `[`, and to the next or previous file with `}` and `{`; the top row names the commit and the file in view
- Show the diff in one column on a terminal narrower than 110 columns, and switch between one and two columns with `v`
- List the files beside a diff on a terminal at least 150 columns wide, with the lines added, deleted and left out of the commit, rather than at the top of the diff. `Tab` moves into the list and `l` hides it
- Hide the lines of a file in the diff with `h`, e.g. a generated file
- Resolve the files of a merge, rebase, cherry pick or revert one after the other, listed beside the resolver, from the conflict box, the top bar or the repo menu, and commit or continue once none is left
- Combine both sides of a conflict with `c` where they changed different words, or lines that only touch
- Type a conflict's resolution by hand in the result pane with `e`, with both sides still in view

### Improved
- Draw the diff more calmly: a bar beside each changed line, whole differing words marked rather than scattered letters, a row for each file, and a `⋯` line where unchanged lines are left out, naming the function git finds the next part in
- Show each side of a conflict as what it changed from the common ancestor, with its changed words marked, named by its branch and its commit's subject
- Show the whole file below the sides as it will be saved, following the cursor, with each conflict's lines marked by the side they came from

## [v1.11.1447.386] - 2026-10-16

### New
- Commit part of a file: `x` in the diff of the uncommitted changes leaves the change at the cursor, or the selected lines, out of the commit, which takes the rest and leaves them uncommitted
- Discard part of a file: `u` in the diff offers the change at the cursor, or the selected lines, above the whole files
- Bring back what was discarded, a change, a file, all changes or a dropped stash, with **Restore Discarded Changes** in the **Undo** menu, for about 30 days
- Search the lines of a diff or a blame with `f` or `/`, with the matches marked, and step through them with `n` and `Shift-N`
- Hide whitespace-only changes in a diff with `w`; a file with nothing else is listed as whitespace only

### Improved
- Commit and discard some lines of a renamed file, which stays renamed, and of a deleted file
- Sign the commit a squash makes when git's config signs commits, as every other commit Gmd makes is
- Say on their own status line why a greyed out item of the diff's or the blame's menu cannot be used, rather than behind them

## [v1.10.1446.1006] - 2026-10-15

### Improved
- Give a commit's message most of the details pane, with its fields in four rows, and name the branch of a parent or child on another branch, so a merge says what it merged
- List the files a commit changed in its details, with the lines added and deleted in each, and the uncommitted files in theirs, conflicted files first
- Ask what to copy with `Ctrl-C` in the log: a commit's id, message, subject or link, or the selected rows' ids, messages or range
- Show commit ids with eight characters rather than six, as `git log --oneline` does in all but small repositories
- Show the latest 50,000 commits of a large repository rather than 30,000, in no more memory than before
- Read a large repository's log much faster when it has no commit-graph file, by writing one, as `git gc` would
- Decide the branches of long ambiguous stretches of history in a fraction of the time, e.g. 2 s rather than 17 s for 200,000 commits of rust
- Keep main's line through a merged tag or another project's branch pulled in, e.g. git's translations and gitk
- Recover a deleted branch named in merge subjects where other branches were started from it, leaving far fewer commits ambiguous
- Name a branch after a tag or a loose word of a merge subject only when nothing else names it
- Treat a branch and a fork's copy of it, e.g. `timfel/1.2.x`, as one branch where they meet
- Let a branch started on the merge of the previous branch share that branch's column

### Fixed
- A branch set by hand is kept by its commit's full id, so it never applies to another commit with the same first characters. One set with an earlier version needs to be set again

## [v1.9.1446.426] - 2026-10-15

### Improved
- Grey out **Auto update** and **Allow preview releases** in Config for a Gmd installed with Homebrew, and say why
- Say after the PowerShell install on Windows that Gmd already runs in that terminal

### Fixed
- A Gmd installed with Homebrew, which has the stable releases only, is never offered a preview

## [v1.8.1445.1141] - 2026-10-14

### New
- Install Gmd on Windows with one PowerShell command, into your own folder and PATH, with no admin rights needed
- Install Gmd in a dev container with the feature `ghcr.io/michael-reichenauer/gmd/gmd:1`

### Improved
- Leave updating a Gmd installed with winget to winget, showing its command at `⇓`, to run once Gmd is closed

## [v1.7.1445.1049] - 2026-10-14

### New
- Install Gmd with Homebrew on macOS and Linux, `brew install michael-reichenauer/tap/gmd`, and leave updating it to Homebrew, whose command `⇓` shows
- Show the change log before an update, with a **Change Log** button in the question, and say before updating that Gmd quits once updated
- Cancel an update, once asked, with **Cancel** or `Esc` while it runs, up to the moment Gmd is replaced

### Improved
- Check a new version against its checksum, and that it runs, before it replaces Gmd
- Never leave Gmd missing when an update fails, and replace it in one step on Linux and macOS
- Try a failed update again without restarting Gmd
- Offer only a release that has a binary for this platform
- Say that Gmd's folder cannot be written to before an update downloads anything
- Ask before **Rebase and Push onto** pushes with force, and refuse while origin has commits the branch has not
- Squash all the chosen commits or none, keeping a merge among the later commits, and their authors and dates
- Push the shared branch choices without wiping out those another clone pushed meanwhile
- Never hide the main branch: `h` says it is always shown, and **Hide Branch** is greyed out for it
- Make **Push** and **Pull** in the menus do what `p`, `u` and `Shift-U` do, and offer **Pull All** with uncommitted changes
- Make `b`, `d`, `m` and `Shift-E` do what their key hints and menu items say
- Let **Amend** give the last commit a new message alone, or with changes too, and name the right branch in the commit dialog
- Offer **Uncommit**, **Squash** and a range **Cherry Pick** only where they work, and say why not
- Count the files of each kind, and the lines added and removed, above a diff's file list
- Offer no `+` and `-` in a line history, and show an uncommitted line's diff from blame read only
- Scroll the resolver's result with `Shift-Up` and `Shift-Down` or the mouse wheel, expand its tabs, and keep its cursor on its conflict when `b` shows the ancestor
- Keep the columns of the log, the diff, blame and the resolver aligned with Chinese, Japanese and emoji text
- Show branch tips, tags and the graph whole on a narrow terminal
- Lay out the graph faster with many branches shown
- Say why `g` leaves main and deleted branches their colors, and when there is nothing to merge
- Say that a new tag was added when only its push failed
- Never offer to make the home folder, or the folder of the recent repos, a repo with **Init**
- Open the commit and amend dialogs from the menu at once
- Give the branch field of **Create Worktree** the same context menu as the other text fields
- Say in the help how to make macOS Terminal send `Shift-Up` and `Shift-Down`

### Fixed
- Gmd could hang at start on macOS, now and then, before anything was drawn
- Updates failed over connections slower than about 8 Mbit/s
- An automatic update was installed again every hour until Gmd was restarted
- Updates failed in a folder with a space in its path
- Branch and tag names with a space were taken by git as two names; they are now refused
- A file was marked resolved before all its conflicts were; one resolved by hand is now marked as such
- Diffs broke with some git settings (`diff.noprefix`, `color.ui`, `log.showSignature` and others)
- Files with non-ASCII names could not be shown, committed, discarded or blamed
- The changes of a file whose mode also changed were not shown
- A change of line endings alone is now shown, as `␍` on the side with the CR, and a line with a lone CR is kept whole
- Text and binary files are now told apart as git does, by their bytes
- Commit, tag and stash messages with `\"` or a trailing `\` failed
- A rebase that stopped again on conflicts was shown as an error, not a conflict
- A stash's message was cut at a colon
- Authors with `|` in their name were misread
- A working tree in a folder named like `project.git` could not be opened
- Blame and origin's tags did not work in SHA-256 repositories
- A repo in the middle of merging a commit no branch has, e.g. after `git pull <url> <branch>` stopped on a conflict, could not be opened
- A fetch bringing origin's older choice undid a branch set by hand, an ambiguity resolved or a branch renamed
- Merge and rebase now use the side of a branch that has the commits, not the one with the newer time
- A refresh could put back the repo from before a newer read, undoing a branch shown or hidden meanwhile
- Gmd stopped on a click in an empty menu, on `m` after a branch was hidden, when a fetch pruned a highlighted branch's remote copy, on **Uncommit** of the first commit, on a cherry pick of rows of two branches, and on a copy after a drag below the log
- A read-only `gmd.log` hung Gmd on a blank screen
- A click on a menu's border ran the item next to it, and `Esc` on a question answered Yes
- The keyboard stayed dead after a menu was opened by mouse during a refresh
- In the diff: `c` is now offered only for the uncommitted changes, a copy of an added file no longer includes line numbers, and **Scroll to** a file and `+` and `-` in a file history work
- The diff's right side was numbered wrongly after conflict markers
- Clicks and the cursor landed in the wrong place in the conflict resolver and in blame, and could scroll sideways past the text
- Clone used another folder than the one browsed to, `-d` was read unlike the other options, and a large file's size was missing when committing
- A mouse drag's selection now starts where the button was pressed
- A view whose content shrank past the rows shown is filled again

## [v1.6.1444.518] - 2026-10-13

### Improved
- Make the notice of an available update clearer

## [v1.5.1444.482] - 2026-10-13

### New
- A start page, in place of the start menu, described in the help

## [v1.4.1442.1069] - 2026-10-11

### Fixed
- An error when hiding a branch

## [v1.1.1439.1092] - 2026-10-08

### Improved
- Show the update available symbol in the bar when a release is found after Gmd started
- Name the current row's commit on the key-hint line rather than in the top bar

## [v1.0.1439.952] - 2026-10-08

### New
- Version 1.0
- The help opens on GitHub rather than in a dialog

### Improved
- A force push is leased on the commit you saw, so it never overwrites commits you have not seen

### Fixed
- Gmd failed to start when its config file was broken

## [v0.97.1439.614] - 2026-10-08

### Improved
- Release notes from the change log, with checksums and `gmd.exe` in every release
- Keep only the latest five previews on the Releases page
- Badges and a quick install at the top of the README

## [v0.96.1439.476] - 2026-10-08

### New
- Choose which files to commit, in a checklist in the commit dialog
- Amend or drop any commit not pushed yet, from its commit menu, recorded for **Undo**
- Ask git's and ssh's questions, e.g. a passphrase, in a login dialog
- **Stash Apply**, which brings a stash back and keeps it
- List every tag, and push or remove one from the list
- Diff any two commits: **Mark for Diff**, then **Diff with it**
- Search the changes themselves with `change:`
- The history of lines, from the blame view
- Give the diff, blame and conflict views key-hint lines of their own

### Improved
- Restructure the help: the screen first, then tasks, then reference, with sections that are easier to get around
- Rework the README for new users
- Say what failed when Gmd crashes, and where to report it
- Say once that branches are hidden, and how to show one
- Give **Show Branch** one name, and its key one spelling
- Name both branches in the merge items
- Copy the commit id and message with `i` and `Shift-I`, in the log and in blame
- Show in **Add Tag** that it pushes the tag, and let the push be left out
- Write the year in full in dates
- Name the sides in the resolver's status line
- Take the duplicate items out of the branch and commit menus
- Move **Clean Working Folder** to **Undo**, and list what it deletes
- Write shifted keys as `⇧` in the menus, as the key-hint line does
- Let the views of a dialog with keys of its own have their hot keys
- Captions in the README's demo animation, saying what each part shows and with which key

### Fixed
- The help's description of the lines between branch columns
- Sorting branches hung on branch orders that contradict each other

## [v0.95.1438.449] - 2026-10-07

### New
- Undo the last change of any branch, checked out or not, including a squash as one step
- Recover lost commits that no branch has any more, from the **Undo** menu
- Tell when origin was rewritten by a force push, pull such a branch without merging the two versions, or restore origin from before the force push
- Restore a branch Gmd deleted, including one deleted with its worktree
- Count the hidden branches with something new, local ones too, with `✦`

### Improved
- Make `Ctrl-A` in the commit dialog add only the subjects of the merged commits
- Show the current branch when it becomes current
- Mark a hidden branch with no commits of its own where it branches out
- Keep commits not pushed yet out of a squash of pushed ones
- Diff a range from a root commit against the empty tree
- Make **Cancel** the default of the **Delete Branch** dialog

## [v0.94.1431.496] - 2026-09-30

### Improved
- Say what a commit, push or pull is doing while git works

## [v0.94.1429.782] - 2026-09-28

### Improved
- Version 0.94, with an improved updater

## [v0.91.1429.412] - 2026-09-28

### Improved
- Easier to use in many places
- Better at deciding which branch a commit belongs to
- Faster reading of the git log

### Fixed
- Scrolling the commit details

## [v0.91.1425.955] - 2026-09-24

### Improved
- A hint about spelling errors in the commit dialog
- Updated help, README and install script

## [v0.91.1410.911] - 2026-09-09

### New
- Support for git worktrees

### Fixed
- The cursor was missing after fixing a spelling error in the commit dialog
- Progress while committing

## [v0.91.1406.461] - 2026-09-05

### New
- Spell checking of the commit, tag and other text fields

### Fixed
- An issue in deciding which branch a commit belongs to

## [v0.91.1404.491] - 2026-09-03

### Improved
- Move some items of the **Branches** menu

### Fixed
- **Pull All** and **Update All** for branches
- Fetch did not really fetch and update

## [v0.91.1388.1436] - 2026-08-18

### New
- Blame
- Conflict handling
- Rename a branch
- **Merge to**
- Adjust the diff's context size
- A **Branches** submenu in the commit menu
- Progress while updating Gmd

### Improved
- Show deleted, no longer active, branches in gray
- Copy text on more platforms
- Move **File History** to the repo menu

### Fixed
- 100% CPU use on Linux and macOS
- The `q` and `p` keys in the log view
- A local-only tag was pruned
- Several other bugs

## [v0.91.1119.279] - 2025-11-22

### New
- Builds for arm64, which the updater installs

## [v0.91.716.1041] - 2024-10-15

### New
- Annotated tags with messages
- Run the configured diff tool and merge tool from the diff view of uncommitted changes

### Fixed
- A click below the last commit

## [v0.91.671.467] - 2024-08-31

### Improved
- Mark leading and trailing whitespace changes in diffs better
- Runs on .NET 8

### Fixed
- Author initials of branch tips
- An issue in **Set Branch Manually**

## [v0.91.541.196] - 2024-04-23

### New
- Show my active branches
- Squash commits

### Improved
- Allow **Pull All** with uncommitted changes

### Fixed
- Gmd crashed when the status changed

## [v0.91.530.207] - 2024-04-12

### Improved
- Start a stash with an empty message
- Highlight selected commits on the same branch

### Fixed
- Copying selected commits within a branch

## [v0.91.453.278] - 2024-01-26

### Fixed
- The Show menu crashed on the main branch

## [v0.91.449.265] - 2024-01-22

### Improved
- Rebase the current branch onto master
- Show the tip author's initials in the branch menu items

## [v0.91.419.545] - 2023-12-23

### New
- Pull with rebase
- Rebase the current branch onto any branch
- Uncommit or squash down to the current row
- Diff a range of commits
- Cherry pick a range of commits
- Named stashes, the dialog starting with the current subject

### Improved
- Push with force after a rebase of a branch that is on origin, using `--force-with-lease`
- Choose a branch's color by its primary name
- Handle a stash whose commit no longer exists

### Fixed
- The UI froze after a rebase of a remote branch

## [v0.91.350.838] - 2023-10-15

### Improved
- More recent folders in the start view
- Clearer warnings when committing large files
- Rename **Switch** to **Switch/Checkout**
- Skip moving up or down to a branch when moving left or right
- Green and red backgrounds in the diff view

## [v0.91.317.1113] - 2023-09-12

### Improved
- Show whitespace changes in diffs

## [v0.91.317.211] - 2023-09-12

### Improved
- **Hide All Branches** in the commit menu, in place of its **Branches** menu

## [v0.91.314.262] - 2023-09-09

### Fixed
- **Branch Out More**

## [v0.91.311.1117] - 2023-09-06

### Improved
- Branch menus in the commit menu
- A clearer application bar, current and uncommitted symbols, and version text
- Show the empty repo's commit in dark gray
- Show `…` on the left side of a diff scrolled to the right
- More recent repos

### Fixed
- The **Open Repo** menu and the `O` key

## [v0.90.310.1122] - 2023-09-05

### Improved
- Clearer menus

### Fixed
- Amend

## [v0.90.307.299] - 2023-09-02

### Improved
- One config file in place of separate state and config files
- More space between sibling branches in the graph
- The stash submenu moved from the repo menu to the commit menu
- A clearer application bar
- Create a branch in a local-only repo, with no error

### Fixed
- `h` sometimes hid the wrong branch
- Changing a branch's color
- The uncommitted diff of an empty repo
- **Diff Branches**

## [v0.90.293.440] - 2023-08-19

### New
- Init a repo, from the main menu
- Stash status in the application bar and the commits

### Improved
- Filter and search
- Menu widths

### Fixed
- Hiding a pull merge branch

## [v0.90.282.1137] - 2023-08-08

### Improved
- Fewer overlapping branches in the graph, and a better branch structure
- Unset a branch in **Set Branch Manually**
- Show the diff of an added or removed file in one column
- Clearer update information

### Fixed
- The graph's width
- Refresh after a diff

## [v0.90.277.1021] - 2023-08-03

### Improved
- `Y` shows the current branch menu
- Handle binary files better when committing
- Clearer progress, commit dialog, application bar and filter bar, with close buttons on the bars
- A right click sets the current row
- Shown branches are marked with `o`
- Better branch structure

### Fixed
- Refresh after removing a tag
- Pushing a local-only branch
- Focus on buttons and checkboxes
- Progress when clicking on the application bar
- Creating a branch

## [v0.90.272.349] - 2023-07-29

### Improved
- A better **Set Branch Manually** dialog
- Filter results limited to 5,000
- `Shift-→` opens the branch menu
- Menus placed by the application bar
- A key for the diff of branches

## [v0.90.269.290] - 2023-07-26

### Improved
- Combo boxes take mouse clicks
- Better branch name parsing
- A better Unicode character set dialog

## [v0.90.268.282] - 2023-07-25

### New
- Merge from a branch with the middle mouse button

### Fixed
- Clicking on a branch

## [v0.90.267.1006] - 2023-07-24

### New
- Double click a branch to switch to it

### Improved
- The application bar, and the help

## [v0.90.267.349] - 2023-07-24

### New
- Update from the main menu
- An application bar with repo and commit info
- Branch menus and commit menus, with the branch and commit under the mouse highlighted
- Move left and right between branches, and up and down between commits
- Open and close branches by clicking in the graph, on a branch's commits, its tip or its bottom commit
- `y` goes to the current branch
- Copy, and select with the mouse

### Improved
- Version 0.90
- `Space` scrolls a page
- Menu items highlighted on hover
- Show commit ids in the log view
- Cleaner hide and switch menu items
- Better file monitoring

### Fixed
- The main view closed when a menu was closed
- Mouse support in menus, and on Linux

## [v0.80.257.540] - 2023-07-14

### Improved
- Select the commit after a search

## [v0.80.257.500] - 2023-07-14

### New
- Show all recent, all active or all branches
- Set a commit's branch in a dialog
- Scroll to a commit in the file history

### Improved
- Better search and filter: the graph and a branch count in the results, mouse scrolling, and `*` to find ambiguous tips
- Show ambiguous branches in the branch structure menu
- Show a branch when switching to it
- Highlight the whole row in the log
- Clearer merge commit messages
- Menus, and the current path disabled in Open Repo's recent paths
- Shown branches listed first when deleting a branch

### Fixed
- Showing a complex branch with many pull merges

## [v0.80.243.218] - 2023-06-30

### Improved
- Offer to undo binary files when committing

## [v0.80.242.208] - 2023-06-29

### New
- Undo all uncommitted binary files

### Improved
- Warn about binary files when committing modified files too
- Fewer duplicates when showing branches
- **Publish** rather than **Push** in the Create Branch dialog

## [v0.80.241.218] - 2023-06-28

### Improved
- **Switch to Commit** moved into the **Switch to** submenu
- No metadata fetch when sync is turned off
- The undo menu in the diff

### Fixed
- Parsing the diff of a merge commit

## [v0.80.239.954] - 2023-06-26

### New
- The **Open Repo** menu
- Commit from the diff
- Undo and restore files, and all binary files, from the diff, whose menu also has **Scroll to**
- Refresh the diff of uncommitted changes

### Improved
- Long diff lines are marked with `…`
- Pause file monitoring during a commit

### Fixed
- The updater
- File paths with spaces in diff, status, undo and clone

## [v0.80.232.224] - 2023-06-19

### New
- Include a summary of the commits in a merge message
- Copy commits and diff rows

### Fixed
- Showing branches
- The **Allow preview** option was not always shown in Config

## [v0.80.223.287] - 2023-06-10

### Improved
- Version 0.80
- `p` and `u` push and pull the current branch, `Shift-P` and `Shift-U` all branches
- Clearer push and pull messages, menus and info boxes
- Fetch at better times
- The window title is the repo's name

## [v0.50.215.219] - 2023-06-02

### New
- Switch to a commit, with a detached head

### Fixed
- Message boxes without a title

## [v0.50.213.234] - 2023-05-31

### New
- Show the changes within lines in the diff

### Improved
- Progress moved to the upper left corner

## [v0.50.207.216] - 2023-05-25

### Improved
- Skip leading empty lines in commit messages

## [v0.50.205.229] - 2023-05-23

### New
- Move branches left or right to change their order
- Publish a local-only branch

### Improved
- A better diff
- Branches sorted in menus

### Fixed
- Config when there was no previous file

## [v0.50.202.436] - 2023-05-20

### New
- Diff within a line

### Improved
- Highlight the whole current row in the log

## [v0.50.194.217] - 2023-05-12

### New
- Ignore whitespace in diffs
- Set a commit's branch by hand

## [v0.50.190.252] - 2023-05-08

### New
- Add and remove tags

## [v0.50.185.224] - 2023-05-03

### Improved
- Hierarchical branch names in **Show Branch**

## [v0.50.184.216] - 2023-05-02

### Improved
- Highlight the row found by a search

## [v0.50.183.675] - 2023-05-01

### New
- `a` amends

## [v0.50.183.563] - 2023-05-01

### New
- Amend

### Improved
- **Cherry Pick** moved into the merge menu

## [v0.50.175.562] - 2023-04-23

### New
- Diff of branches, to and from
- Delete a branch in a dialog, with a force option
- A change log

## [v0.50.173.249] - 2023-04-21

### New
- Stash

### Improved
- The diff items moved to a submenu

## [v0.50.167.459] - 2023-04-15

### New
- Change a branch's color

### Improved
- Clearer tags and branch tips in the log, and resolved ambiguous branches
- Better branch names from Azure Repos pull requests

## [v0.50.146.567] - 2023-03-25

### Improved
- Binary files marked better in the diff
- Tags moved to the right side of the log

## [v0.50.146.466] - 2023-03-25

### New
- Preview the diff of a merge

### Improved
- A better clone dialog

## [v0.50.120.477] - 2023-02-27

### Improved
- A better diff view

## [v0.50.114.456] - 2023-02-21

### Improved
- Clearer automatic update messages and behavior

## [v0.50.110.496] - 2023-02-17

### New
- The Config dialog

## [v0.50.85.545] - 2023-01-23

### Fixed
- Repos with no tags

## [v0.50.82.394] - 2023-01-20

### New
- Settings for the UI, metadata sync and preview updates
- Check for updates every hour

### Fixed
- Some events needed a mouse click to proceed

## [v0.50.75.387] - 2023-01-13

### Improved
- A clearer error message on conflicts

## [v0.50.72.805] - 2023-01-10

### Improved
- The color of conflict markers

## [v0.50.72.793] - 2023-01-10

### Fixed
- The diff of a commit

## [v0.50.72.553] - 2023-01-10

### New
- Cherry pick

### Improved
- Faster update downloads

## [v0.50.66.1095] - 2023-01-04

### Improved
- A better branch graph
- The current line is highlighted

### Fixed
- Gmd could fetch over and over again

## [v0.50.50.494] - 2022-12-19

### New
- Push and checkout options when creating a branch

## [v0.50.50.382] - 2022-12-19

### Fixed
- Updating on macOS

## [v0.50.49.645] - 2022-12-18

### New
- Command line help

## [v0.50.49.509] - 2022-12-18

### New
- Empty repos, e.g. just after init

## [v0.50.49.389] - 2022-12-18

### Fixed
- Repos without any tags

## [v0.50.48.467] - 2022-12-17

### Changed
- Preview releases are off by default; turn on **Allow preview releases** to get them

## [v0.50.48.458] - 2022-12-17

### New
- Preview releases besides the releases

## [v0.50.47.827] - 2022-12-16

### Fixed
- The commit details now show the last row of the message

## [v0.50.47.669] - 2022-12-16

### Improved
- Better mouse support

## [v0.50.47.300] - 2022-12-16

### Improved
- Version 0.50

## [v0.40.46.859] - 2022-12-15

### Improved
- The author in the commit details

## [v0.40.46.494] - 2022-12-15

### New
- A help dialog

## [v0.40.45.645] - 2022-12-14

### Improved
- Faster update installs in most cases

## [v0.40.45.500] - 2022-12-14

### New
- A Windows installer, which downloads the latest Windows binary

### Fixed
- Parsing some branch structures

## [v0.40.44.320] - 2022-12-13

### New
- Merge from a commit

## [v0.40.43.1123] - 2022-12-12

### Improved
- Version 0.40, with clearer menus

## [v0.30.42.938] - 2022-12-11

### New
- Switch to, check out, a deleted branch

## [v0.30.42.901] - 2022-12-11

### Improved
- Show the merge on the uncommitted row while merging
- Select the latest commit when merging a branch that is both local and remote

## [v0.30.42.813] - 2022-12-11

### Fixed
- Merging remote-only branches

## [v0.30.42.555] - 2022-12-11

### Improved
- Push metadata only when pushing a branch

### Fixed
- Working out the branch structure took a long time

## [v0.30.41.979] - 2022-12-10

### Improved
- Clearer update messages

## [v0.30.41.907] - 2022-12-10

### Improved
- A better branch structure

## [v0.30.37.353] - 2022-12-06

### Improved
- Deleted folders are removed from the recent folders

## [v0.30.36.925] - 2022-12-05

### New
- Clone

## [v0.30.36.591] - 2022-12-05

### Fixed
- Changes in `.git*` files triggered refreshes

## [v0.30.32.894] - 2022-12-01

### Improved
- Runs on .NET 7

## [v0.30.32.367] - 2022-12-01

### Improved
- A better branch structure

## [v0.30.30.522] - 2022-11-29

### New
- Push and pull all branches

## [v0.30.29.632] - 2022-11-28

### New
- An About dialog in the main menu

## [v0.30.29.573] - 2022-11-28

### New
- A Windows setup file

## [v0.30.29.496] - 2022-11-28

### New
- The `--update` command line option

## [v0.30.26.309] - 2022-11-25

### New
- File history

## [v0.30.25.487] - 2022-11-24

### New
- Resolve ambiguous branches by hand

## [v0.30.24.729] - 2022-11-23

### New
- Undo

## [v0.30.24.357] - 2022-11-23

### Improved
- Many improvements

## [v0.22.1023] - 2022-11-21

### Fixed
- The show branches menu
