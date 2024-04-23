# Change Log for Gmd
--------------------

117 releases:

## [Current] - 2024-04-23
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
- Updated change log- Combined State and Config files
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
- Adjust change log- Adjusted the progress indicator

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
Fix some issues after update to .net 7Update to .NET 7

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
