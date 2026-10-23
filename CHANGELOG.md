# Change Log for Gmd

What changed for users in each release, newest first. A release includes the previews before it.
Releases with no change users notice are left out.

## [Unreleased]

### Improved
- **Delete Merged Branches** now also lists the branches origin still has that are merged into its main or an integration branch, with their local branches. A column says where each one is (`here`, `origin` or `both`), and each row shows its name in its color in the log and who made its last commit and when
- Deleting a branch on origin from **Delete Merged Branches** now asks first, with Cancel the default, since it deletes it for everyone; **Delete All** asks whether to delete those on origin too (**Also on Origin**) or leave them (**Here Only**)

### Fixed
- A command such as continuing a rebase no longer fails with "Unable to create index.lock" when Gmd reads the status in the background at that moment

## [v1.15.1453.671] - 2026-10-22

### Improved
- `+` and `-` now step the context, and `h` hides a file's lines, on the first file where a diff opens, without moving to a file first
- The diff menu is now grouped, and the diff of a commit no longer offers **Commit**, **Discard Changes** or the conflict items
- The diff of a merge now says that it is against its first parent
- The last commit's amend is now named **Amend Last Commit of main ...** in the menu of another commit
- `Ctrl-L` now adds a merge's subjects to its message, as the commit dialog says, and only once; `Ctrl-A` selects all there, as in other fields
- The folder browser now cuts a long folder path at its start, so its last folder shows
- The note of a branch merged on origin now starts with **Delete Merged Branches**, which a narrow terminal cut off before
- The uncommitted row with conflicts now hints `d resolve…`, since `d` asks first
- The name field of **Create Branch** and **Rename Branch** now has a label

### Changed
- The `--changelog` command line option is gone; the change log is here, and Gmd's update question links to it

### Fixed
- Typing a branch name fast after `Shift-→` no longer opens a Find Branch dialog per letter
- The lines of the resolver's result below an undecided conflict are now numbered as its first side is

## [v1.14.1452.819] - 2026-10-21

### New
- **Publish** in a branch's menu publishes a branch that is not on origin yet, e.g. one created with its Publish box unticked
- **Publish** in a branch's menu also puts a branch deleted on origin back, after asking

### Improved
- `+` and `-` now step the diff's context 15 lines at a time, starting from 6 lines, around the changes of the file at the cursor, and the diff says when the whole file is shown. The diff menu's **Show More Context** and **Show Less Context** do the same, **Show All** shows the whole file, and `-` goes back to the context before it
- `+` on the `⋯` between two parts of a file now shows more context; `Enter` no longer does
- The row counting the files of a diff, e.g. `4 files changed  4 modified  +8 -8`, is now right under the author and date, so every diff starts one row higher

### Changed
- `p` no longer publishes a branch; it says to use **Publish** instead

### Fixed
- **Push** in a branch menu is now greyed out when there is nothing to push, rather than pushing nothing

## [v1.13.1450.1146] - 2026-10-19

### New
- Gmd now draws in its own colors: twelve branch colors, with a dark or light palette picked from the terminal's background, or the terminal's own 16 colors, chosen with **Colors** in the Config dialog
- The diff, the blame and the conflict resolver now color code by language, as VS Code's Dark+ and Light+ themes do, for about fifty languages. It can be turned off in Config
- Commits that are already pushed can now be amended, dropped and squashed, and the branch pushed with force along with it. Gmd asks first on main, develop or a release branch, and for someone else's commits
- **Move to New Branch** in the commit menu moves commits made on the wrong branch, e.g. main, to a new branch, and **Reorder** moves a commit above or below its neighbor
- **Undo** in the commit menu resets the current branch to any commit, keeping the changes uncommitted or discarding them
- Branches squash or rebase merged on origin are marked with `✓`, and **Delete Merged Branches** in the repo menu deletes local branches merged into main, including by a squash on origin
- All the conflicted files of a merge, rebase or other operation are resolved in one view, where the next file opens when one is done. Unsaved decisions are kept for each file until the view closes, `↑` and `↓` in the file list show each file at once, and **Unresolve This File** (`a`) puts a resolved file's conflicts back
- `b` in a diff blames the file at the cursor's line and commit, and `Shift-L` in the blame shows a file's history at the line's commit; `Esc` goes back
- **Blame File** and **File History** now pick the file in a new file browser: the files the commit changed first, then all its files as a tree, where typing any part of a path finds a file
- The folder to open, clone into, init or put a worktree in is now picked in a similar folder browser, as large as the terminal, which lists the folders of a typed path as a shell completes it
- The commit chosen for a diff is now marked with `±` in the log, its row tinted, and **Unmark for Diff** removes the mark; a diff of selected commits says how many in its title

### Improved
- A branch now keeps the color it is first drawn in, chosen to differ from its parent and its neighbors, so showing, hiding, pushing or merging branches never changes it
- Emoji in commit subjects are now drawn as emoji
- `p` after a squash, amend or rebase of pushed commits, made in Gmd or by another tool, now offers to push with force, rather than pull back the old version origin still has
- **Rebase and Push onto** is now **Rebase onto**, which asks whether to push with force too
- A squash's message now starts with the oldest commit's message and lists the others below it, as GitHub writes a squash merge
- Cherry picking the commits selected with `Shift-↑` and `Shift-↓` now keeps them as they were made, with their authors, and one **Undo** takes them all back
- The commit menu's **Undo** now offers **Abort Merge**, **Abort Rebase** and so on first while one is in progress
- Gmd now tells a branch deleted on origin apart from one never pushed, so `p` no longer publishes it again unasked
- `d` on the uncommitted row with conflicts now asks whether to resolve them or show the diff; `Ctrl-D` shows the diff at once
- `Enter` on a diff's `⋯` now shows more of the lines left out, as `+` does, and the cursor's line stays in place on the screen as `+` and `-` change the context
- A one-column diff now marks removed and added lines with `-` and `+`, and a file's header with a bright cyan line
- The diff, the blame and the resolver now scroll sideways with a mouse's sideways wheel, or with `Shift` and the wheel, which no longer scrolls the log and lists up and down
- The commit and branch menus are now grouped by what their items do, and the **Undo** menu by undoing the last change, undoing commits, discarding and bringing back. **Set Commit Branch Manually** is in the commit menu, **Show Branch** in the repo menu, and **Full File History** is now **File History**
- **Discard All Changes** is now one item, whose question lists the ignored files and offers to delete them too
- The commit message now takes `Ctrl-A`, `Ctrl-C`, `Ctrl-X`, `Ctrl-V`, `Ctrl-Z` and `Ctrl-Y`, as the subject field does
- The branches set by hand are now kept in `.git/gmd-metadata.json` rather than in a ref in the repository, moved there at the first change
- A shared branch structure now pushes only what concerns commits origin already has, so the names of unpushed branches and unpushed work stay on your machine; Config says what sharing pushes before it is turned on
- The README now lists everything Gmd stores and sends

## [v1.12.1448.496] - 2026-10-17

### New
- `]` and `[` go to the next or previous change, and `}` and `{` to the next or previous file; the top row names the commit and the file in view
- The diff is shown in one column on a terminal narrower than 110 columns, and `v` switches between one and two columns
- A terminal at least 150 columns wide lists the files beside a diff, with the lines added, deleted and left out of the commit, rather than at the top of the diff. `Tab` moves into the list and `l` hides it
- `h` hides the lines of a file in the diff, e.g. a generated file
- The files of a merge, rebase, cherry pick or revert are resolved one after the other, listed beside the resolver, from the conflict box, the top bar or the repo menu, and Gmd offers to commit or continue once none is left
- `c` combines both sides of a conflict where they changed different words, or lines that only touch
- `e` lets you type a conflict's resolution by hand in the result pane, with both sides still in view

### Improved
- The diff is drawn more calmly: a bar beside each changed line, whole differing words marked rather than scattered letters, a row for each file, and a `⋯` line where unchanged lines are left out, naming the function git finds the next part in
- Each side of a conflict is now shown as what it changed from the common ancestor, with its changed words marked, named by its branch and its commit's subject
- The whole file is now shown below the sides as it will be saved, following the cursor, with each conflict's lines marked by the side they came from

## [v1.11.1447.386] - 2026-10-16

### New
- Part of a file can be committed: `x` in the diff of the uncommitted changes leaves the change at the cursor, or the selected lines, out of the commit, which takes the rest and leaves them uncommitted
- Part of a file can be discarded: `u` in the diff offers the change at the cursor, or the selected lines, above the whole files
- **Restore Discarded Changes** in the **Undo** menu brings back what was discarded, a change, a file, all changes or a dropped stash, for about 30 days
- `f` or `/` searches the lines of a diff or a blame, with the matches marked, and `n` and `Shift-N` step through them
- `w` hides whitespace-only changes in a diff; a file with nothing else is listed as whitespace only

### Improved
- Some lines of a renamed file, which stays renamed, or of a deleted file can now be committed and discarded
- The commit a squash makes is now signed when git's config signs commits, as every other commit Gmd makes is
- A greyed out item of the diff's or the blame's menu now says why on the diff's or the blame's own status line, rather than on the log's behind it

## [v1.10.1446.1006] - 2026-10-15

### Improved
- A commit's message now has most of the details pane, with its fields in four rows, and a parent or child on another branch is named by its branch, so a merge says what it merged
- A commit's details now list the files it changed, with the lines added and deleted in each, and the uncommitted details list the uncommitted files, conflicted files first
- `Ctrl-C` in the log now asks what to copy: a commit's id, message, subject or link, or the selected rows' ids, messages or range
- Commit ids now have eight characters rather than six, as `git log --oneline` does in all but small repositories
- A large repository now shows its latest 50,000 commits rather than 30,000, in no more memory than before
- Gmd now reads a large repository's log much faster when it has no commit-graph file, by writing one, as `git gc` would
- The branches of long ambiguous stretches of history are now decided in a fraction of the time, e.g. 2 s rather than 17 s for 200,000 commits of rust
- Main's line now goes on through a merged tag or another project's branch pulled in, e.g. git's translations and gitk
- A deleted branch named in merge subjects is now recovered where other branches were started from it, leaving far fewer commits ambiguous
- A branch is now named after a tag or a loose word of a merge subject only when nothing else names it
- A branch and a fork's copy of it, e.g. `timfel/1.2.x`, are now one branch where they meet
- A branch started on the merge of the previous branch now shares that branch's column

### Fixed
- A branch set by hand is now kept by its commit's full id, so it never applies to another commit with the same first characters. One set with an earlier version needs to be set again

## [v1.9.1446.426] - 2026-10-15

### Improved
- **Auto update** and **Allow preview releases** in Config are now greyed out for a Gmd installed with Homebrew, which says why
- The PowerShell install on Windows now says that Gmd already runs in that terminal

### Fixed
- A Gmd installed with Homebrew, which has the stable releases only, is no longer offered a preview

## [v1.8.1445.1141] - 2026-10-14

### New
- Gmd installs on Windows with one PowerShell command, into your own folder and PATH, with no admin rights needed
- Gmd installs in a dev container with the feature `ghcr.io/michael-reichenauer/gmd/gmd:1`

### Improved
- A Gmd installed with winget now leaves updating to winget, showing its command at `⇓`, to run once Gmd is closed

## [v1.7.1445.1049] - 2026-10-14

### New
- Gmd installs with Homebrew on macOS and Linux, `brew install michael-reichenauer/tap/gmd`, and leaves updating to Homebrew, whose command `⇓` shows
- The question before an update now has a **Change Log** button, and says that Gmd quits once updated
- An update, once asked for, can be cancelled with **Cancel** or `Esc` while it runs, up to the moment Gmd is replaced

### Improved
- A new version is now checked against its checksum, and that it runs, before it replaces Gmd
- A failed update no longer leaves Gmd missing, and Linux and macOS replace it in one step
- A failed update can now be tried again without restarting Gmd
- Only a release that has a binary for this platform is offered
- An update now says that Gmd's folder cannot be written to before it downloads anything
- **Rebase and Push onto** now asks before it pushes with force, and refuses while origin has commits the branch has not
- A squash now takes all the chosen commits or none, keeping a merge among the later commits, and their authors and dates
- Pushing the shared branch choices no longer wipes out those another clone pushed meanwhile
- The main branch is never hidden: `h` says it is always shown, and **Hide Branch** is greyed out for it
- **Push** and **Pull** in the menus now do what `p`, `u` and `Shift-U` do, and **Pull All** is offered with uncommitted changes
- `b`, `d`, `m` and `Shift-E` now do what their key hints and menu items say
- **Amend** can now give the last commit a new message alone, or with changes too, and the commit dialog names the right branch
- **Uncommit**, **Squash** and a range **Cherry Pick** are offered only where they work, and say why not
- A diff now counts the files of each kind, and the lines added and removed, above its file list
- A line history no longer offers `+` and `-`, and blame shows an uncommitted line's diff read only
- The resolver's result scrolls with `Shift-Up` and `Shift-Down` or the mouse wheel, expands its tabs, and keeps its cursor on its conflict when `b` shows the ancestor
- The columns of the log, the diff, blame and the resolver now stay aligned with Chinese, Japanese and emoji text
- Branch tips, tags and the graph are now shown whole on a narrow terminal
- The graph is laid out faster with many branches shown
- `g` now says why it leaves main and deleted branches their colors, and when there is nothing to merge
- Gmd now says a new tag was added when only its push failed
- **Init** no longer offers to make the home folder, or the folder of the recent repos, a repo
- The commit and amend dialogs now open from the menu at once
- The branch field of **Create Worktree** now has the same context menu as the other text fields
- The help now says how to make macOS Terminal send `Shift-Up` and `Shift-Down`

### Fixed
- Gmd no longer hangs now and then at start on macOS, before anything is drawn
- Updates no longer fail over connections slower than about 8 Mbit/s
- An automatic update is no longer installed again every hour until Gmd is restarted
- Updates no longer fail in a folder with a space in its path
- Branch and tag names with a space are now refused, rather than taken by git as two names
- A file is no longer marked resolved before all its conflicts are, and one resolved by hand is marked as such
- Diffs no longer break with some git settings (`diff.noprefix`, `color.ui`, `log.showSignature` and others)
- Files with non-ASCII names can now be shown, committed, discarded and blamed
- The changes of a file whose mode also changed are now shown
- A change of line endings alone is now shown, as `␍` on the side with the CR, and a line with a lone CR is kept whole
- Text and binary files are now told apart as git does, by their bytes
- Commit, tag and stash messages with `\"` or a trailing `\` no longer fail
- A rebase that stops again on conflicts is now shown as a conflict, not an error
- A stash's message is no longer cut at a colon
- Authors with `|` in their name are no longer misread
- A working tree in a folder named like `project.git` can now be opened
- Blame and origin's tags now work in SHA-256 repositories
- A repo in the middle of merging a commit no branch has, e.g. after `git pull <url> <branch>` stopped on a conflict, can now be opened
- A fetch bringing origin's older choice no longer undoes a branch set by hand, an ambiguity resolved or a branch renamed
- Merge and rebase now use the side of a branch that has the commits, not the one with the newer time
- A refresh no longer puts back the repo from before a newer read, undoing a branch shown or hidden meanwhile
- Gmd no longer stops on a click in an empty menu, on `m` after a branch was hidden, when a fetch prunes a highlighted branch's remote copy, on **Uncommit** of the first commit, on a cherry pick of rows of two branches, or on a copy after a drag below the log
- A read-only `gmd.log` no longer hangs Gmd on a blank screen
- A click on a menu's border no longer runs the item next to it, and `Esc` on a question no longer answers Yes
- The keyboard no longer stays dead after a menu is opened by mouse during a refresh
- In the diff: `c` is now offered only for the uncommitted changes, a copy of an added file no longer includes line numbers, and **Scroll to** a file and `+` and `-` in a file history now work
- The diff's right side is now numbered correctly after conflict markers
- Clicks and the cursor now land in the right place in the conflict resolver and in blame, and no longer scroll sideways past the text
- Clone now uses the folder browsed to, `-d` is read as the other options are, and a large file's size is shown when committing
- A mouse drag's selection now starts where the button was pressed
- A view whose content shrank past the rows shown is now filled again

## [v1.6.1444.518] - 2026-10-13

### Improved
- The notice of an available update is now clearer

## [v1.5.1444.482] - 2026-10-13

### New
- A start page, in place of the start menu, described in the help

## [v1.4.1442.1069] - 2026-10-11

### Fixed
- Hiding a branch no longer fails with an error

## [v1.1.1439.1092] - 2026-10-08

### Improved
- The bar now shows the update available symbol when a release is found after Gmd started
- The key-hint line, rather than the top bar, now names the current row's commit

## [v1.0.1439.952] - 2026-10-08

### New
- Version 1.0
- The help opens on GitHub rather than in a dialog

### Improved
- A force push is now leased on the commit you saw, so it never overwrites commits you have not seen

### Fixed
- Gmd now starts when its config file is broken

## [v0.97.1439.614] - 2026-10-08

### Improved
- Every release now has notes from the change log, checksums and `gmd.exe`
- The Releases page now keeps only the latest five previews
- The README now has badges and a quick install at the top

## [v0.96.1439.476] - 2026-10-08

### New
- The commit dialog has a checklist of the files to commit
- Any commit not pushed yet can be amended or dropped from its commit menu, recorded for **Undo**
- A login dialog asks git's and ssh's questions, e.g. a passphrase
- **Stash Apply** brings a stash back and keeps it
- A list of every tag, where one can be pushed or removed
- **Mark for Diff**, then **Diff with it**, diffs any two commits
- `change:` searches the changes themselves
- The history of lines, from the blame view
- The diff, blame and conflict views have key-hint lines of their own

### Improved
- The help is restructured: the screen first, then tasks, then reference, with sections that are easier to get around
- The README is reworked for new users
- Gmd now says what failed when it crashes, and where to report it
- Gmd now says once that branches are hidden, and how to show one
- **Show Branch** now has one name, and its key one spelling
- The merge items now name both branches
- `i` and `Shift-I` now copy the commit id and message, in the log and in blame
- **Add Tag** now shows that it pushes the tag, and the push can be left out
- Dates now have the year in full
- The resolver's status line now names the sides
- The branch and commit menus no longer have duplicate items
- **Clean Working Folder** has moved to **Undo**, and lists what it deletes
- The menus now write shifted keys as `⇧`, as the key-hint line does
- The views of a dialog with keys of its own now have their hot keys
- The README's demo animation now has captions, saying what each part shows and with which key

### Fixed
- The help now describes the lines between branch columns correctly
- Sorting branches no longer hangs on branch orders that contradict each other

## [v0.95.1438.449] - 2026-10-07

### New
- Undo takes back the last change of any branch, checked out or not, including a squash as one step
- **Recover Lost Commits** in the **Undo** menu brings back commits that no branch has any more
- Gmd tells when origin was rewritten by a force push, and can pull such a branch without merging the two versions, or restore origin from before the force push
- A branch Gmd deleted can be restored, including one deleted with its worktree
- `✦` counts the hidden branches with something new, local ones too

### Improved
- `Ctrl-A` in the commit dialog now adds only the subjects of the merged commits
- The current branch is now shown when it becomes current
- A hidden branch with no commits of its own is now marked where it branches out
- A squash of pushed commits now leaves out commits not pushed yet
- A range from a root commit is now diffed against the empty tree
- **Cancel** is now the default of the **Delete Branch** dialog

## [v0.94.1431.496] - 2026-09-30

### Improved
- A commit, push or pull now says what it is doing while git works

## [v0.94.1429.782] - 2026-09-28

### Improved
- Version 0.94, with an improved updater

## [v0.91.1429.412] - 2026-09-28

### Improved
- Many places are now easier to use
- Deciding which branch a commit belongs to is better
- The git log is read faster

### Fixed
- The commit details now scroll

## [v0.91.1425.955] - 2026-09-24

### Improved
- The commit dialog now hints about spelling errors
- The help, the README and the install script are updated

## [v0.91.1410.911] - 2026-09-09

### New
- Support for git worktrees

### Fixed
- The cursor is no longer missing after fixing a spelling error in the commit dialog
- Committing now shows its progress

## [v0.91.1406.461] - 2026-09-05

### New
- Spell checking of the commit, tag and other text fields

### Fixed
- An issue in deciding which branch a commit belongs to is fixed

## [v0.91.1404.491] - 2026-09-03

### Improved
- Some items of the **Branches** menu have moved

### Fixed
- **Pull All** and **Update All** for branches now work
- Fetch now really fetches and updates

## [v0.91.1388.1436] - 2026-08-18

### New
- Blame
- Conflict handling
- Renaming a branch
- **Merge to**
- The diff's context size can be adjusted
- A **Branches** submenu in the commit menu
- Updating Gmd shows its progress

### Improved
- Deleted, no longer active, branches are now shown in gray
- Copying text now works on more platforms
- **File History** has moved to the repo menu

### Fixed
- Gmd no longer uses 100% CPU on Linux and macOS
- The `q` and `p` keys in the log view now work
- A local-only tag is no longer pruned
- Several other bugs

## [v0.91.1119.279] - 2025-11-22

### New
- Builds for arm64, which the updater installs

## [v0.91.716.1041] - 2024-10-15

### New
- Annotated tags with messages
- The diff view of uncommitted changes can run the configured diff tool and merge tool

### Fixed
- A click below the last commit now works

## [v0.91.671.467] - 2024-08-31

### Improved
- Diffs now mark leading and trailing whitespace changes better
- Gmd now runs on .NET 8

### Fixed
- The author initials of branch tips are now right
- An issue in **Set Branch Manually** is fixed

## [v0.91.541.196] - 2024-04-23

### New
- Showing my active branches
- Squashing commits

### Improved
- **Pull All** now works with uncommitted changes

### Fixed
- Gmd no longer crashes when the status changes

## [v0.91.530.207] - 2024-04-12

### Improved
- A stash now starts with an empty message
- Selected commits on the same branch are now highlighted

### Fixed
- Copying selected commits within a branch now works

## [v0.91.453.278] - 2024-01-26

### Fixed
- The Show menu no longer crashes on the main branch

## [v0.91.449.265] - 2024-01-22

### Improved
- The current branch can now be rebased onto master
- The branch menu items now show the tip author's initials

## [v0.91.419.545] - 2023-12-23

### New
- Pulling with rebase
- Rebasing the current branch onto any branch
- Uncommitting or squashing down to the current row
- A diff of a range of commits
- A cherry pick of a range of commits
- Named stashes, the dialog starting with the current subject

### Improved
- Pushing a branch that is on origin after a rebase now pushes with force, using `--force-with-lease`
- A branch's color is now chosen by its primary name
- A stash whose commit no longer exists is now handled

### Fixed
- The UI no longer freezes after a rebase of a remote branch

## [v0.91.350.838] - 2023-10-15

### Improved
- The start view now has more recent folders
- Committing large files now warns more clearly
- **Switch** is now **Switch/Checkout**
- Moving left or right no longer moves up or down to a branch
- The diff view now has green and red backgrounds

## [v0.91.317.1113] - 2023-09-12

### Improved
- Diffs now show whitespace changes

## [v0.91.317.211] - 2023-09-12

### Improved
- The commit menu now has **Hide All Branches** in place of its **Branches** menu

## [v0.91.314.262] - 2023-09-09

### Fixed
- **Branch Out More** now works

## [v0.91.311.1117] - 2023-09-06

### Improved
- The commit menu now has branch menus
- The application bar, the current and uncommitted symbols, and the version text are clearer
- The empty repo's commit is now shown in dark gray
- A diff scrolled to the right now shows `…` on its left side
- More recent repos are listed

### Fixed
- The **Open Repo** menu and the `O` key now work

## [v0.90.310.1122] - 2023-09-05

### Improved
- The menus are clearer

### Fixed
- Amend now works

## [v0.90.307.299] - 2023-09-02

### Improved
- One config file now replaces separate state and config files
- Sibling branches now have more space between them in the graph
- The stash submenu has moved from the repo menu to the commit menu
- The application bar is clearer
- A branch can now be created in a local-only repo, with no error

### Fixed
- `h` no longer sometimes hides the wrong branch
- Changing a branch's color now works
- The uncommitted diff of an empty repo now works
- **Diff Branches** now works

## [v0.90.293.440] - 2023-08-19

### New
- **Init** in the main menu makes a repo
- The application bar and the commits show the stash status

### Improved
- Filter and search are better
- Menus have better widths

### Fixed
- Hiding a pull merge branch now works

## [v0.90.282.1137] - 2023-08-08

### Improved
- The graph has fewer overlapping branches, and a better branch structure
- **Set Branch Manually** can now unset a branch
- The diff of an added or removed file is now shown in one column
- The update information is clearer

### Fixed
- The graph's width is now right
- A diff is now followed by a refresh

## [v0.90.277.1021] - 2023-08-03

### Improved
- `Y` now shows the current branch menu
- Committing now handles binary files better
- The progress, the commit dialog, the application bar and the filter bar are clearer, with close buttons on the bars
- A right click now sets the current row
- Shown branches are now marked with `o`
- The branch structure is better

### Fixed
- Removing a tag is now followed by a refresh
- Pushing a local-only branch now works
- Buttons and checkboxes now take the focus
- Clicking on the application bar now shows progress
- Creating a branch now works

## [v0.90.272.349] - 2023-07-29

### Improved
- The **Set Branch Manually** dialog is better
- Filter results are now limited to 5,000
- `Shift-→` now opens the branch menu
- Menus are now placed by the application bar
- The diff of branches now has a key

## [v0.90.269.290] - 2023-07-26

### Improved
- Combo boxes now take mouse clicks
- Branch names are parsed better
- The Unicode character set dialog is better

## [v0.90.268.282] - 2023-07-25

### New
- The middle mouse button merges from a branch

### Fixed
- Clicking on a branch now works

## [v0.90.267.1006] - 2023-07-24

### New
- A double click on a branch switches to it

### Improved
- The application bar and the help are better

## [v0.90.267.349] - 2023-07-24

### New
- Updating from the main menu
- An application bar with repo and commit info
- Branch menus and commit menus, with the branch and commit under the mouse highlighted
- Moving left and right between branches, and up and down between commits
- Clicks in the graph open and close branches, on a branch's commits, its tip or its bottom commit
- `y` goes to the current branch
- Copying, and selecting with the mouse

### Improved
- Version 0.90
- `Space` now scrolls a page
- Menu items are now highlighted on hover
- The log view now shows commit ids
- The hide and switch menu items are cleaner
- File monitoring is better

### Fixed
- Closing a menu no longer closes the main view
- The mouse now works in menus, and on Linux

## [v0.80.257.540] - 2023-07-14

### Improved
- A search now selects the commit

## [v0.80.257.500] - 2023-07-14

### New
- Showing all recent, all active or all branches
- A dialog that sets a commit's branch
- Scrolling to a commit in the file history

### Improved
- Search and filter are better: the graph and a branch count in the results, mouse scrolling, and `*` to find ambiguous tips
- The branch structure menu now shows ambiguous branches
- Switching to a branch now shows it
- The whole row is now highlighted in the log
- Merge commit messages are clearer
- The menus are better, and Open Repo's recent paths disable the current path
- Deleting a branch now lists the shown branches first

### Fixed
- Showing a complex branch with many pull merges now works

## [v0.80.243.218] - 2023-06-30

### Improved
- Committing now offers to undo binary files

## [v0.80.242.208] - 2023-06-29

### New
- Undoing all uncommitted binary files

### Improved
- Committing modified files now warns about binary files too
- Showing branches gives fewer duplicates
- The Create Branch dialog now says **Publish** rather than **Push**

## [v0.80.241.218] - 2023-06-28

### Improved
- **Switch to Commit** has moved into the **Switch to** submenu
- Metadata is no longer fetched when sync is turned off
- The undo menu in the diff is better

### Fixed
- The diff of a merge commit is now parsed right

## [v0.80.239.954] - 2023-06-26

### New
- The **Open Repo** menu
- Committing from the diff
- The diff undoes and restores files, and all binary files, and its menu also has **Scroll to**
- The diff of uncommitted changes can be refreshed

### Improved
- Long diff lines are now marked with `…`
- File monitoring now pauses during a commit

### Fixed
- The updater now works
- File paths with spaces now work in diff, status, undo and clone

## [v0.80.232.224] - 2023-06-19

### New
- A merge message now includes a summary of the commits
- Copying commits and diff rows

### Fixed
- Showing branches now works
- The **Allow preview** option is now always shown in Config

## [v0.80.223.287] - 2023-06-10

### Improved
- Version 0.80
- `p` and `u` now push and pull the current branch, `Shift-P` and `Shift-U` all branches
- Push and pull messages, menus and info boxes are clearer
- Fetching now happens at better times
- The window title is now the repo's name

## [v0.50.215.219] - 2023-06-02

### New
- Switching to a commit, with a detached head

### Fixed
- Message boxes now have a title

## [v0.50.213.234] - 2023-05-31

### New
- The diff shows the changes within lines

### Improved
- Progress has moved to the upper left corner

## [v0.50.207.216] - 2023-05-25

### Improved
- Leading empty lines in commit messages are now skipped

## [v0.50.205.229] - 2023-05-23

### New
- Moving branches left or right changes their order
- Publishing a local-only branch

### Improved
- The diff is better
- Branches are now sorted in menus

### Fixed
- Config now works when there was no previous file

## [v0.50.202.436] - 2023-05-20

### New
- A diff within a line

### Improved
- The whole current row is now highlighted in the log

## [v0.50.194.217] - 2023-05-12

### New
- Ignoring whitespace in diffs
- Setting a commit's branch by hand

## [v0.50.190.252] - 2023-05-08

### New
- Adding and removing tags

## [v0.50.185.224] - 2023-05-03

### Improved
- **Show Branch** now has hierarchical branch names

## [v0.50.184.216] - 2023-05-02

### Improved
- The row found by a search is now highlighted

## [v0.50.183.675] - 2023-05-01

### New
- `a` amends

## [v0.50.183.563] - 2023-05-01

### New
- Amend

### Improved
- **Cherry Pick** has moved into the merge menu

## [v0.50.175.562] - 2023-04-23

### New
- A diff of branches, to and from
- A dialog that deletes a branch, with a force option
- A change log

## [v0.50.173.249] - 2023-04-21

### New
- Stash

### Improved
- The diff items have moved to a submenu

## [v0.50.167.459] - 2023-04-15

### New
- Changing a branch's color

### Improved
- Tags, branch tips and resolved ambiguous branches are clearer in the log
- Branch names from Azure Repos pull requests are better

## [v0.50.146.567] - 2023-03-25

### Improved
- The diff marks binary files better
- Tags have moved to the right side of the log

## [v0.50.146.466] - 2023-03-25

### New
- A preview of the diff of a merge

### Improved
- The clone dialog is better

## [v0.50.120.477] - 2023-02-27

### Improved
- The diff view is better

## [v0.50.114.456] - 2023-02-21

### Improved
- Automatic updates are clearer in their messages and behavior

## [v0.50.110.496] - 2023-02-17

### New
- The Config dialog

## [v0.50.85.545] - 2023-01-23

### Fixed
- Repos with no tags now work

## [v0.50.82.394] - 2023-01-20

### New
- Settings for the UI, metadata sync and preview updates
- Gmd checks for updates every hour

### Fixed
- Some events no longer need a mouse click to proceed

## [v0.50.75.387] - 2023-01-13

### Improved
- The error message on conflicts is clearer

## [v0.50.72.805] - 2023-01-10

### Improved
- Conflict markers have a better color

## [v0.50.72.793] - 2023-01-10

### Fixed
- The diff of a commit now works

## [v0.50.72.553] - 2023-01-10

### New
- Cherry pick

### Improved
- Updates now download faster

## [v0.50.66.1095] - 2023-01-04

### Improved
- The branch graph is better
- The current line is now highlighted

### Fixed
- Gmd no longer fetches over and over again

## [v0.50.50.494] - 2022-12-19

### New
- Creating a branch offers to push it and check it out

## [v0.50.50.382] - 2022-12-19

### Fixed
- Updating on macOS now works

## [v0.50.49.645] - 2022-12-18

### New
- Command line help

## [v0.50.49.509] - 2022-12-18

### New
- Empty repos, e.g. just after init, now work

## [v0.50.49.389] - 2022-12-18

### Fixed
- Repos without any tags now work

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
- Mouse support is better

## [v0.50.47.300] - 2022-12-16

### Improved
- Version 0.50

## [v0.40.46.859] - 2022-12-15

### Improved
- The commit details now show the author

## [v0.40.46.494] - 2022-12-15

### New
- A help dialog

## [v0.40.45.645] - 2022-12-14

### Improved
- Updates now install faster in most cases

## [v0.40.45.500] - 2022-12-14

### New
- A Windows installer, which downloads the latest Windows binary

### Fixed
- Some branch structures are now parsed right

## [v0.40.44.320] - 2022-12-13

### New
- Merging from a commit

## [v0.40.43.1123] - 2022-12-12

### Improved
- Version 0.40, with clearer menus

## [v0.30.42.938] - 2022-12-11

### New
- Switching to, checking out, a deleted branch

## [v0.30.42.901] - 2022-12-11

### Improved
- The uncommitted row now shows the merge while merging
- Merging a branch that is both local and remote now selects the latest commit

## [v0.30.42.813] - 2022-12-11

### Fixed
- Merging remote-only branches now works

## [v0.30.42.555] - 2022-12-11

### Improved
- Metadata is now pushed only when a branch is pushed

### Fixed
- Working out the branch structure no longer takes a long time

## [v0.30.41.979] - 2022-12-10

### Improved
- The update messages are clearer

## [v0.30.41.907] - 2022-12-10

### Improved
- The branch structure is better

## [v0.30.37.353] - 2022-12-06

### Improved
- Deleted folders are now removed from the recent folders

## [v0.30.36.925] - 2022-12-05

### New
- Clone

## [v0.30.36.591] - 2022-12-05

### Fixed
- Changes in `.git*` files no longer trigger refreshes

## [v0.30.32.894] - 2022-12-01

### Improved
- Gmd now runs on .NET 7

## [v0.30.32.367] - 2022-12-01

### Improved
- The branch structure is better

## [v0.30.30.522] - 2022-11-29

### New
- Pushing and pulling all branches

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
- Resolving ambiguous branches by hand

## [v0.30.24.729] - 2022-11-23

### New
- Undo

## [v0.30.24.357] - 2022-11-23

### Improved
- Many improvements

## [v0.22.1023] - 2022-11-21

### Fixed
- The show branches menu now works
