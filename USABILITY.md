# Usability review

A review of gmd's user experience made on 2026-09-24, with ranked proposals. gmd grew one feature at
a time for its author's own use, so this looks at it as someone new would. It covers every key
binding, menu, dialog and message; the screens as the end-to-end snapshots draw them; and the
everyday workflows, compared with lazygit, tig and gitui.

The findings are sorted under five usability principles. Each is a question to ask of any new
feature:

- **Safety**: can a slip of the finger cost work, or change the remote? Mistakes will happen, so the
  costly ones need a guard and the cheap ones a way back.
- **Discoverability**: can someone who has never read the help find the command?
- **Consistency**: does a key or a word mean the same thing everywhere?
- **Feedback**: after every key, does the user know what happened, or why nothing did?
- **Workflow fit**: are the everyday tasks short, compared with the tools people already know?

File references are as of the review. Close items here, or move them to `MODERNIZATION.md`, as
they land. The safety findings (section 1, and Tier 1 of the proposals) are fixed; the rest is
open.

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
    merely looking at it wipes any staging done with the git CLI.
- **Other gaps:**
  - No undo for showing or hiding branches.
  - Search looks at the subject only: not the message body, and not file paths.
  - Nothing opens a branch, commit or pull request in the browser.
  - Only `origin` is supported.
  - `pull` offers no choice between merge and rebase.

---

## Proposals, ranked

### Tier 1: safety (done, 2026-09-25)

Each has a regression test, and `MODERNIZATION.md` records them under "Bugs fixed".

1. The side views are modal, so no key falls through to the log. Their letters work in both cases,
   as their menus write them.
2. Esc in the log view asks "Quit gmd?" with **Yes as the default**, and so does the top bar's `X`.
   Esc then Enter quits quickly, and an accidental double Esc cancels. `q` still quits at once.
3. Safe defaults: *Cancel* in *Binary Files Detected*, and Esc means *Stay* in *Unsaved Decisions*.
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
   already names its keys.
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

### Tier 4: bigger bets, to decide later

1. **A file checklist in the commit dialog,** every file ticked by default. This fits gmd's model
   (no staging area to manage) better than lazygit-style staging does. Also, stop the uncommitted
   diff from resetting the index.
2. **Undo for showing and hiding branches,** e.g. Backspace brings back the previous set. It is
   cheap, and it makes the README's "can be undone at any time" literally true.
3. **Open the branch, the commit or a new pull request in the browser.**
4. **Search message bodies and file paths,** with next and previous match.
5. **A count of the incoming commits on hidden branches.**
6. **Merge or rebase on pull,** asked once and remembered.
7. **Theme:**
   - Honour `NO_COLOR` and light terminals, with no forced black background.
   - More branch colors, with the Terminal.Gui 2.x port.
   - Mark the current row in the graph column too.

---

## Small bugs found along the way

- `AboutDlg.cs:33`: `{latest.Txt}` is missing its `()`, so it prints a delegate type name.
- `BranchPushPullCommands.cs:121`: Push All says "Commit changes before **pulling**".
- `CommitCommands.cs:215`: compares `id1` with `id1`, so "not on same branch" can never be reported.
- ~~`CommitMenu.cs:143`: checks `c1` twice and never checks `c2`.~~ Fixed.
- `FilterDlg.cs:25,176`: the mouse-handler map is never filled, so clicking a search result does
  nothing.
- `ConfigDlg.cs:145`: says "Removed gmd **to** PATH".
- "Commit 1 changes on 'main'": the plural is wrong (`CommitDlg.cs:43`).
- `help.md`:
  - It says *Uncommit until X* is `reset --soft X`, but the code resets to X's parent, so X is
    uncommitted too.
  - ~~It says a diverged branch "cannot be pushed", but `p` offers *Force Push*.~~ Fixed.
