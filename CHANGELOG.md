# Change Log for Gmd
--------------------

153 releases:

## [v1.14.1452.819] - 2026-10-21
- Step the diff's context 15 lines at a time with + and -, from 6, around the changes of the file the cursor is on, + saying when the whole file is shown; the diff menu's Show More Context and Show Less Context do the same, and Show All shows the whole file, - going back to the context it had
- Show more context with + on the ⋯ between two parts of a file too, where Enter no longer does
- Put the diff's row counting the files, e.g. '4 files changed  4 modified  +8 -8', right under who made the commit and when, one row less at the top of every diff
- Publish a branch not on origin yet, made with its Publish box unticked, with Publish in its branch menu, in place of Push and with no key; p no longer publishes any branch, and says that Publish does
- Put a branch deleted on origin back with Publish in its branch menu, asked first
- Grey out Push in the branch menu of a branch with nothing to push, where it could be picked and pushed nothing

## [v1.13.1450.1146] - 2026-10-19
- Draw in colors of gmd's own, on Terminal.Gui 2.5: twelve branch colors, a dark or a light palette picked by the terminal's background, or the terminal's own 16 colors, set with Colors in the Config dialog
- Color the code in the diff, the blame and the conflict resolver by its language, as VS Code's Dark+ and Light+ do, for some fifty languages; Config turns it off
- Keep the color a branch is first drawn in, picked unlike its parent's and those of the branches beside it, so that showing, hiding, pushing or merging branches never changes it
- Draw emoji in subjects as themselves
- Amend, drop and squash commits already pushed, and push the branch with force along with it, asked first on main, develop or a release branch, or for someone else's commits
- Push a branch with force when p asks, after a squash, an amend or a rebase of its pushed commits, made here or by an agent, rather than pull back the old version origin still has
- Rebase and Push onto is Rebase onto, which asks whether to push with force too
- Move commits made on the wrong branch, e.g. main, to a new one with Move to New Branch in the commit menu, and a commit above or below the one beside it with Reorder
- Start a squash's message with the oldest commit's, the others listed below it, as GitHub writes a squash merge
- Cherry pick the commits selected with Shift-↑↓ as they were made, authors kept, and take them back with one Undo
- Reset the current branch to any commit, keeping the changes uncommitted or discarding them, from the commit menu's Undo
- Offer Abort Merge, Abort Rebase and so on first in the commit menu's Undo while one is in progress
- Mark a branch squash or rebase merged on origin with ✓, and delete the local branches merged into main, by a squash on origin too, with Delete Merged Branches in the repo menu
- Tell a branch deleted on origin from one never pushed, so that p no longer publishes it again unasked
- Resolve all the conflicted files of a merge, rebase or other operation in one view, the next file opening in it, the decisions not saved kept for each file until it closes; ↑ ↓ in its list of files show each at once, and Unresolve This File (a) puts a resolved file's conflicts back
- Ask on d on the uncommitted row with conflicts whether to resolve them or show the diff; Ctrl-D shows the diff at once
- Blame the file at the cursor's line and commit with b in a diff, and show the file's history at a blamed line's commit with Shift-L; Esc goes back
- Pick the file of Blame File and File History in a new browser: the files the commit changed first, then all its files as a tree, typing finding a file by any part of its path
- Pick the folder to open, clone into, init or put a worktree in with a browser like it, as large as the terminal, a typed path listing the folders in it as a shell completes one
- Show more of the lines left out of a diff with Enter on its ⋯, as + does, and keep the cursor's line where it is on the screen as + and - change them
- Mark removed and added lines with - and + in a diff in one column, and a file's header with a bright cyan line
- Mark the commit marked for a diff with ± in the log, its row tinted, and take the mark away with Unmark for Diff; a diff of selected commits says how many in its title
- Scroll the diff, the blame and the resolver sideways with a mouse's sideways wheel, or Shift and the wheel, which no longer scrolls the log and the lists up and down
- Group the commit and branch menus by what their items do, and the Undo menu by the last change, undoing commits, discarding and bringing back; Set Commit Branch Manually is in the commit menu, Show Branch in the repo menu, and Full File History is File History
- Make Discard All Changes one item, whose question lists the ignored files and offers to delete them too
- Edit the commit message with Ctrl-A, Ctrl-C, Ctrl-X, Ctrl-V, Ctrl-Z and Ctrl-Y, as the subject
- Keep the branches set by hand in .git/gmd-metadata.json rather than in a ref in the repository, moved there at the first change
- Push only what is about commits origin has when the branch structure is shared, so the names of branches never pushed and work not pushed stay on your machine; Config says what sharing pushes before it is turned on
- List in the README everything gmd stores and sends

## [v1.12.1448.496] - 2026-10-17
- Draw the diff calmer: a bar beside each changed line, the words that differ marked rather than scattered letters, a row for each file, and a line with ⋯ on both sides where unchanged lines are left out between its parts, naming the function git finds a part in
- Go to the next or previous change with ] and [, and file with } and {, the top row naming the commit and the file the view is scrolled into
- Show the diff in one column on a terminal narrower than 110 columns, and switch between the two with v
- List the files beside a diff on a terminal 150 columns wide, with their lines added, deleted and left out of the commit, rather than at the diff's top; Tab moves into the list, and l hides it
- Hide the lines of a file in the diff with h, e.g. a generated one
- Resolve the files of a merge, rebase, cherry pick or revert one after the other, listed beside the resolver, from the conflict box, the top bar or the repo menu, and commit or continue once none is left
- Show each side of a conflict as what it changed in the common ancestor, its changed words marked, and name it by its branch and its commit's subject
- Combine both sides' changes to a conflict with c, where they changed other words or lines that only touch
- Show the whole file below the sides as it will be saved, going along with the cursor, each conflict's lines marked by the side they came from
- Type a conflict by hand in the result pane with e, with both sides still in sight

## [v1.11.1447.386] - 2026-10-16
- Commit part of a file: x in the diff of the uncommitted changes leaves the change the cursor is on, or the selected lines, out of the commit, which takes the rest and leaves them uncommitted
- Discard part of a file: u in the diff offers the change the cursor is on, or the selected lines, above the files whole
- Bring back what was discarded, a change, a file, all changes or a dropped stash, with Restore Discarded Changes in the Undo menu, kept for about 30 days
- Commit and discard some lines of a renamed file, which stays renamed, and of a deleted one
- Search the lines of a diff or a blame with f or /, the matches marked, and step through them with n and Shift-N
- Hide the changes of whitespace alone in a diff with w, a file with nothing else listed as whitespace only
- Sign the commit a squash makes when git's config signs commits, as every other commit gmd makes is
- Say why a greyed out item of the diff's or the blame's menu cannot be used on their own status line, rather than behind them

## [v1.10.1446.1006] - 2026-10-15
- Give a commit's message most of the details pane, with its fields in four rows, and name the branch of a parent or child on another one, so a merge says what it merged
- List the files a commit changed in its details, with the lines added and deleted in each, and the uncommitted files in theirs, the conflicted first
- Ask what to copy with Ctrl-C in the log: of a commit its id, message, subject or link, of the rows selected their ids, messages or range
- Show commit ids with eight characters rather than six, as git log --oneline does in all but small repositories
- Keep a branch set by hand by its commit's full id, so it never applies to another commit with the same first characters; one set with an earlier version is to be set again
- Show the latest 50,000 commits of a large repository rather than 30,000, in no more memory than before
- Read a large repository's log much faster when it has no commit-graph file, by writing one, as git gc would
- Decide the branches of long ambiguous stretches in a fraction of the time, e.g. 2 s rather than 17 s for 200,000 commits of rust
- Keep main's line through a merged tag or another project's branch pulled in, e.g. git's translations and gitk
- Recover a deleted branch that merge subjects name where other branches were started from it, leaving far fewer commits ambiguous
- Name a branch after a tag or a loose word of a merge subject only when nothing else names it
- Take a branch and a fork's copy of it, e.g. timfel/1.2.x, as one where they meet
- Let a branch started on the merge of the one before it share that one's column

## [v1.9.1446.426] - 2026-10-15
- Never offer a preview to a gmd installed with Homebrew, which has the stable releases only
- Grey out Auto update and Allow preview releases in Config for a gmd installed with Homebrew, and say why
- Say after the PowerShell install on Windows that gmd runs in that terminal already

## [v1.8.1445.1141] - 2026-10-14
- Install gmd on Windows with one PowerShell command, into a folder and a PATH of your own, with no admin rights needed
- Install gmd in a dev container with the feature ghcr.io/michael-reichenauer/gmd/gmd:1
- Leave updating a gmd installed with winget to winget, whose command ⇓ says, once gmd is closed
- Leave the Co-Authored-By lines of merges out of the change log and the release notes

## [v1.7.1445.1049] - 2026-10-14
- Install gmd with Homebrew on macOS and Linux, 'brew install michael-reichenauer/tap/gmd', and leave updating it to Homebrew, whose command ⇓ says
- Fixed gmd hanging at its start on macOS, now and then, before anything was drawn
- Show the change log before an update, with a Change Log button in the question, and say before updating that gmd quits once updated
- Let an update be cancelled, with Cancel or Esc in the box shown while it runs, after asking, up to the moment gmd is replaced
- Download updates over a slow connection, which failed below about 8 Mbit/s
- Check a new version against its checksum, and that it runs, before it replaces gmd
- Never leave gmd missing when an update fails, and replace it in one step on Linux and macOS
- Try a failed update again without restarting gmd
- Install an automatic update once, not again every hour until gmd is restarted
- Install updates in a folder with a space in its path
- Offer only a release that has a binary for this platform
- Say that gmd's folder cannot be written to before an update downloads anything, rather than failing in the download
- Ask before Rebase and Push onto pushes with force, and refuse it while origin has commits the branch has not
- Squash whole or not at all, keeping a merge among the commits after the squashed ones, and their authors and dates
- Push the shared branch choices without wiping out ones another clone pushed meanwhile
- Never hide the main branch: h says it is always shown, and Hide Branch is greyed out for it
- Refuse branch and tag names with a space, which git took as two names
- Keep a file conflicted until all its conflicts are resolved, and mark one resolved by hand as it is
- Show diffs whatever the git config says (diff.noprefix, color.ui, log.showSignature, ...)
- Show, commit, discard and blame files with non-ASCII names
- Show the changes of a file whose mode changed as well
- Show a change of line ends alone in the diff, as a ␍ on the side with the CR, and keep a line with a lone CR whole
- Tell text files from binary ones as git does, by their bytes
- Commit, tag and stash messages with \" or a trailing \ in them
- Show a rebase that stops again on conflicts as a conflict, not an error
- Keep a stash's message whole when it has a colon
- Read authors with a '|' in their name
- Open a working tree in a folder named like 'project.git'
- Blame and track origin's tags in SHA-256 repositories
- Open a repo in the middle of merging a commit no branch has, e.g. after 'git pull <url> <branch>' stopped on a conflict
- Keep a branch set by hand, an ambiguity resolved or a branch renamed when a fetch brings origin's older choice
- Merge and rebase onto the side of a branch that has the commits, not the one with the newer time
- Fixed a refresh putting back the repo from before a newer read, and undoing a branch shown or hidden while it read
- Fixed gmd stopping on a click in an empty menu, on m after a branch was hidden, when a fetch pruned a highlighted branch's remote copy, on Uncommit of the first commit, on a cherry pick of rows of two branches, and on a copy after a drag below the log
- Never let the log stop gmd: a read-only gmd.log hung it on a blank screen
- Fixed a click on a menu's border running the item next to it, and Esc on a question answering Yes
- Fixed the keyboard staying dead after a menu opened by mouse during a refresh
- Make Push and Pull in the menus do what p, u and Shift-U do, and offer Pull All with uncommitted changes
- Make b, d, m and Shift-E do what their key hints and menu items say
- Let Amend give the last commit a new message alone, with changes there too, and name the right branch in the commit dialog
- Offer Uncommit, Squash and a range Cherry Pick only where they work, and say why not
- Fixed the diff: c only for the uncommitted changes, a copy of an added file without line numbers, Scroll to a file, and + and - in a file history
- Count the files of each kind and the lines added and removed above a diff's file list
- Number the diff's right side correctly after conflict markers
- Offer no + and - in a line history, and show an uncommitted line's diff from blame read only
- Fixed where clicks and the cursor land in the conflict resolver and in blame, and scrolling sideways past the text
- Scroll the resolver's result with Shift-Up and Shift-Down or the mouse wheel, expand its tabs, and keep its cursor on its conflict when b shows the ancestor
- Keep the columns of the log, the diff, blame and the resolver in place with Chinese, Japanese and emoji text
- Show branch tips, tags and the graph whole on a narrow terminal
- Lay out the graph faster with many branches shown
- Clone into the folder browsed to, read -d as the other options are read, and give a large file's size when committing
- Say why g leaves main and a deleted branch their colors, and that there is nothing to merge
- Say that a new tag was added when only its push failed
- Start a mouse drag's selection where the button was pressed
- Never propose making the home folder, or the folder of the recent repos, a repo with Init
- Open the commit and amend dialogs from the menu at once
- Give the branch field of Create Worktree the context menu of the other text fields
- Fill a view whose content shrank past the rows shown
- Say in the help how to make macOS Terminal send Shift-Up and Shift-Down

## [v1.6.1444.518] - 2026-10-13
- Improve notification about new available update

## [v1.5.1444.482] - 2026-10-13
- Replace the start menu with a start page
- Document the start page

## [v1.4.1442.1069] - 2026-10-11
- Fixed error when closing/hiding a branch

## [v1.3.1440.271] - 2026-10-09
- Updated demo animation

## [v1.2.1439.1149] - 2026-10-08
- Pin the release action to a commit rather than a tag

## [v1.1.1439.1092] - 2026-10-08
- Improve show update available symbol in the bar when a release is found after startup
- Name the row's commit on the key-hint line rather than in the top bar

## [v1.0.1439.952] - 2026-10-08
- Bump the nuget group with 2 updates
- The help opens on GitHub rather than in a dialog
- Version 1.0
- Lease a force push on the commit the user saw, so it never overwrites commits not seen
- Keep gmd starting when its config file is broken

## [v0.97.1439.614] - 2026-10-08
- Release notes from the change log, and checksums and gmd.exe in every release
- Keep only the latest five previews on the Releases page
- Badges and a quick install at the top of the README
- A security policy, a pull request template and Dependabot

## [v0.96.1439.476] - 2026-10-08
- Restructure the help: the screen first, then tasks, then reference
- Correct the help's description of the lines between branch columns
- Rework the README for new users, and move development to CONTRIBUTING
- Say what failed when gmd crashes, and where to report it
- Say once that branches are hidden, and how to show one
- Give the Show Branch menu one name, and its key one spelling
- Name both branches in the merge items
- Copy the commit id and message with i and Shift-I
- Show in Add Tag that it pushes the tag, and let it be left out
- Write the year in full in the dates
- Name the sides in the resolver's status line
- Copy a blamed line's commit with i and Shift-I, as in the log
- Take the duplicate items out of the branch and commit menus
- Stop the branch sort from hanging on orders that contradict each other
- Move Clean Working Folder to Undo, and list what it deletes
- Write shifted keys as ⇧ in the menus, as the key-hint line does
- Give the diff, blame and conflict views key-hint lines of their own
- Make the help easier to get around: sections, height, and where it opens
- Let the views of a dialog with keys of its own have their hot keys
- Choose which files to commit, in a checklist in the commit dialog
- Re-record the animation for the commit dialog's file list
- Amend or drop an older commit with git's own rebase, recorded for Undo
- Amend and Drop of any commit not pushed yet, from its commit menu
- Ask git's and ssh's questions in a login dialog
- Say how to try the login dialog by hand, and what is left of it
- Stash Apply, which brings a stash back and keeps it
- List every tag, and push or remove one from the list
- Diff any two commits: Mark for Diff, then Diff with it
- Search the changes themselves with change:
- The history of lines, from the blame view
- Captions in the demo animation, saying what each part shows and with which key

## [v0.95.1438.449] - 2026-10-07
- Make Ctrl-A in the commit dialog add only the subjects of the merged commits
- Show the current branch when it becomes current
- Count the hidden branches with something new, local and new ones too, as ✦
- See the new branches once they are listed
- Mark a hidden branch with no commits of its own where it branches out
- Classify what a branch reflog records, for Undo
- Undo the last change of the current branch
- Undo the last change of a branch that is not checked out
- Undo a squash as one step
- Find the lines of work no branch has any more
- Recover lost commits from the Undo menu
- Tell when origin was rewritten by a force push
- Pull a branch whose origin was rewritten without merging the two
- Restore origin from before a force push
- Record Undo, Recover and the force push handling in the docs
- Keep the commits not pushed yet out of a squash of pushed ones
- Copy the saved undo steps when the repo is read
- Diff a range from a root commit from the empty tree
- Undo the current branch only from where it was read
- Tell every force push a refresh finds, not only the first
- Count only the commits a pull of a rewritten branch moves
- Tell a force push that only dropped commits from origin
- Added the git commands a restore of a deleted branch needs
- Restore a branch gmd deleted
- Tell deleted branch records apart by their pair, not their name
- Say a branch deleted with its worktree can be restored
- Share one list dialog between worktrees, lost commits and deleted branches
- Share the side boxes of the delete and restore branch dialogs
- Make Cancel the default of the Delete Branch dialog

## [v0.94.1431.496] - 2026-09-30
- Say what a commit, push or pull is doing while git works

## [v0.94.1429.782] - 2026-09-28
- Version 0.94
- Updated packages
- Improve updating functionality

## [v0.91.1429.412] - 2026-09-28
- Improve UI usability
- Improve algorithm to determine which branch a commit "belongs" to
- Improve git log parsing performance
- Fixed issue with scrolling commit details

## [v0.91.1425.955] - 2026-09-24
- Ignore Claude worktrees folder
- Added spell check hint in Commits dialog
- Build with the .NET 11 SDK for the C# 15 compiler
- Updated help file
- Updated README and and install script

## [v0.91.1410.911] - 2026-09-09
- Added git Worktree support
- Fixed issue with missing cursor after spell error fix in commits
- Fixed progress for commit

## [v0.91.1406.461] - 2026-09-05
- Added spell-check support for text fields in commit, tag, ...
- Fixed issue how branches are determined

## [v0.91.1404.491] - 2026-09-03
- Fixed issue with "Pull/Update All" for branches
- Move some Branches menu items
- Fixed fetch issue, did not really update and fetch

## [v0.91.1394.267] - 2026-08-24
- Fixed spell warnings

## [v0.91.1388.1436] - 2026-08-18
- Modernized codebase
- Fixed 100% CPU usage on linux/max by updating Terminal.Gui to 1.19.0
- Added support for e2e terminnal testing
- Fixed issue with key 'q' and 'p' in log view
- Copy text refactor, works on more platforms
- Show deleted (non-active) branches as gray
- Added progress to upgrading the app
- Added support for rename branch
- Added 'Branches' sub menu to commit menu
- Move file history menu item to repo menu
- Implement 'Merge to' functionality
- Implement 'blame' support
- Added support for adjusting diff context size
- Added support for conflict handling
- Fixed issue with local only tag beeing pruned
- Added more unit and e2e ui tests
- Fixed some bugs

## [v0.91.1364.357] - 2026-07-25
- New version
- Adjust installtools to include some more tools

## [v0.91.1119.391] - 2025-11-22
- Fixed path

## [v0.91.1119.384] - 2025-11-22
- Adjust osx asset name

## [v0.91.1119.371] - 2025-11-22
- Adjust install.sh

## [v0.91.1119.328] - 2025-11-22
- Fixed path

## [v0.91.1119.279] - 2025-11-22
- Adjust building script and artifacts to support arm64
- Support updater for x86 and arm64

## [v0.91.716.1041] - 2024-10-15
- Added support for annotated tags with messages
- Fixed issue with clicking outside last commit
- Added xsel (for copy text) to installtoools
- Added updatepackages script
- Updated some packages
- Added support for running difftool and merge tool in diff view
- Only show diff tool items for uncommited diffs

## [v0.91.671.467] - 2024-08-31
- Improve mark leading and trailing space diffs
- Upgrade to .NET 8
- Fixed .net version in build script
- Fixed issue with author intials for branch tips
- Fixed an issue with SetBranchManuallyDlg

## [v0.91.541.196] - 2024-04-23
- Added support for showing my active branches
- Fixed issue with craching gmd when status changed
- Allow pull all even if uncommited changes
- Support squash commits

## [v0.91.530.207] - 2024-04-12
- Adjust stash to start with empty message
- Adjust selecting commits to highlight on same branch
- Fixed copy of selected commits within branch

## [v0.91.453.278] - 2024-01-26
- Fixed show menu crash on main branch

## [v0.91.449.265] - 2024-01-22
- Allow rebase current branch to master
- Added tip author initals to branch menu items

## [v0.91.419.545] - 2023-12-23
- Alow git pull with rebase
- Only allow rebase if current is local only
- Fixed issue with fresing ui when after rb on remote branch
- Allow rebaset to current even if remote
- Push force after rebase if local branch has remote
- Adjust how push after rebase is handled by status updates
- Adjust push --forece to push --force-with-lease
- Added support for uncommmit until current row
- Added support for squash until current row
- Use branche primary name for determine color
- Added support diff range
- Handle stashed values if commit no longer exists
- Added support for named stash
- Adjust stash dialog with curren subject
- Added support range cherry pick
- Added support for cherrypick a range of commits
- Support rebasing current branch to any branch

## [v0.91.350.838] - 2023-10-15
- Increase recent folder count in startup view
- Adjust warning texts of commmiting large files
- Rename 'switch' to 'switch/checkout'
- Skip moving up/down to branch when moving left/right
- Using green/red background in diff view

## [v0.91.317.1113] - 2023-09-12
- Show white space diff

## [v0.91.317.211] - 2023-09-12
- Disable Branches menu in commitmenu, add Hide all branches

## [v0.91.314.262] - 2023-09-09
- Fixed 'branch out more' issue

## [v0.91.311.1117] - 2023-09-06
- Adjust application bar
- Show empty repo commit in dark gray
- Adjust current and uncommites symbol shown
- Added ... on left side of diff if scrolled to right
- Added branches menues to commit menu
- Adjust version text
- Fixed OpenRepo menu and 'O' command
- Adjust recent repos count

## [v0.90.310.1122] - 2023-09-05
- Fixed amend
- Improve menues

## [v0.90.307.299] - 2023-09-02
- Updated change log
- Combined State and Config files
- Adjusted margin between sibling branches in graph
- Moved stash submenu from repo menu to commit menu
- Fixed issue with hiding wrong branch when using 'h'
- Adjust Application bar
- Fixed issue with change color of branch
- Fixed diff of empty repo uncommited diff
- Fixed Diff branches
- Support create branch in local repo (no longedr error msg)

## [v0.90.293.882] - 2023-08-19
- Adjust Readme

## [v0.90.293.878] - 2023-08-19
- Updated Readme
- Updated Help documentation

## [v0.90.293.440] - 2023-08-19
- Adjust filter/search x
- Added support for git init
- Improve menu width calculation
- Fixed issue with hiding pull merge branch
- Added 'Init repo' to main view menu
- Added support for stash status in ApplicationBar and commits

## [v0.90.282.1137] - 2023-08-08
- Try to improve graph branch overlapping handling
- Fixed graph width error
- Fixed refresh after diff
- Adjust update info and added sln file
- Support unset branch in SetBranchManually dlg
- Diff of added or removed file use one column
- Improve branch structure

## [v0.90.277.1021] - 2023-08-03
- Added short cut 'Y' to show current branch menu item
- Try fix refresh after remove tag
- Improve handling of binary files when committing.
- Adjust progress
- Adjust commit dialog
- Adjust Application and Filter bars
- Set current row when rightclick
- Adjust branch structure
- Fixed push local-only branch
- Fixed issue with focus on buttons and checkboxes
- Try fix progess when clicking on ApplicationBar
- Adjust the symbol of shown branches to 'o'
- Fixed isue with creating branch
- Added close button to ApplicationBar and filterBar

## [v0.90.272.349] - 2023-07-29
- Impoved set branch manually dialog
- Limit filter results to 5000
- Added support for OpenBranchMenu shortcut 'Shift  →'
- Adjust menus pos for ApplicationBar
- Added support for shortcut for diff for branches

## [v0.90.269.290] - 2023-07-26
- Improve combobox with mouseclick support
- Improve branch name parsing
- Improve unicode char set dialog

## [v0.90.268.282] - 2023-07-25
- Support middle mouse botton click to merge from branch
- Fixed click on branch issue

## [v0.90.267.1006] - 2023-07-24
- Improve ApplicationBar
- Updated help file
- Support double click on branch to switch to branch

## [v0.90.267.349] - 2023-07-24
- Added support for updating from mainn view menu
- Added support for ApplicationBar with repo and commit info
- Adjust dialog button focus color
- Fixed issue with not closing main view on closing menu
- Support move left/right to branches and up/down to commits
- Support both branch and commit menus
- Support hoover/highlight branch and commit
- Support open/close child branch by clicking on branch commits
- Support close branch clicking on tip or bottom commit
- Added shortcut 'y" to go to current branch
- Scroll page on space
- Version 0.90
- Improved FileMonitor functionality
- Added support for clicking in graph to open/close branches
- Highlight menu item when hover with mouse
- Fixed menu mouse support
- Enable show sid in log view
- Adjust change log generation
- Added support for Copy and mouse select
- Fixed mouse support on linux
- Improve menu and clean hide and switch menu items
- Fixed hide and switch menu items

## [v0.80.257.540] - 2023-07-14
- Improve select commit after search

## [v0.80.257.500] - 2023-07-14
- Added branch count to filter
- Adjust how add merge commits messages are formated
- Added graph to filter/search view results
- Added mouse scroll support to filter/search
- Adjust how OS detection is handled
- Improved serach/filter functionality
- Support showing all recent, all active or all branches
- Show branch when switching
- Show ambiguous branches in branch structure menu
- Support search/filter of ambiguous tips using '*'
- In repo view highlight whole row
- Added support for set branch with dialog
- Added support for combo text fields
- Adjust menues
- Added support to scroll to commit in file history
- In open repo, disable current path in recent paths
- When delete branch, shown branches are listed first
- Fixed issue when showing complex branch with many pullmerges
- Adjust how shown branch names are handled

## [v0.80.243.218] - 2023-06-30
- Adjust commit to offer undo of binary files
- Added releases count to change log file

## [v0.80.242.208] - 2023-06-29
- Added support for undo all unccmmitted binary files
- Adjust binary commit warning for commit also for modified files
- Try to eliminate duplicates in show branches
- Adjust create branch dialog to use publish instead of push

## [v0.80.241.218] - 2023-06-28
- Move switch to commit into switch to submenu
- Disable metadata fetch if sync is disabled
- Fixed diff parsing of merge commit
- Adjust undo menu in diff

## [v0.80.239.954] - 2023-06-26
- Fixed updater
- Added support for open repo menu
- Added support for commit from within diff
- Mark long diff lines with a '...' char
- Added support for undo/restore all binary files in diff
- Added support in diff for main menu with scroll to and undo
- Refresh diff after undo
- Added support for undo/restore files in diff
- Adjust push branch and enhanse push/fetch logging
- Added support for refresh diff of uncommitted changes
- Refactor diff copy
- Fixed issue with ' ' in file paths when diff and status/undo
- Try to fix path with space in clone
- Added install file exists check
- Adjust check mark signs
- Pause file monitor during git commit
- Trying different folder montitor event handling

## [v0.80.233.211] - 2023-06-20
- Adjusted check mark chars

## [v0.80.232.224] - 2023-06-19
- Fixed issue with showing branches
- Added support for including summery of commits in merge
- Fixed config issue with not always showing 'allow preview' option
- Added support for copy commits and diff rows

## [v0.80.225.1107] - 2023-06-12
- Adjusted push/pull messages
- Adjusted menues

## [v0.80.223.287] - 2023-06-10
- Adjusted menues
- Changed color of info message boxes
- Adjusted shortcuts for push/pull for current (p/u) and all (P/U) branches
- Version 0.80.*
- Adjusted when fetch is done
- Setting windows title to repo name

## [v0.50.215.219] - 2023-06-02
- Added support for switch to commit
- Handles Detached head state
- Fixed issue with message boxes without title

## [v0.50.213.234] - 2023-05-31
- Added support for inline diff showing diffs within lines
- Moved progress to upper left corner

## [v0.50.211.218] - 2023-05-29
- Adjust change log
- Adjusted the progress indicator

## [v0.50.207.216] - 2023-05-25
- Skip leading empty lines in commit messages

## [v0.50.205.229] - 2023-05-23
- Support of moving branches left/right to change visual order
- Support of publish local only branch
- Improved diff
- Refactored ui dialogs
- Fixed issue with config when no precious file
- Adjusted sort of branches in menues

## [v0.50.202.436] - 2023-05-20
- Highlight current whole row in log
- Added support for diff within a line
- Refactored dialogs

## [v0.50.194.217] - 2023-05-12
- Ignoring white space in diffs
- Added support for manualy set/determine branch of a commit

## [v0.50.190.252] - 2023-05-08
- Added support for add and remove tags

## [v0.50.185.224] - 2023-05-03
- Added support for hierarchical branch names i show branch

## [v0.50.184.216] - 2023-05-02
- Highligting searched row

## [v0.50.183.675] - 2023-05-01
- Added short cut 'a' to amend command
- Added post commit hoog sample file for changelog generation

## [v0.50.183.563] - 2023-05-01
- Added support for amend cmd
- Moved cherry pic menu item into merge menu

## [v0.50.176.232] - 2023-04-24
- Updated change log

## [v0.50.176.217] - 2023-04-24
- Added change log link to relases

## [v0.50.175.562] - 2023-04-23
Merge branch 'dev' into main

- Added support for branches diff (to/from)
- Added support for delete branch dlg with force option
- Added support for change log file

## [v0.50.173.249] - 2023-04-21
- Added support for Stash
- Moved diff menu items to a sup menu

## [v0.50.167.459] - 2023-04-15
- Added support for changing branch color
- Adjusted how tag, branch tips are shown in log
- Adjust how resolved ambiguous branches are indicated
- Improved branch name parsing for Azure repos pull requests

## [v0.50.146.567] - 2023-03-25
- Adjusted binary marking in diff
- Moved tags to right side in log

## [v0.50.146.466] - 2023-03-25
- Improved clode dialog
- Added support for preview merge diff

## [v0.50.120.477] - 2023-02-27
- Improved diff view

## [v0.50.114.456] - 2023-02-21
- Adjusted autoupdate message and behaviour

## [v0.50.110.496] - 2023-02-17
- Added support for Config  dialog

## [v0.50.85.545] - 2023-01-23
- Fixed issue with repos with no tags

## [v0.50.85.375] - 2023-01-23
- Updated README.md

## [v0.50.85.373] - 2023-01-23
- Create README.md

## [v0.50.82.394] - 2023-01-20
- Added support for ui config
- Added support for option: meta data sync
- Added support for option: enable preivew updates
- Fixed issue when events needed mouse click to procede
- Added support for checking for updates every hour

## [v0.50.75.387] - 2023-01-13
- Adjust error msg when conflicts

## [v0.50.72.805] - 2023-01-10
- Adjust conflict marker color

## [v0.50.72.793] - 2023-01-10
- Fixed commit diff

## [v0.50.72.553] - 2023-01-10
- Added support for Cherry pick
- Impoved updates downloads

## [v0.50.66.1095] - 2023-01-04
- Improved branch graph
- Added current line highligt
- Fixed some issue, where gmd would contiously repeat fetch repo

## [v0.50.50.494] - 2022-12-19
- Added support for push/checkout create branch options

## [v0.50.50.382] - 2022-12-19
- Fixed issues with updating on mac/osx

## [v0.50.49.645] - 2022-12-18
- Added command line help text

## [v0.50.49.509] - 2022-12-18
- Added support for empty repos (after init)

## [v0.50.49.389] - 2022-12-18
- Fixed issue with reopos without any tags

## [v0.50.48.467] - 2022-12-17
- Changed default AllowPreview option to false
  I.e. now users must manually set to true to see preview releases.

## [v0.50.48.458] - 2022-12-17
- Added support for building both release and preview releases

## [v0.50.47.827] - 2022-12-16
- Adjust commit details to show last row in message

## [v0.50.47.669] - 2022-12-16
- Imporve mouse support

## [v0.50.47.350] - 2022-12-16
- Updated Readme file

## [v0.50.47.300] - 2022-12-16
- Version 0.50

## [v0.40.46.940] - 2022-12-15
- Updated readme file

## [v0.40.46.866] - 2022-12-15
- Updated readme file

## [v0.40.46.859] - 2022-12-15
- Updated help file
- Added author to commit details.

## [v0.40.46.494] - 2022-12-15
- Added support for Help dialog

## [v0.40.46.437] - 2022-12-15
- Added support for help

## [v0.40.45.732] - 2022-12-14
- Text change

## [v0.40.45.645] - 2022-12-14
- Faster updrade installs in most cases

## [v0.40.45.500] - 2022-12-14
- Added windows installer, which downloads latest windows binary
- Fixed issue with parsing some branch structures

## [v0.40.44.320] - 2022-12-13
- Added support for merge from commit
- Adjusted download path to latest release

## [v0.40.43.1123] - 2022-12-12
- Version 0.40.x
- Adjust some menues

## [v0.30.42.938] - 2022-12-11
- Added support for switchin/checkout a deleted branch

## [v0.30.42.901] - 2022-12-11
- Added support for showing merge for uncommitted commit line when merging
- Select latest commit when merging branch with both local and remote

## [v0.30.42.813] - 2022-12-11
- Fixed issue with merging remote only branches

## [v0.30.42.555] - 2022-12-11
- Only push meta data when pushing some branch
- Fixed issue with branch structure taking long time
- Clean

## [v0.30.41.979] - 2022-12-10
- Adjust update messages

## [v0.30.41.907] - 2022-12-10
- Improved branch structure

## [v0.30.37.353] - 2022-12-06
- Clean recent folder list when folders deleted

## [v0.30.36.925] - 2022-12-05
- Added support for clone

## [v0.30.36.591] - 2022-12-05
- Fixed issue with change events in .gitxxx files
- Adjust container post create commands

## [v0.30.32.894] - 2022-12-01
Fix some issues after update to .net 7
Update to .NET 7

## [v0.30.32.367] - 2022-12-01
- Improve branch structure

## [v0.30.31.840] - 2022-11-30
- Improve branch structure

## [v0.30.30.522] - 2022-11-29
- Added support for push/pull all branches

## [v0.30.29.779] - 2022-11-28
- Updated readme

## [v0.30.29.759] - 2022-11-28
- Fixed image url

## [v0.30.29.755] - 2022-11-28
Update readme file

## [v0.30.29.632] - 2022-11-28
- Added about dlg to main view menu

## [v0.30.29.594] - 2022-11-28
- Fixed setup path

## [v0.30.29.577] - 2022-11-28
- Fixed publish setup path

## [v0.30.29.573] - 2022-11-28
- Added support for windows setup file

## [v0.30.29.496] - 2022-11-28
- Added support for update cmd line option

## [v0.30.29.429] - 2022-11-28
Some code cleanup

## [v0.30.26.309] - 2022-11-25
- Added support for file history

## [v0.30.25.670] - 2022-11-24
- Improved ambiguity resolving

## [v0.30.25.487] - 2022-11-24
- Added support for manually resolving ambiguous branches

## [v0.30.24.729] - 2022-11-23
- Support for undo

## [v0.30.24.363] - 2022-11-23
- Fix

## [v0.30.24.357] - 2022-11-23
A lot of improvments

## [v0.22.1228] - 2022-11-21
- Adjust build

## [v0.22.1194] - 2022-11-21
- More adjust build time version

## [v0.22.1178] - 2022-11-21
- Minor adjustments

## [v0.22.1031] - 2022-11-21
- Fixed build base version to utc

## [v0.22.1023] - 2022-11-21
Fixed show branches menu
