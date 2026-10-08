# Usability review

Two reviews of gmd's user experience, each with ranked proposals. gmd grew one feature at a time for
its author's own use, so both look at it as someone new would.

- **The product review (2026-10-07)**, first below, steps back from the keys: whether choosing which
  branches are shown is a sound idea, who besides its author can use gmd, which features are missing
  or not needed, and whether the README and the help explain it.
- **The usability review (2026-09-24)**, from *What already works well* on, covers every key binding,
  menu, dialog and message; the screens as the end-to-end snapshots draw them; and the everyday
  workflows, compared with lazygit, tig and gitui.

The usability review's findings are sorted under five usability principles. Each is a question to
ask of any new feature:

- **Safety**: can a slip of the finger cost work, or change the remote? Mistakes will happen, so the
  costly ones need a guard and the cheap ones a way back.
- **Discoverability**: can someone who has never read the help find the command?
- **Consistency**: does a key or a word mean the same thing everywhere?
- **Feedback**: after every key, does the user know what happened, or why nothing did?
- **Workflow fit**: are the everyday tasks short, compared with the tools people already know?

File references are as of each review. Close items here, or move them to `MODERNIZATION.md`, as
they land. The usability review's safety findings (section 1, and Tier 1 of its proposals) are
fixed, and most of Tiers 2 to 4; the product review's proposals are open.

---

## Product review (2026-10-07)

Most of the usability review's proposals are done, so this one asks the questions above them. It was
made from the code, the end-to-end snapshots, the README and the help, and one experiment (the
prompts, in part 2). It has three audiences in mind, the ones gmd is pitched to: people who use git
in the terminal (lazygit, tig, gitui), teams whose branches are merged with merge commits, and
developers who run AI coding agents in branches and worktrees of their own.

### 1. The idea: choosing which branches are shown

- **It is sound, and less odd than it first looks.** Git records no branch per commit, so most
  clients draw the history by its shape: a line per chain of commits, columns reused as lines end,
  and colors that say nothing of the branch. gmd answers the question people actually ask of a
  commit, which branch it was made on, the way Mercurial's named branches, Plastic SCM and TFS have
  it. Each branch keeps one column and one color, main runs down the left, and a branch that was
  merged and deleted keeps its column, recovered from the merge message.
- **Others have parts of it, none all of it.** GitKraken and GitLens can hide or solo a branch, and
  Sourcetree and Fork can show the current branch only. None keeps a branch to its column and color,
  brings back the deleted ones, or starts from showing less.
- **"A squash merge that you can take back"** (the README) is the pitch: the log is as clean as a
  squashed history, and nothing was rewritten to get it.
- **Where it is strongest:** merge-commit workflows (git-flow, pull requests merged with a merge
  commit, long-lived release branches), and many branches at once, the branches AI agents make among
  them, where ✦ says which hidden ones have news without showing them all. gmd's own merges are
  `--no-ff` (`BranchService.cs:132`), which is what keeps a branch's identity in the history.
- **Where it has little to do:** a repository whose pull requests are squash merged or rebase
  merged, as many on GitHub are. Main is a straight line, the branches leave no trace once deleted,
  and there is nothing to hide; gmd is then a terminal client like the others. The README should
  say so, rather than let someone find out.
- **The risk is the first five minutes, not the idea:**
  - The first screen of a repository shows main and the current branch
    (`ViewRepoCreater.cs:415-423`). Every other branch is a dark `╮` or `╯` beside a commit of main,
    and nothing says how many are hidden. Someone new may take their branches for missing.
  - The branch gmd works out for a commit is drawn as a fact. Only an ambiguous one says otherwise,
    with `(~ambiguous)` at its tip, and a wrong guess drawn as a fact costs more trust than a gap.
  - One menu has three names: *Open Branch (type to find)* when `Shift-→` opens it
    (`BranchMenu.cs:52`), *Show Branch* as a submenu and in the help (`:698`), and *Show/Hide
    Branch* when Enter opens the branches at a commit (`:62`). Its key is written three ways: `⇧→`
    in the hint line, `Shift →` in the menus, `Shift-→` in the help.
  - The merge items change words with the menu they are in. In the menu of `dev`, `e` is *Merge to
    main* and `Shift-E` *Merge from main*; in the menu of the current branch the same keys are
    *Merge from* and *Merge to*, each a list. The keys always do the same (`e` merges into the
    current branch), but the words make the reader work it out each time.

### 2. Features a new user expects that are missing

Ranked by how soon someone new meets the gap.

1. **Choosing what to commit.** A commit is always `git add .` and then `git commit -a`
   (`CommitService.cs:41-47`), new files included. Every other client lets you pick the files, and
   lazygit users expect hunks. The file checklist in the commit dialog (Tier 4, item 1 of the
   usability review) is the right size for gmd, and it is also the guard against committing a stray
   `.env` or a debug log. *Done (2026-10-07), see proposal B.1.*
2. **Passwords, passphrases and host keys.** gmd runs git on its own terminal, with no
   `GIT_TERMINAL_PROMPT`, no askpass and no timeout (`Cmd.cs:209-243`), and fetches in the background
   every five minutes. Tried with a stand-in for ssh that asks on `/dev/tty` as ssh does (a
   `core.sshCommand` script, so no network):
   - The fetch right after opening wrote its prompt over the bottom row, and the screen scrolled up
     a row, so the top bar was gone, and stayed gone.
   - The keys typed for gmd went to the prompt: `?` and `q` arrived as the passphrase `?q`, and gmd
     did nothing, not even quit.
   - Enter did not end the prompt: the terminal is raw, so Enter sends `\r`, and a prompt that reads
     a line waits for `\n`. Only Ctrl-J did, and then the status line said *Fetch failed: Could not
     read from remote repository*.
   - `p` showed *Pushing 'main'...* and the prompt again, and waited until that Ctrl-J.

   OpenSSH's own passphrase and host-key prompts stop at `\r` too, so with the real ssh Enter would
   end them, but they still take the keys meant for gmd and draw over it. Git's username and
   password prompt for https reads a line, as the stand-in did. A machine already set up with an ssh
   agent or a credential helper never meets any of this; a new user's first push may.
3. **Light terminals.** Every color is on a forced black background (`Color.cs:47`), so a light
   terminal theme, the default of macOS Terminal in light mode, shows gmd as a black box. `NO_COLOR`
   is not read, and red and green are both branch colors. All three wait for the Terminal.Gui 2.x
   port (`MODERNIZATION.md`).
4. **Copying a commit id or message from the log.** `CopyCommitId` and `CopyCommitMessage` exist,
   and nothing calls them (`RepoCommands.cs:447-458`). It is one of the commonest things done in a
   log.
5. **Reword, fix up or drop a commit not yet pushed.** Squash is the only rewrite there is. Kept to
   commits not yet pushed, these stay true to leaving the shared history alone. *Done
   (2026-10-07):* as Amend and Drop of any commit not pushed yet, see B.3. Amend had only checked
   'ahead of the remote', so it refused the last commit of a branch never pushed, which Uncommit
   took back; one rule for not pushed now serves all of them (`CommitRewrite`).
6. **A second remote.** `origin` is written into the fetch, the push and the name matching
   (`RemoteService.cs:33`), so a fork cannot follow its `upstream`.
7. **Smaller gaps:** a stash apply that keeps the stash; a list of the tags, and pushing them; the
   history of a range of lines (`git log -L`) and a search of the changes themselves (`-S`); a diff
   of any two commits; a merge tool that runs in the terminal, such as vimdiff, whose screen is
   captured rather than shown. *Done (2026-10-08), all but the terminal merge tool, which a tool
   with a window of its own already covers, as does gmd's own resolver:* Stash Apply beside Stash
   Pop; Tags ... in the Tag menu, a list of every tag with whether origin has it, to show, push or
   remove; Mark for Diff and Diff with in the commit menu; `change:` in the search; and `l` in the
   blame, the history of the lines.

Not needed by the audiences above: submodule and LFS commands, bisect, patches, and pull requests
through the services' APIs.

One surprise to decide on: **Add Tag pushes the tag** whenever the commit's branch has a remote
(`AugmentedService.cs:825-851`), where every other push is asked for. *Decided (2026-10-07):* the
dialog shows it, as a *Push to origin* box that can be unticked, ticked as it starts, as Create
Branch's *Publish* is, since nothing else in gmd pushes a tag later.

### 3. Features that could go, or move

Little is unused. What costs is the length and depth of the menus, which every new user reads:

- The branch menu has 21 items, up to 25, and the commit menu 17, six of them submenus
  (`BranchMenu.cs:165-343`, `CommitMenu.cs:38-117`).
- *Pull All Branches* and *Push All Branches* are in four places: the repo menu, the branch menu,
  *Branches* in the commit menu, and the ▲ and ▼ menus.
- The whole repo menu is nested at the bottom of both the branch menu and the commit menu.
- *Branches* in the commit menu repeats the menu of every shown branch, so *Diff Branch to* is four
  levels down.
- *Rebase* is a submenu holding one item, *Squash ...*.

Candidates to take out or move:

- The duplicates above: Pull All and Push All out of the branch menu, *Squash ...* straight into the
  commit menu, and *Branches* cut down to showing and hiding.
- `g` for the branch color and `<=` / `=>` for the branch order, to the menu only. Orders that
  contradict each other can hang `Sorter.Sort` (`MODERNIZATION.md`).
- *Clean Working Folder* (`RepoMenu.cs:89`), which is `git clean -fxd` and so deletes the ignored
  files as well, `.env` and `node_modules` among them, at the top level of the repo menu beside the
  everyday items. It asks first, but it is rarely what anyone wants; *Discard All Changes* is.
- The `5` key, *Set Commit Branch Manually* (`RepoViewInput.cs:121`): undocumented, and as easy to
  hit as `0`, which was taken out of release builds for that.
- The `*` and `$` search words, for the ambiguous and the manually set branches. *Ambiguous* in Show
  Branch covers the first.

Worth keeping, although few will use them: the shared branch structure (the team's version of the
idea), the items that only appear when they apply (*Restore origin/...*, *Discard Changes in Binary
Files*), spell checking, and the worktrees dialog.

### 4. The README and the help

**`README.md`** is accurate, but:

- It lists the features before it shows how to read the graph, which is the one thing that is new.
- It says nothing of the limits: one remote, a commit takes every change, a dark terminal is
  needed, and at most 30,000 commits are read (`AugmentedService.cs:15`).
- Half of it is for developers.
- It does not say what gmd stores and sends, or how to report a problem.
- The animation runs 46 seconds, with nothing saying which key is pressed.

**`gmd/doc/help.md`**:

- It is 494 lines in a fixed 80×30 box (`HelpDlg.cs`) with no contents, search or jump: about 18
  screens, always read from the top.
- It starts with the keys before it explains what is on the screen, and the top bar is explained
  nowhere in one place.
- Errors: `╂┸` for a synced local and remote branch, which is drawn `┣─┺`; the menu names above;
  *Merge to* for `Shift-E`, which the menu of another branch calls *Merge from*; a "(see below)"
  that points above; and "Undo" for `u` in the diff, which is *Discard Changes*.
- Keys left out: Ctrl-D in the log; Space, PgUp, PgDn, Home and End; and right-click in the diff,
  blame and resolver.
- A force push on origin takes more of the branch section than anything else, for the rarest case.

Both are reworked, see proposal D.1.

### 5. Other questions worth asking

- **How often is the inference wrong?** `InferenceDumpTest` dumps what it decided for every commit,
  and how often the reflog agrees. Run over a few public repositories of each workflow, the numbers
  would say how far to trust the colors, and the README could give them.
- **Does gmd work in the terminals people have?** Light themes (above), Windows Terminal, fonts
  with no `✦`, `Ϙ` or `ß`, and 80 columns (the worktrees dialog needs about that).
- **Can people install it the way they install everything else?** There is the install script and
  the Windows installer, but no Homebrew, Scoop, winget or AUR package, and no build for Intel Macs.
- **What happens when gmd crashes?** It logs the exception and exits without a word
  (`ExceptionHandling.cs:60-110`, where the dialog for it is commented out). The About box has no
  link to the project, and nothing says how to report a problem or where the log is.
- **What does gmd write, and what does it send?** `.gmdconfig` in each repository's git folder;
  `refs/gmd-metadata-key-value/data` in every repository with a reflog, pushed only when the shared
  branch structure is on; `~/.gmdconfig` and `~/gmd.log`; a request to GitHub's releases API every
  hour unless that is turned off; and the tags of Add Tag, above. There is no telemetry. People ask
  this before they run a tool on their work, and the README should answer it.
- **Can others contribute?** Building needs a preview .NET 11 SDK, for the C# 15 unions, until .NET
  11 is released (`UPGRADING.md`).
- **Can it be found?** "gmd" is hard to search for. A tagline, the repository's GitHub topics and the
  lists of terminal tools are how people come across a tool like this.

### Proposals, ranked

**A. First impression**, which decides whether someone new stays:

1. **No git prompt on gmd's screen.** At the least, run git with `GIT_TERMINAL_PROMPT=0` and with no
   terminal for ssh to open, and when a fetch or a push fails for want of a password or a
   passphrase, say what to do: `ssh-add`, or a credential helper. Better, ask for it: gmd as
   `GIT_ASKPASS` and `SSH_ASKPASS` (with `SSH_ASKPASS_REQUIRE=force`), answering with a dialog.
   *The first half is done (2026-10-07):* git gets `GIT_TERMINAL_PROMPT=0` and a closed stdin, and
   ssh `SSH_ASKPASS_REQUIRE=force` with gmd itself as `SSH_ASKPASS` (`Cmd.NeverAskOnTheTerminal`,
   `Askpass`), unless the user has an askpass of their own. gmd answers nothing yet, so the command
   fails, and `LoginError` says what to do: the key to `ssh-add`, the host to trust, a credential
   helper to set up. The fetch says it on the status line, a push or a pull in its error box.
   *The second half is done (2026-10-07):* the askpass asks the gmd that ran the git, over a named
   pipe opened for the user only and a token in git's environment (`AskpassServer`), which shows a
   dialog (`LoginDlg`): the passphrase or password typed hidden, the user name shown, a host to trust
   by Yes or No, No the default. Not `GIT_ASKPASS`: git asks `SSH_ASKPASS` when it has none, and
   setting it would override a user's `core.askPass`. Only what the user asked for asks: the fetch in
   the background (`Askpass.NeverAsk`) says on the status line that `r` fetches and asks, and the
   metadata sync beside a fetch or a push never asks, so a command asks once. Cancel is said on the
   status line, and the command's later questions are not asked.
2. **Say what is hidden.** On the first open of a repository, once, on the status line: "Showing
   main and dev. 29 more branches are hidden: ⇧→ shows one, or Enter on ┣╮." Perhaps the count in
   the top bar for good. *Done (2026-10-07), once per user rather than per repository:* the first
   repository with hidden branches says "Showing main and dev; 29 other branches are hidden: ⇧→
   shows one, as does a click on a dark ╮ or ╯" for fifteen seconds, a tip being read rather than
   glanced at (`HiddenBranchesTip`, `IStatusLine.Tip`, `Config.IsHiddenBranchesTold`). The idea is
   learned once, and a tip on every first open would cover the key hints each time. The count in
   the top bar is left out.
3. **Say something when gmd crashes:** once the screen is given back, a line with the path of the
   log and where to report it; and the project's link in About. *Done (2026-10-07):* what failed,
   the log's path and that the next start begins it anew, and the issues link, on stderr once the
   terminal is given back (`Program.CrashMessage`), with exit code 1; About lists the project, the
   issues and the log.
4. **One name for one menu** (Show Branch), one way of writing its key, and merge items that name
   both branches: *Merge dev into main*, *Merge main into dev*. *The menu is done (2026-10-07):*
   `Shift-→` opens *Show Branch (type to find)*, the submenu of the branch menus is *Show Branch*,
   and menus, reasons and help write the key `Shift-→`. The key-hint line keeps `⇧→`, as it writes
   every shifted key (`⇧p`, `⇧e`). *Show/Hide Branch*, what Enter or a click on a ╮ or ╯ opens when
   several branches meet there, keeps its name: it is a list of its own, of the branches at that
   commit to show and those to hide. *The merge items are done too (2026-10-07):* in the menu of
   dev, while main is current, `e` is *Merge dev into main* and `Shift-E` *Merge main into dev*; on
   the current branch they are *Merge into main* and *Merge main into*, lists whose pick completes
   the title; and the key-hint line says `e merge into main` (`⇧e merge main into`) to match.

**B. Expected features:**

1. The file checklist in the commit dialog (Tier 4, item 1, below). *Done (2026-10-07):* the
   dialog lists the changed files under the message, all ticked, `Space` unticking one and `a` all,
   and the heading counts what is ticked ("Commit 2 of 3 changes"). With every file ticked the
   commit is as it always was; with some unticked only the ticked paths are staged and committed
   (`CommitFilesAsync`, the paths given to git in a file and literal), and the others are left as
   they are. No list while a merge or the like is in progress, which git commits whole.
2. Copy Commit Id and Copy Commit Message in the commit menu, with a key. *Done (2026-10-07):*
   `i` and `Shift-I`, and the commit menu's items, each saying on the status line what it copied;
   the uncommitted row says it is no commit yet. Blame copies its line's commit with the same two
   keys, so its gutter detail moved from `i` to `g`, and `c` copies nothing there any more, where
   in the log it commits (one of the letters the usability review found meaning two things).
3. Reword, fix up and drop, for the commits not yet pushed. *Done (2026-10-07):* reword and fix up
   are one item, **Amend <id> ...** in the commit menu of an older commit, the commit dialog with
   its message and the changed files, none ticked; **Drop <id>** is under Undo, beside Revert
   Commit, and asks first. Both are git's own rebase (an `amend!` commit folded in with
   `--autosquash`, and `rebase --onto`), so a conflict stops it as any rebase, for the resolver
   and Continue or Abort, and Undo takes each back as one change. Refused, with the reason, for a
   merge or past one, which the rebase would flatten, and with another branch or a tag on the
   commit or after it, which would keep the old commits; the server asks git the same before it
   rewrites. The `a` key still amends the last commit only. Squash keeps its reset and
   cherry-pick, which stops half done on a conflict: moving it onto the same rebase is left for
   later.
4. Light themes, `NO_COLOR`, and branch colors that do not lean on red against green, with the
   Terminal.Gui 2.x port.
5. A second remote, read only at first: fetch `upstream` and show its branches. *Postponed
   (2026-10-08), until the need is met in use:* the case that matters is the fork workflow, and
   what is known of it is in `MODERNIZATION.md` (deferred).

**C. Polish:**

1. The year in the dates. `26-03-10` (`RepoWriter.cs:355`) reads as 26 March in much of the world;
   `2026-03-10`, or a relative time for the recent ones, does not. *Done (2026-10-07):* the year in
   full in the log, the blame gutter, Recover Lost Commits and Restore Deleted Branch; the subject
   column gives up the two columns, and a narrow log still shows the date alone.
2. A help that is easier to get around: wider on a wide terminal, a contents to jump from, and
   opening at the part about the view it was opened from. *Done (2026-10-07), taller rather than
   wider:* the box is as tall as the terminal, `m` lists the sections to jump to and `]` and `[`
   step through them, the title saying so, and the diff, blame and conflict views open it at the
   part about them. It is no wider, since the text is written to 77 columns: a wider box would be
   blank space, and rewrapping the text would break its tables and pictures, for lines that read
   worse the longer they get.
3. A key-hint line in the diff and blame views too. *Done (2026-10-07), the resolver too:* each
   view's keys on its bottom row, `Esc close` early on, as the log has them and turned off with
   them: the diff's `c commit` and `u discard` for the uncommitted changes and `Enter resolve` for a
   conflict, blame's `p previous` for a line with an older version, and the resolver's moves and
   whole-file keys, below its result pane, which names what 1 to 4 and 0 take.
4. The resolver's status line naming the sides: "press 1 (HEAD), 2 (dev), 3, 4 or 0". *Done
   (2026-10-07):* "Conflict 1 is not resolved yet — press 1 for HEAD, 2 for dev, 3 or 4 for both,
   0 for neither", the sides in the colors of their column titles.
5. The menu trims of part 3. *The duplicates are done (2026-10-07):* Pull All and Push All are in
   the repo menu, the ▲ and ▼ menus and on `Shift-U` and `Shift-P`, and no longer in the branch menu
   or *Branches*; in an open branch menu `Shift-P` and `Shift-U` now pick that branch's own Push and
   Pull, as a menu picks the item showing a letter in either case. *Squash ...* is in the commit
   menu itself, where the *Rebase* sub menu held it alone. *Branches* keeps the menu of each shown
   branch, since `←` `→` reach only the branches on the cursor's row. *The rare items too
   (2026-10-07):* *Clean Working Folder* is *Discard All Changes and Ignored Files* under Undo,
   beside *Discard All Changes*, which differs from it by the ignored files only. It is kept for
   making the folder as a fresh clone has it, to check that no leftover ignored file makes a build
   or a test pass, and its question lists what git's dry run says it deletes, so a `.env` meant to
   be kept is seen before Yes. The `5` key is gone, *Set Commit Branch Manually* stays in the
   branch menu. `g` stays, since automatic branch colors often collide and changing one is how two
   branches are told apart; `<=` / `=>` never had keys. *And the search words (2026-10-07):* `*`
   and `$` are searched for as text now; *Ambiguous* in Show Branch lists the ambiguous branches,
   and `Φ` in the graph marks a commit whose branch was set by hand.

**D. Reach:**

1. The README and the help reworked: what is on the screen first, then the everyday tasks, then
   the reference; who gmd is for and its limits; what it stores and sends. *Done (2026-10-07):*
   the help starts with its contents and a tour of the screen (the graph with and without a hidden
   branch, the highlighted branch, the top bar item by item), then Everyday Tasks, then the
   reference, with the errors of part 4 fixed. The README sets `git log --graph` of the demo
   repository beside gmd's first screen of it, and adds Reading the Graph, Is Gmd for You? with
   the known limits, Gmd and AI Coding Agents, the first five minutes, and What Gmd Stores and
   Sends; its development half is `CONTRIBUTING.md` now.
2. Homebrew, Scoop or winget, and AUR packages, and a build for Intel Macs.
3. Issue templates, linked from About and the README.
4. Captions in the animation, saying which key is pressed.
5. The inference's agreement with the reflog, measured on public repositories and published.

### Implementation steps

The proposals above in the order they are to be done: one step at a time, each on a branch of its
own merged into dev, and ticked here when it lands. A step may be adjusted or skipped before it is
started. The details are in the proposals and parts above; effort is S, M or L.

First impression:

- [x] 1. **No git prompt on the screen** (A.1): git runs with nothing to ask on, and a failed login
  says what to do (`ssh-add`, a credential helper). A test with the ssh stand-in. M
- [x] 2. **A word on a crash** (A.3): the log's path and the issues link once the terminal is given
  back, and the project's link in About. S
- [x] 3. **Say what is hidden** (A.2): once per repository, on the status line, how many branches
  are hidden and how to show one. S
- [x] 4. **One name for the Show Branch menu** (A.4): the same title everywhere, and `Shift-→`
  written one way. S
- [x] 5. **Merge items that name both branches** (A.4): *Merge dev into main*. S

Quick wins:

- [x] 6. **Copy the commit id and message** (B.2): in the commit menu, with a key. S
- [x] 7. **Add Tag asks before it pushes** (part 2), as every other push does. S
- [x] 8. **The year in the dates** (C.1): `2026-03-10`, in the log, blame and the lists. M (every
  snapshot with a date changes)
- [x] 9. **The resolver names the sides** (C.4): "press 1 (HEAD), 2 (dev), 3, 4 or 0". S

Shorter menus (C.5, part 3):

- [x] 10. **No duplicates:** Pull All and Push All out of the branch menu and out of *Branches*,
  and *Squash ...* straight into the commit menu. *Branches* keeps the menu of each shown branch,
  the one way by keyboard to a branch that does not cross the cursor's row. S
- [x] 11. **Rare and risky items moved:** *Clean Working Folder* under Undo as *Discard All Changes
  and Ignored Files*, its question listing what it deletes; the `5` key gone; `g` kept, since the
  branch colors often collide; `<=` / `=>` had no keys already; and the `Sorter.Sort` hang fixed. S
- [x] 12. **The `*` and `$` search words** dropped. S

Getting around:

- [x] 13. **Key hints in the diff and blame views** (C.3), and in the conflict resolver. M
- [x] 14. **A help that is easier to get around** (C.2): wider on a wide terminal, a contents to
  jump from, opening at the view's own section. M

Bigger features:

- [x] 15. **Choose what to commit** (B.1): a file checklist in the commit dialog, every file ticked.
  L
- [x] 16. **Reword, fix up and drop** commits not yet pushed (B.3): Amend and Drop of any commit
  not pushed yet. L
- [x] 17. **Ask for the password in a dialog** (A.1, the better half): gmd as `SSH_ASKPASS`, which
  git asks too, asking in a dialog of the gmd that started git. What is left, Git Credential
  Manager included, is in `MODERNIZATION.md` (open issues, Product). M
- [x] 18. **The smaller gaps** (part 2, item 7), each to pick or skip: stash apply, a tag list and
  push, a diff of any two commits, search of the changes (`-S`), line history (`log -L`); a merge
  tool in the terminal skipped. S to M each
- [ ] 19. **A second remote, read only** (B.5): fetch `upstream` and show its branches. L
  *Postponed (2026-10-08)*, to understand the use from experience first; see `MODERNIZATION.md`
  (deferred).

Reach:

- [ ] 20. **Issue templates** (D.3), linked from the README. S
- [ ] 21. **Captions in the animation** (D.4). S
- [ ] 22. **The inference measured** (D.5) on public repositories of each workflow, and the numbers
  in the README. M
- [ ] 23. **The terminals people have** (part 5): 80 columns, Windows Terminal, fonts with no `✦`,
  `Ϙ` or `ß`. M
- [ ] 24. **Packages** (D.2): Homebrew, Scoop or winget, AUR, and an Intel Mac build. Needs the
  author's accounts. M

Waiting for the Terminal.Gui 2.x port:

- [ ] 25. **Light themes, `NO_COLOR` and colors that do not lean on red against green** (B.4). L

---

## What already works well

- **Choosing which branches are shown** is unique, and it is the reason to use gmd. The ┣╮ / ┣╯
  markers show where more is hidden and where to open it.
- **The newer screens are the model to follow:** the conflict resolver, the worktrees dialog, pull
  all. Their messages explain why and name the next step, and the resolver's status line tells you
  which keys to press ("press 1, 2, 3, 4 or 0"). Most of the proposals below spread that style to
  the older parts.
- **Every command is in a menu, and the menu shows its key,** so the shortcuts can be learned by
  using the menus. The mouse works too, and every item in the top bar can be clicked.

---

## Findings

### 1. Safety: it was too easy to do something costly by accident (fixed)

This matters most. One lost piece of work costs more trust than many small annoyances. As found,
all of it fixed since (Tier 1 below):

- **Keys the side views did not handle fell through to the log view underneath.** The diff, blame
  and conflict views were non-modal toplevels, so an unregistered key reached `RepoViewInput`:
  - `Shift-U` in a diff pulled every branch, and `Shift-P` in blame pushed every branch.
  - `p`, `e` and `E` in a diff pushed or merged.
  - The menus of those views write their shortcuts in upper case, which invited exactly those keys.
  - In the conflict resolver, `c` reached the diff view below it and closed the resolver without the
    unsaved-decisions question.
  - A letter typed in a diff opened from the commit dialog went into the commit message.
- **Esc quit the app without asking** (`RepoViewInput.cs:74`). Everywhere else Esc means "close" or
  "back", so one Esc too many exited. The top bar's `X` sits right next to `?`.
- **Unsafe default buttons:**
  - In *Binary Files Detected*, Enter meant *Undo*, which discards the binary changes
    (`CommitCommands.cs:644`).
  - In the resolver's *Unsaved Decisions*, Esc meant *Discard* (`ConflictView.cs:536`).
- **Irreversible discards ran without a question:**
  - discard all changes (`reset --hard` plus `clean -fd`);
  - discard a file (a new file is deleted);
  - drop a stash;
  - remove a tag, which deletes it on origin too.

  Yet the milder *Clean Working Folder* asked first.
- **`p` force-pushed.** For any branch with a remote it ran `git push --force-with-lease`
  (`BranchPushPullCommands.cs:76`), not only after the user picked *Force Push* in the warning. The
  call was one block too far out; it came in with 09534e6.
- **Single clicks acted on the remote:**
  - Clicking ▲ or ▼ in the top bar pushed or pulled all shown branches at once.
  - A middle click merged a branch, and on Linux the middle button is the habitual paste.
- **Dead ends at start-up:**
  - A failed clone or init left a blank screen where no key but Esc worked (`MainView.cs:271-298`).
  - A click anywhere outside the start menu quit gmd.

### 2. Discoverability: can a new user find things?

- **Nothing tells a newcomer which keys to press.** The log, diff and blame views show no key hints.
  The only cue is `?` in the top bar.
- **The letter shown next to a menu item does nothing inside the menu.**
  - A menu shows `B` beside *Create Branch*, but the menu is modal and knows only arrows, Enter and
    Esc (`Menu.cs:175-184`).
  - Long branch lists (*Show/Open Branch*) have no type-to-find either, so showing one hidden branch
    takes 5–10 key presses.
- **Stopped operations are hard to see and hard to get out of:**
  - No key opens the Repo menu, which is where *Continue*, *Skip* and *Abort* live during a stopped
    merge or rebase.
  - The conflict is first reported in a red "Error !" box with no *Resolve* button.
  - Nothing on screen goes on saying that a merge or rebase is in progress.
- **Disabled menu items never say why.** For example, *Diff Branch to* and *Merge* turn grey whenever
  there are uncommitted changes.
- **Keys that are not documented:**
  - `0` opens a developer Unicode dialog, and is easy to hit.
  - `1` opens help, `5` sets a commit's branch by hand, and `y` goes to the current branch.
- **Starting outside a repository is unexplained.** A "Recent Repos" menu opens with no word on why.
  With no recent repositories it starts with a bare separator.

### 3. Consistency: the same key or word should mean the same thing

- **Letters change meaning from view to view:**
  - `c`: commit (log, diff) or copy a SHA (blame).
  - `s`: switch branch, scroll to a file, or save.
  - `r`: refresh, but *remove* in the worktrees dialog.
  - `p`: push, blame the previous version, or go to the previous conflict.
  - `u`: pull, open the undo menu, or un-decide.
- **The branch menu shows *Push `P`* and *Pull/Update `U`* for its own branch,** but the keys `p` and
  `u` act on the current branch.
- **Where the mouse rests silently changes what keys act on.** Hovering over the graph re-targets
  `s e h d m b`. The target is shown only as an unlabelled `(branch)` at the right of the top bar.
- **Names mix concepts:**
  - Slash names: Pull/Update, Switch/Checkout, Undo/Restore, Search/Filter, Refresh/Reload,
    Show/Open.
  - Three different "undo"s: *Undo Commit* is a revert, *Uncommit* is a reset, and *Undo/Restore*
    discards.
  - "..." appears on items that open no dialog (*Toggle Commit Details ...*) and is missing on some
    that do (*Stash Changes*).
- **The help and the README write keys in upper case** (`M`, `D`, `C`), but only lower case works,
  and some upper-case letters do something else (`P` is push all).
- **Several commands demand a clean working tree even though git doesn't:**
  - Push is refused with "Commit changes before pushing".
  - *Merge* and *Diff Branch to* turn grey with no reason given.
- **The key decides what can be done, not the menu.** *Amend* is disabled in the menu with a clean
  tree, but the `a` key amends the message anyway.

### 4. Feedback: does gmd say what happened?

- **Silent no-ops:**
  - `c` with nothing to commit;
  - `s` or `e` with no branch highlighted;
  - Enter on a branch away from the current row;
  - Ctrl-C with nothing selected.
- **Non-errors look like errors.** "No local changes to push" and "No remote changes on current
  branch to pull" come up in a red "Error !" box.
- **Keys pressed while git runs are dropped without a sign** (`Progress.cs:62`). The only feedback
  is a marquee shown after 800 ms, and it covers the start of the repo path.
- **A failed background fetch is only logged** (`RepoView.cs:519-523`). When offline, or when
  authentication fails, the remote state quietly goes stale.
- **Hidden branches hide news.** ▲ and ▼ count only shown branches, so commits arriving on a hidden
  branch leave no sign.
- **Git errors are shown raw.** A failed command shows git's stderr and a `Command: git …` line
  inside the gmd message.

### 5. Workflow fit, compared with lazygit / tig / gitui

These are choices for the author, not defects.

- **There is no choice of what to commit.** A commit is always `git add .` plus `commit -a`
  (`CommitService.cs:37-46`).
  - Viewing the uncommitted diff runs `git add .` and then `git reset` (`DiffService.cs:64-91`), so
    merely looking at it wipes any staging done with the git CLI. *Fixed (2026-09-25):* it stages
    into a copy of the index instead (`GIT_INDEX_FILE`), so the index is never written.
- **Other gaps:**
  - No undo for showing or hiding branches.
  - Search looks at the subject only: not the message body, and not file paths.
  - Nothing opens a branch, commit or pull request in the browser.
  - Only `origin` is supported.
  - `pull` offers no choice between merge and rebase. *Fixed (2026-09-25),* see Tier 4 item 6:
    it was worse than no choice, since git refuses a diverged pull when `pull.rebase` is unset.

---

## Proposals, ranked

### Tier 1: safety (done, 2026-09-25)

Each has a regression test, and `MODERNIZATION.md` records them under "Bugs fixed".

1. The side views are modal, so no key falls through to the log. Their letters work in both cases,
   as their menus write them.
2. Esc in the log view asks "Quit gmd?" with **Yes as the default**, and so does the top bar's `X`.
   Esc then Enter quits quickly, and an accidental double Esc cancels. `q` still quits at once.
3. Safe defaults: *Cancel* in *Binary Files Detected* and in *Delete Branch* (whose box for origin
   is checked when there is one, so Enter, Enter deleted there too, a push; 2026-10-07), and Esc
   means *Stay* in *Unsaved Decisions*.
4. Discard all, discard a file, drop a stash and remove a tag ask first. The default is No, and the
   question says what will be lost.
5. `p` is a plain push. It force-pushes only when *Force Push* is chosen.
6. ▲ and ▼ open a small menu (current branch or all branches) instead of acting at once. A middle
   click asks before it merges.
7. A failed clone or init returns to the start menu, and a click outside that menu no longer quits.

### Tier 2: learnability quick wins

1. **A key-hint line at the bottom of the log view** that follows what the cursor is on: a commit, a
   highlighted branch, the uncommitted row, or an operation in progress. For example:
   `m menu  d diff  c commit  ←→ branch  ⇧→ show branch  f search  ? help`. It can be turned off in
   Config. This is the biggest single gain for someone new. *Done (2026-09-25):* `KeyHints`, keys
   written as typed (`P` is Shift-P), with `c continue` during a rebase and `d resolve` on
   conflicts. The diff and blame views have no such line yet; the conflict resolver's status line
   already names its keys. *Since (2026-10-07):* the diff, blame and conflict views have lines of
   their own, see the product review's step 13.
2. **In a menu, pressing the letter it shows runs that item,** and typing in a long branch list
   narrows it. *The letters are done (2026-09-25):* `MenuShortcuts`, both cases of a letter unless
   the menu also shows it as `Shift-`, and a greyed out item's key does nothing. *Type-to-find is
   done too:* typing in the Open Branch menu or its sub menus opens Find Branch (`BranchFinder`,
   `FindBranchDlg`), a list that narrows as the name is typed.
3. **A short non-modal status message** for no-ops and results: "Nothing to commit", "Highlight a
   branch with ← → first", "Pushed main", "Fetch failed: offline". It replaces the red box for
   anything that is not an error, and fades after a few seconds. *Done (2026-09-25):* on the
   key-hint line for five seconds (over the log's bottom row with hints off), via `IStatusLine` and
   `Notice`: `c`/`a`/`t` with nothing to act on, `s`/`e`/`E` with no branch highlighted, Ctrl-C with
   nothing selected, the push and pull guards and results, and a fetch that starts failing.
   *Progress too (2026-09-29):* a commit, push or pull says what it is doing while git works, in
   cyan ("Committing to 'main'..."), which what it did then replaces ("Committed to 'main'").
4. **An operation in progress is shown in the top bar,** e.g. `MERGING · 2 conflicts`.
   - Clicking it offers Continue, Skip and Abort, and a key opens the Repo menu.
   - A conflict is reported in an info box with a *Resolve Conflicts* button.

   *Done (2026-09-25):* the operation right after ' Gmd ' (`Merging: 1 conflict` in red, `…: commit
   to finish` in yellow), a click on it opening Resolve Conflicts / Continue / Skip / Abort,
   `Shift-M` for the repo menu (hinted as `M abort…` while an operation lasts), and a
   `ConflictError` from git reported by `RepoCommands.ShowConflicts`.
5. **A disabled item says why** when chosen, or in the hint line. *Done (2026-09-25):* a click on a
   greyed out item, or its key, puts the reason on the status line (`MenuItem.WhyNot`, `Why`), for
   the branch, commit, repo, push and pull menus. The Amend item is enabled as the `a` key is.
6. **Housekeeping:** *done (2026-09-25).*
   - Take the `0` developer key out of release builds, and document `y`.
   - With no recent repositories, the start menu says so. It is titled "Open a Repository" too,
     which says why it is there.

### Tier 3: consistency

1. **One key vocabulary for every view:**
   - Esc = back, `q` = close, `m` = menu, Enter = open, `?` = help, `/` = search (as well as `f`),
     Ctrl-C = copy.
   - A command letter is not reused for something else in another view.

   *Done (2026-09-25), as far as the shared keys go:* `?` and F1 open the help in the diff, blame
   and resolver too, `/` searches the log, and the diff's second refresh key `d` and the log's
   undocumented `1` are gone. Letters that mean different things in different views stay: with the
   side views modal, none of them can reach a command of another view any more.
2. **The branch menu's key labels match what the keys do.** Either `p` and `u` act on the highlighted
   branch, like `s e h b d` do, or the label goes. *Done (2026-09-25):* they act on the highlighted
   branch, by the same rules as the menu's Push and Pull (`CanPushBranch`, `CanPullBranch`).
3. **Plain names:**
   - *Discard Changes in File…*, *Discard All Changes…*
   - *Revert Commit*, *Uncommit Last Commit (keep changes)*, *Uncommit to X*
   - *Switch to Branch*, *Search*, *Refresh*
   - "..." only when a dialog follows.

   *Done (2026-09-25):* no slash names left (Switch to Branch, Pull, Pull All Branches, Show
   Branch, Search, Refresh, Clean Working Folder, Open, Clone or Init Repo); Discard / Revert
   Commit / Uncommit Last Commit / Uncommit X and Newer for the three kinds of undo; and "..." on
   the items that ask for something before they run, not on those that open a view, toggle or
   only confirm.
4. **Keys act on the keyboard highlight,** not on wherever the mouse happens to rest. At least,
   label the target in the top bar (`▸ dev`). *The label is done (2026-09-25),* on the key-hint line,
   which starts with the highlighted branch's name (`dev:  s switch …`); the mouse still moves it.
5. **Don't require a clean tree to push.** For merge and switch, offer "stash, do it, pop".
   *Done (2026-09-25):* push only waits for a merge or rebase in progress, and says the changes
   stay local; a switch git refuses over the changes offers Stash and Switch. Merge and pull still
   ask for a clean tree.
6. **Write keys in the help and the README the way they are pressed.** *Done (2026-09-25):* menus,
   help and README write `c` for the c key and `Shift-P` for P; the key-hint line writes `⇧p`.
   *Since (2026-10-07):* the menus write `⇧p` as well, and so do the status messages and the
   reasons a greyed out item gives, so what is drawn writes a key one way; the help and the
   README keep `Shift-P` in their text, the help saying once that ⇧ is Shift.

### Tier 4: bigger bets, to decide later

1. **A file checklist in the commit dialog,** every file ticked by default. This fits gmd's model
   (no staging area to manage) better than lazygit-style staging does. Also, stop the uncommitted
   diff from resetting the index. *The index half is done (2026-09-25):* the diff stages into a copy
   of the index, which also lets it show new files during a merge. *The checklist too (2026-10-07),*
   see the product review's proposal B.1.
2. **Undo for showing and hiding branches,** e.g. Backspace brings back the previous set. It is
   cheap, and it makes the README's "can be undone at any time" literally true. *Done
   (2026-09-25):* `Backspace` steps back through the shows and hides the user asked for
   (`ShownHistory`), and says what it undid; *Undo …* under Branches names it. The key-hint line
   offered `Bksp undo show` or `undo hide` too, and no longer does (2026-09-28).
3. **Open the branch, the commit or a new pull request in the browser.** *Done (2026-09-25):*
   *Open in Browser* and *Create Pull Request in Browser* in the branch menu, *Open Commit in
   Browser* and *Open Repository in Browser*, for GitHub, GitLab, Bitbucket, Azure DevOps and Gitea
   (`WebLinks`). A pull request goes into the branch the branch was made from, which gmd knows and
   the service does not. `$BROWSER` comes first (`BrowserService`), and where there is no browser,
   e.g. over ssh, the link is copied instead.
4. **Search message bodies and file paths,** with next and previous match. *Done (2026-09-25):*
   a search matches the whole message; `file:<path>` matches the commits that changed such a file,
   asked of git once typing pauses (`SearchTerms`, `GetIdsChangingFilesAsync`); and after a commit
   is picked, `n` and `Shift-N` step through the other matches in the log (`SearchMatches`). A
   picked match is shown as any branch is, so Backspace undoes it, which it did not before.
5. **A count of the incoming commits on hidden branches.** *Done (2026-09-25):* ▽ in the top bar
   counts the commits pushed to hidden remote branches since each was last shown, remembered per
   repository (`HiddenNews`, `RepoConfig.SeenTips`). A click lists them, as *New Commits* in Show
   Branch does, and *Mark All as Seen* clears what is not followed. *Since (2026-10-06):* ✦ rather
   than ▽, which was easily taken for ▼, and it counts branches rather than commits, the question
   being which branches: new ones too, with no commits of their own yet, and local ones never
   pushed, e.g. the branch Claude Code makes in a worktree of its own. A branch made elsewhere is
   told of rather than shown, since showing each one would fill the log of a busy repository; the
   one exception is the current branch, shown once it becomes current (`CurrentBranchShown`). And
   listed is seen, so *Mark All as Seen* is gone: a count that stayed until each branch was shown
   or marked would never go in a busy repository, and what the user wanted was to know.
   ╯ was considered instead of ✦ (2026-10-07) and turned down: in the graph and the branch menus
   it means a hidden branch branching out at a commit, a fact of the history that a log shows many
   of, so `╯3` would read as three hidden branches rather than as something new to look at, and in
   Show Branch it would mean both, a few lines apart. Alone in the top bar a box-drawing glyph also
   looks like a broken frame corner, where ✦ is as solid as the markers beside it.
6. **Merge or rebase on pull,** asked once and remembered. *Done (2026-09-25):* with git's default
   config, pulling a diverged branch failed outright ("Need to specify how to reconcile divergent
   branches", under a dozen lines of hints). gmd now asks, Merge or Rebase, when git has no
   `pull.rebase`, `pull.ff` or `branch.<name>.rebase`, and saves the answer as `pull.rebase`
   (`merges` for a rebase, which keeps local merges), so git on the command line does the same.
7. **Theme:**
   - Honour `NO_COLOR` and light terminals, with no forced black background.
   - More branch colors, with the Terminal.Gui 2.x port.
   - Mark the current row in the graph column too.

   *The last is done (2026-09-25):* the current row's highlight covers the graph as well, so the
   commit's node is found at a glance. The first two wait for the Terminal.Gui 2.x port, recorded
   in MODERNIZATION.md with the reasons.

8. **A way back from what moves a branch.** *Done (2026-10-06):* the reflog, which git keeps for
   every move of a branch, is what Safety's "a way back" for the cheap mistakes needed, and what
   most users never reach on the command line. *Undo* takes back the last change of a branch,
   named after it (a commit, an amend, a merge, a pull, a rebase, a squash, a reset), losing
   nothing uncommitted, and Undo again redoes it; it asks nothing, being undoable. *Recover Lost
   Commits* lists what a reset, a rebase, an amend or a deleted branch left behind, and brings a
   line back with a branch. *Restore Deleted Branch* (2026-10-07) brings back a branch gmd deleted,
   whose reflog git deletes with it, from gmd's own record, origin's side too, asking first since
   that is a push; the status line says so after every delete. A force push on origin is said once
   it is fetched, and a pull then moves the branch's own commits onto the new version rather than
   merging the two; *Restore origin* puts origin back, asking first, since that is a force push too. Left for later: a key for Undo
   and a key hint, and a way back from Discard All Changes and Drop Stash, which the reflog does not
   record (see MODERNIZATION.md).

---

## Small bugs found along the way

All fixed (2026-09-25), each with a test where one could reach it:

- `AboutDlg.cs`: `{latest.Txt}` was missing its `()`, so it printed a delegate type name, and with
  no latest version known (update checks off) parsing the empty value crashed gmd.
- `BranchPushPullCommands.cs`: Push All said "Commit changes before **pulling**" (it no longer needs
  a clean tree at all).
- `CommitCommands.cs`: compared `id1` with `id1`, so "not on same branch" was never reported and a
  range diff across branches included commits that were not selected.
- `CommitMenu.cs`: checked `c1` twice and never `c2`.
- `FilterDlg.cs`: the mouse-handler map was never filled, so clicking a search result did nothing.
- `ConfigDlg.cs`: said "Removed gmd **to** PATH", after a "Not implemented yet" branch that could
  never run.
- "Commit 1 changes on 'main'", "1 uncommitted changes" and "Amend 0 changes": the plurals.
- `MainView.cs`: start-up errors printed "Error: ..." inside the "Error !" box.
- `help.md`: *Uncommit until X* was documented as `reset --soft X`, but X is uncommitted too; and a
  diverged branch "cannot be pushed", but `p` offers *Force Push*.
