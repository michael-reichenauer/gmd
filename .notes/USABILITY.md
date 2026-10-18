# Usability review

Five reviews of gmd's user experience, each with ranked proposals. gmd grew one feature at a time for
its author's own use, so they look at it as someone new would.

- **The file browser review (2026-10-18)**, first below, looks at the dialog Blame File and Full
  File History pick their file in, which was a workaround.
- **The rebase and squash review (2026-10-17)** looks at gmd for those who rebase,
  squash and squash merge rather than merge: what it does with a history rewritten here or on
  origin, and which rewrites of commits it could offer.
- **The diff and resolver review (2026-10-16)** looks at the two views most of the
  work is read in, how they look and how they are used, and at the way through a conflict.
- **The product review (2026-10-07)** steps back from the keys: whether choosing which
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

## File browser review (2026-10-18)

Blame File and Full File History pick a file of the row's commit in `FileBrowseDlg`, written early
as a workaround over Terminal.Gui's `TreeView`. Tried in the built binary in tmux, on a clone of gmd
itself (617 files).

### Findings

1. **A fixed 50 by 20 box**, whatever the terminal, so deep paths were cut and a tree of a few
   hundred files was a long walk through a small window.
2. **No way to type a name.** The only way to a file was the arrows through its folders, in a repo
   of thousands of files.
3. **Enter on a folder did nothing**, and only `→` opened one, which nothing said: no key hints, and
   a Cancel button but none to pick with.
4. **It did not look like gmd**: `TreeView`'s own `├>` lines, an open folder with no marker at all,
   folders and files in the same color, and its own highlight, on the name alone.
5. **Nothing pointed at the likely file.** It opened on the first folder, though the file wanted is
   most often one the commit changed.

### As done (2026-10-18)

All five: the dialog is now laid out as Find Branch is, a field to type in over a list and a status
line with the keys, as large as the terminal allows (up to 90 by 34). With nothing typed, the list
is the tree (`FileTree`), gmd's own rows (`FileBrowseRows`): `▸` / `▾`, dark guides, folders in
blue, the files the commit changed marked with their letter at the right and their folders with a
`•`, those folders opened and the cursor on the first such file. Typing lists the files whose path
has every word (`FileFinder`, ranked as `BranchFinder` is), the words marked in yellow; clearing it
goes back to the tree as it was. Enter or a click picks a file or opens and closes a folder, `→` /
`←` open, close and go up. The titles are the menu items', and the status line says what the files
are (`617 files of commit 1be33bd1`) and what Enter does where the cursor is.

Open: **the folder browser** (`FolderBrowseDlg`: Init, Clone, Add Worktree and opening a repository,
from the start page or the repo menu) is the same kind of workaround over `TreeView` and looks as
the file browser did; it lists the file system lazily, so it would share the rows rather than
`FileTree`.

---

## Rebase and squash review (2026-10-17)

gmd is made for merge workflows, but most people and agents work on branches, merged with a commit,
squash merged or rebase merged, and gmd can still help while a branch lives. Made by trying each
case in the built binary in tmux, on throwaway repositories with an origin, and a stand-in for
GitHub's squash merge and rebase merge (a second clone that merges and deletes the branch there).

### Findings

1. **Three rules for rewriting pushed commits.** Squash allows them, with no warning and no push;
   Amend (the last commit or an older one), Drop and Uncommit refuse them ("Only a commit not yet
   pushed can be amended"); Rebase and Push onto allows them, asks, and force pushes with a lease,
   but only when the rebase has no conflict: continued after one, nothing pushes
   (`BranchWriteService.RebaseBranchAsync`).
2. **After a squash of pushed commits gmd advised the wrong thing.** The top bar said `▼3, ▲1`, the
   three old commits were drawn `▼` "to pull", and the key hints offered `u pull` and no push. `p`
   said "has remote commits not yet pulled. Pull current remote branch first before pushing, or do
   you want to force push? NOTE: be careful!". `u` asked *Pull Diverged Branch* with Merge the
   default, which would have merged the old commits back beside the squash, every change twice, and
   saved `pull.rebase=false`; Rebase would have replayed the squash onto them, empty, undoing it.
   The same followed a conflicted Rebase and Push onto, and an agent's rewrite on the command line.
   gmd told the mirror case, origin force pushed by someone (`RemoteRewrites`), and not this one.
3. **The squash dialog.** The message joins the old ones with `-----` lines, which go into the commit
   as they are; the range is written `4050decb...80daa352`, git's three dots being the symmetric
   difference; the commit gets the user as author and the time now, where git's own squash keeps
   the first commit's; and nothing says the commits are on origin. *Fixed (2026-10-17)*, but for
   the author and time: the message is the oldest's with the newer subjects listed as '*' items, as
   GitHub writes a squash merge, the item and the heading count the commits, and B's box says the
   force push. The author's email is not read with the log, so the squash cannot keep the author.
4. **A branch squash or rebase merged on origin looks never pushed.** Once a fetch prunes the remote
   branch the pull request deleted, `git branch -vv` says `[origin/feature: gone]`, but the
   augmentation clears the remote name (`Augmenter.AddAugBranches`), so `p` says "'feature' is not
   on origin yet: Push in its branch menu publishes it", which would make the deleted branch again,
   and Amend and Drop take its commits for not pushed. Nothing says its work is in main. The
   worktree list says `unmerged` (a tip with no child, `RepoExtensions.HasUnmergedCommits`) and
   Remove Worktree leaves *Delete branch* unticked, so every agent branch merged by a squash pull
   request looks unsafe to delete.

### Proposals, most value first

- **A. Tell "rewritten here, origin has the old version"**, the mirror of RemoteRewrites, from the
  branch's own reflog: it was where origin is now, and something moved it off that commit. The old
  version drawn dark rather than `▼`, `p` a force push asked first, naming what it replaces, `u`
  never merging it back. *Done (2026-10-17)*, see below.
- **B. One rule for rewriting pushed commits.** Squash, Amend (a reword too), Drop and Rebase of a
  pushed branch when it is the user's to rewrite: not main nor an integration branch
  (`WellKnownBranches`), and the commits replaced on origin their own (author), else the question
  names the others. The dialog says the commits are on origin and has a *Push with force when done*
  box, ticked, as Add Tag's *Push to origin* is. The way back is there already: the lease, Undo,
  Restore origin. Rebase and Push onto then becomes Rebase onto. Answers "reword an old pushed
  commit". *Done (2026-10-17)*, see below.
- **C. Branches merged on origin by a squash or a rebase.** Keep git's `gone`: "deleted on origin",
  `p` asking before publishing it again. Tell merged: a rebase merge by patch id (`git cherry
  origin/main <branch>`, every commit `-`), a squash merge by the branch's whole change as one patch
  among main's commits since the merge base (tried: a `commit-tree` of the branch's tree on the merge
  base, then `git cherry`, gives `-` for the squash). Only for the local branches whose remote branch
  is gone, so a few git calls. Say it in the log and the worktree list, and offer *Delete Merged
  Branches ...*, a ListDlg of the local branches merged any of the three ways, with their worktrees,
  recorded for Restore Deleted Branch: what `gh poi` and `git-delete-squashed` are for. *Done
  (2026-10-17)*, see below.
- **D. Rebase.** `--update-refs`, so that a stack of branches (feature-b on feature-a, as agents
  make) moves along, which gmd's branch hierarchy knows; it also lifts "another branch is on this
  commit or after it" for Amend, Drop and Squash when that branch is local. `--autostash`, as Amend
  of an older commit has, rather than refusing with changes.
- **E. New commands.** Move commits to a new branch (committed on main by mistake: a branch at HEAD,
  and `reset --keep` of main to origin's); reorder (Move Up and Move Down, an interactive rebase of a
  todo gmd writes, which needs `GIT_SEQUENCE_EDITOR` per call, `Cmd` forcing it to `true`); squash
  merge (`git merge --squash`, the commit dialog with the branch's subjects as a list, as GitHub
  writes them, then the offer to delete the branch, which would look unmerged); and, open in the
  product review already, a cherry pick of the selected commits and a reset of a branch to any
  commit. Later, an absorb of the uncommitted changes into the commits that last touched the lines.
  *Done (2026-10-17):* Move to New Branch ..., and Reorder (Move Above and Move Below, each naming
  the commit passed), see below. A cherry pick of the selected commits, and a reset of the current
  branch to any commit, too. Open: squash merge, absorb.

### E, as done so far (2026-10-17)

- *Move to New Branch ...* in the commit menu: the commit and the newer ones of the current branch,
  not pushed, moved to a new branch made where they are and checked out (files and changes as they
  are), published when ticked; the old branch goes back to the commit before them with git's own
  'branch: Reset to' in its reflog, which Undo in its branch menu reads as a reset. The inference
  draws the commits on the new branch, though the reflog says they were made on the old one.
- *Reorder* in the commit menu, greyed out when neither way can be: the commit swapped with the
  next one of the branch, or the one before it, by `rebase -i --autostash` with gmd's todo list, put
  in place by a sequence editor that copies it (`cp`, through git's shell, also on Windows, where it
  is not tried yet), set for that command over Cmd's `true`. On Amend's rules for the older of the
  two; a conflict stops it as any rebase; recorded as StepKind.Move for Undo.
- *Cherry Pick N Commits into x*: the range pick there was, but hidden, committed each picked commit
  again as the user, losing its author, and lost the rest of the range on a conflict, since a pick
  with `--no-commit` leaves git no sequence to continue. Now git's own `cherry-pick <ids>`, oldest
  first, recorded for Undo as one change; refused for rows of no one line, of the current branch,
  or with a merge among them. One commit is staged for the commit dialog as before.
- *Reset Branch to <sha> ...* in the Undo sub menu: the current branch to any commit, Keep Changes
  (`--mixed`) or Discard Changes (`--keep`), Cancel the default, the question counting the commits
  that leave the branch and saying that origin keeps the pushed ones, which a pull brings back. Only
  from where the repo has the branch (`ResetBranchAsync`). Left: a reset of pushed commits that is
  meant to take them off origin too has no force push, the branch then being only behind.

### A, as done (2026-10-17)

- `LocalRewrites` (beside `RemoteRewrites`) finds them, for the branches that have diverged from
  their remote branch only, with no git call: the branch's reflog has origin's tip as a place it
  was, and the move that took it off (the first after it to a commit without that tip) names it,
  gmd's own record of a squash, amend or drop first. `Repo.LocalRewrites`, and `Commit.IsReplaced` on
  the old version in the view repo instead of `IsBehind`, and no `HasRemoteOnly`, so nothing offers
  a pull.
- `RewrittenHere` says it: once on the status line when found, e.g. right after a squash
  (`ForcePushNotes`); the hint `p force push`; the question before the force push, naming the commits
  it replaces; `u`, the branch menu's Pull and Rebase and Push onto saying why not; Push All leaving
  the branch out. The force push is leased on origin's tip as shown.
- Not told, and shown as diverged as before: commits pushed on top of the old version since, whose
  tip the branch never was at; and a branch only behind, e.g. a reset back, which a pull brings back
  as before. Rebase and Push onto of a branch rewritten here asks for the force push first, rather
  than rebasing and pushing once.

### B, as done (2026-10-17)

- One rule in `CommitRewrite` for Amend (`a` and an older commit's), Drop and Squash, pushed or not:
  the current branch's commits, origin's copy of it being no other branch on them; not while origin
  has commits the branch has not (unless rewritten here already, when they are its old version), and
  not a commit on another branch of origin, which the server asks git (`CheckRewritableAsync`,
  origin/HEAD passed over). `PushedWarning` asks first, No the default, on a shared branch
  (`Branch.IsShared`: main, integration, release) or of someone else's commits on origin, by author.
- The push with the rewrite: a *Push to 'origin/x' with force when done* box in the amend and squash
  dialogs (`ForcePush`), ticked unless the rewrite was asked about first; *Drop and Push* in Drop's
  question; *Rebase onto*, renamed from Rebase and Push onto, asks *Rebase and Push*, *Rebase* or
  Cancel. Leased on origin's tip as shown; a refused push leaves the branch rewritten here (A).
- Found on the way: a squash of pushed and unpushed commits together was refused as "on different
  branches", the two halves of a branch having different names.
- Left: Undo of an amend pushed by force is refused, as for a pushed commit ("would come back with a
  pull"), where Recover Lost Commits has the old one; Rebase onto of a branch rewritten here asks for
  its force push first; Uncommit stays for commits not pushed.

### C, as done (2026-10-17)

- `Branch.GoneRemoteName` keeps the remote branch git reports gone. `p` says the branch was deleted
  on origin, the details pane says so, and Push in its branch menu asks before making it again.
- `MergedBranchService` tells which of those were squash or rebase merged (`git merge-tree
  --write-tree`, git 2.38, into the branch it was made from and into main, as origin has them), for
  the ones with a tip no child has, after the repo is shown, its answers kept for the session by
  commit ids and put in every read at once (`Repo.MergedOnOrigin`). The tip is marked `✓`, the
  details pane, `p` and the push question say where it went, the status line says it once, and the
  worktree list and Remove Worktree count it merged (git deletes it forced).
- *Delete Merged Branches ...* in the repo menu: the local branches with no branch on origin whose
  work is in main or an integration branch, merged as git counts it (`git branch --merged`) or by
  content on origin; not one with no commit of its own. Delete, or Delete All after a question, each
  forced and recorded for Restore Deleted Branch; one checked out here or in a worktree is listed and
  left. The branch menu's Delete Branch no longer asks for force for one merged on origin.
- Left: a branch squash merged whose origin branch was not deleted is not told, nor one whose lines
  main changed again since (the merge conflicts), nor one merged into another branch than its parent
  or main; merged branches still on origin are not listed, since deleting them is deleting them for
  everyone; and Delete Merged Branches does not remove the worktree of a branch with the branch.

---

## Diff and resolver review (2026-10-16)

Made from the screens as the built binary draws them, captured in tmux on a throwaway repository
with a merge conflict, and from mockups of the proposals in the same 16 colors, rendered with agg as
`scripts/demo` renders the animation. Both views work; what holds them back is how they read.

### The diff

- Loud: a whole added or removed line is drawn on bright green or red, and the side with no line as
  a block of `░`.
- The highlight inside a line is per character, so a changed word shows in fragments (`const` to
  `readonly` marks scattered letters, `VatRate` to `vatRate` the `V` alone). The lines of a change
  are paired by position, so unrelated lines are compared (`if (customer.IsMember)` with
  `return customer.IsMember ? …`).
- Chrome: a file takes five rows of rules and blanks, a hunk three and the commit seven, and nothing
  says which function a hunk is in, which git's `@@` line has.
- No key goes to the next change or file: scroll, or `s`.
- Side by side only: at 90 columns a side has 37 characters, and most code ends in `…`.
- The cursor is a gray band over one column that drops the colors under it, unseen on a blank row,
  and it starts on the top rule.

### The resolver

- Three steps to it: the log, the diff of the uncommitted changes, `Enter`. The diff between draws a
  conflict as `=== Start of conflict` rows, with line numbers that jump and a red and green
  highlight between the sides, which there means neither removed nor added.
- The sides are text as it is: what each changed is seen only by showing the ancestor (`b`) and
  comparing by eye.
- A decision takes a side whole, and the common conflict needs some of each. In the test conflict
  none of 1 to 4 is right for either conflict: main rounded the total and renamed `VatRate`, the
  branch added a discount, and taking the branch's side keeps the `VatRate` main removed. Only `e`
  gets there, in a box that hides both sides.
- The result pane shows the current conflict alone, in eight rows; a side is titled `HEAD`, not
  `main`, with no commit subject; there are no line numbers.

### Proposals

1. **The diff's look**, in the 16 colors 1.x has: text white, a change marked by a bar in the gutter
   and the line number's color, and only the changed words on a background; words compared rather
   than characters (DiffPlex's word diff) and marked only where the lines are alike; a blank side
   rather than `░`; one row per file and one per hunk, naming its function from the `@@` line; the
   cursor a reversed line number.
2. **Getting around the diff:** `]` `[` the next and previous change and `}` `{` file, as the
   resolver's `]` `[`; the file the cursor is in pinned on top.
3. **A file list beside the diff** (`Tab`), with each file's kind, its lines and what is left out of
   the commit, and a file collapsed; a unified layout (`v`), by itself below about 120 columns.
4. **The resolver's sides as their changes against the ancestor** (`+` added, `~` changed with its
   words marked, `−` removed, dark), the ancestor read as the file opens; the sides named
   `main (HEAD)` and the branch, with their subjects; line numbers; the result pane the whole file as
   it will be saved, each region marked by the side it came from. This revisits *word-level
   highlighting inside a conflict*, left out by choice (`MODERNIZATION.md`): that was one side
   against the other, this is each against the ancestor.
5. **Combine** (`3`): both sides' changes, offered where they touch no word in common, or one only
   adds lines beside the other's, shown in the result to take and never taken by itself. In the test
   conflict it gives the right result for both. Written test first, as the parsers are.
6. **A Resolve workspace**, opened from the conflict box and the top bar rather than through the
   diff: the conflicted files on the left with how far each is (`● 1 of 2`, `✓ saved`), the other
   changed files, and the way on once all are saved (commit, Continue, Abort). In the diff a
   conflicted file becomes one block, "2 conflicts, Enter to resolve".
7. **Typing in the result pane**, once the focus probe in `MODERNIZATION.md` (*Inline conflict
   editing*) says it can be done.

Waiting for the Terminal.Gui 2.x port: tinted backgrounds for changed lines, syntax highlighting
that does not fight the diff's colors, light themes.

### Steps

- [x] 1. The diff's look (proposal 1). M (every diff snapshot changes) *Done (2026-10-16):* a
  gutter of a bar and the line number, red or green for a change, which stays put while the line
  scrolls sideways; words compared (`LineWords`, in Cui/Common since the resolver uses it too, DiffPlex with a chunker of words, spaces and single
  characters, a space between two changes joined to them) and marked only where the two lines are
  at least half the same; the commit in two rows, the files listed as the commit details list them
  (`ChangedFileText`, shared), a file one row with its rule, a hunk one row with git's function
  name (`SectionDiff.Function`); a side with no line blank; the cursor the line number reversed;
  a conflict's sides in the resolver's yellow and cyan, no word marked. *Not done: pairing the
  lines of a change by likeness.* `FilePatch` pairs them by position, and that decides where a line
  kept ends up when part of a change is committed or discarded, so pairing them otherwise in the
  view would show one file and commit another; and the git layer cannot use DiffPlex.
- [x] 2. Getting around the diff (2). S *Done (2026-10-16):* `]` `[` the next and previous change,
  a run of changed rows or a conflict from its marker, `}` `{` the next and previous file, put at the
  top of the view, each saying which of how many on the status line (`DiffRows.NextChange`,
  `NextFile`); `s` is *Go to*, the four moves and then the files. The pinned file is a breadcrumb in
  a top row of the view: the commit, which is no longer a row of a diff of one commit, and once their
  headers are out of sight, the file and the hunk's function, the commit's subject cut short first
  (`ContentView.TopRow`, drawn in the same draw as the rows, so it never lags a scroll by a key).
  On the way, `Text.ToTextBuilder()` gave a builder of length 0, whatever the text.
- [x] 3. The file list and the unified layout (3). M to L *Done (2026-10-16):* one column below 110
  columns, a change's removed lines above its added ones and a conflict's sides one after the
  other, each named; it follows the terminal's width, and `v` switches it for the diff. A line across
  the view scrolls sideways now, with its gutter in place, which a file added whole never did. The
  list of files (`DiffFileList`) on a terminal 150 wide or more, for more than one file, or the
  commits of a file history: letters, lines, `╎3` for lines left out, the cursor's file marked;
  `Tab` moves the keys there and back, a click goes to a file. `h` hides a file's lines, its header
  alone then; its lines left out of the commit are not refound, and not reported lost, while hidden.
  On the way: Terminal.Gui eats the key after a resize (MODERNIZATION.md).
- [x] 4. The resolver's sides against the ancestor, its names and line numbers, the whole result
  (4). M *Done (2026-10-16):* each side drawn against the ancestor (`SideChanges`: `+`, `~` with its
  words, `-` dark, kept lines dark), a removed line paired with the most alike added one; the
  ancestor read as the file opens, and without it where it cannot be had. The sides named by branch
  and subject (`GetConflictSidesAsync`, `ConflictNames`): `main (HEAD) · <subject>`, a rebase's
  commit by its id. Lines numbered as their side has them, in a gutter that stays while the text
  scrolls. The result pane the whole file as it will be saved (`ConflictRowService.ToResult`), about
  two fifths of the view, each decided conflict's lines barred in its side's color, an undecided one
  a row of the keys, scrolled to the cursor's conflict. With the ancestor known, showing its own
  pane (`b`) no longer moves any row: every line of it is in each side.
- [x] 5. Combine (5). M *Done (2026-10-16), on `c` rather than `3`:* `3` and `4` stay the two sides
  one after the other, as they were. `ConflictCombine`, written test first: each side aligned with the
  ancestor line by line as `SideChanges` draws it, a line one side touched taking its change, lines
  one added going where it added them, and a line both changed combined word by word, words written
  at the edge of the other's change going before or after it; lines both wrote in one place, a line
  removed by one and changed by the other, or a word both changed, and there is none. Both of the
  test conflict's come out as the merge wanted. Taken as text written by hand, so the git layer is
  untouched, and drawn in the result by where each line came from; offered in the undecided row and
  the menu, `e` starts from it, `u` takes it back. The resolver has a status line now, for why not.
- [x] 6. The Resolve workspace (6). M to L *Done (2026-10-16):* `ConflictWorkspace` runs the
  resolver over every conflicted file, from the conflict box, the bar's and the repo menu's Resolve
  Conflicts and the diff's Enter: saving a file resolved opens the next one still conflicted, `}`
  `{` go to another (asking Stay, Save and Go or Discard and Go when decisions are unsaved), and a
  file with no text to merge asks its question in its turn, Cancel passing it over. `ConflictFileList` (no view) lists them
  at the left from 150 columns, `●` the open ones with `0 of 2` on the one shown, `✓` the resolved,
  `Tab` into it; the header says `file 1 of 3`. Once none is left, a box asks Commit (merge, cherry
  pick, revert: the commit dialog, from the diff too) or Continue (rebase, am), or Later; Abort stays
  in the bar and the repo menu, where it was, rather than a button next to the finish. The diff draws
  an unmerged file as its header and one row, "2 conflicts: Enter resolves them". Left out: the
  other changed files in the list, which the diff shows.
- [x] 7. Typing in the result (7), after the probe. *Done (2026-10-16):* the probe passed (see
  `MODERNIZATION.md`, Terminal.Gui), so `e` types the conflict in the result pane rather than a box
  over the sides: a `UITextView` laid over the conflict's rows, the file's lines above and after it,
  its gutter numbered as the file will be (`ConflictTyping`, no view), the lines after renumbered as
  lines are added, from a copy of the decisions with the text typed (`ConflictResolution.WithTyped`).
  It starts from the same text the box did, which the pane's title says. Esc, Tab or a click on the
  sides is done, and what was typed is the decision, with nothing typed left as it was; u takes it
  back. Esc keeps rather than cancels: a reflex Esc should not throw typing away, and Ctrl-Z and u are
  there for the rest.

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
8. **Found later (2026-10-15)**, comparing gmd with the clients its audiences know:
   - A search in the diff and the blame, where a long diff or file could only be scrolled. *Done
     (2026-10-15):* `f` or `/` and then `n` and `Shift-N`, as in the log, but stepping from the
     cursor and matching a part of a line as typed (`RowSearch`); every match marked black on yellow,
     on the cursor's row too, and said on each view's own status line, over the bottom row with the
     key hints off as in the log.
   - A squash left unsigned where git's config signs commits. *Fixed (2026-10-15)*, see
     `MODERNIZATION.md`. Few sign (about one commit in ten on GitHub), but a branch that requires
     signed commits refused the push.
   - Ignoring whitespace in a diff. *Done (2026-10-15):* `w` in the diff view, git's
     `--ignore-all-space` through `DiffOptions`, for the diff it is pressed in only. A file whose
     changes are all whitespace, which git then leaves out, is listed as such, as GitHub lists it
     (`DiffWhitespace`), so it is still in the file list, Scroll to and Discard; and no change of the
     spaces around a line, nor of its line end, is marked.
   - Committing or discarding part of a file (the known limit in the README). *Planned
     (2026-10-15)*, as ticks rather than a staging area, which keeps the checklist's model: in the
     diff of the uncommitted changes `x` leaves a change out, or the selected lines, and the commit
     takes the rest, made with `git commit` on a temporary index of HEAD with the chosen lines
     applied to it (`GIT_INDEX_FILE`), the real index then caught up for the committed paths, the
     working tree never written; a discard is the same patch applied in reverse to the working tree.
     A choice is of a line by its side, number and text, and a left-out line not found exactly when
     the commit is made stops that file ("changed since its changes were chosen"). Not while
     whitespace is hidden, nor during a merge, nor for binary, renamed or deleted files, at first.
     In phases: 1. the patch model and applying it; 2. discarding a change or the selected lines
     (`u`); 3. choosing what to commit (`x`, the drawing, partly ticked files in the commit dialog,
     the commit and amend); 4. later, a way back from discards (`git stash create` first, recorded as
     Restore Deleted Branch records), and renamed and deleted files in part. *Phase 1 done
     (2026-10-15):* `FilePatch`, `PatchParser` and `PatchService` in the git layer, see `CLAUDE.md`.
     *Phase 2 done (2026-10-16):* `u` in the diff of the uncommitted changes has This Change, or
     Selected Lines, at the top of its menu, asked first (`DiffLines`, `Server.DiscardLinesAsync`,
     `ChosenLines`); a line is matched with its tabs as the diff shows them, three spaces each. On
     the way, a greyed out item of the diff's or the blame's menu said why on the log's status line,
     behind the view; it says it on the view's own now (`Menu.SayingWhyNotOn`), drawn under the
     open menu, which is all Terminal.Gui redraws then.
     *Phase 3 done (2026-10-16):* `x` in the diff of the uncommitted changes leaves the change, the
     selected lines or on a file's header the whole file out of the commit, or puts them back
     (`LeftOutLines`, for the session, of the repo shown); drawn dark with `╎`, counted on the
     header and in the commit dialog. The commit is `Server.CommitLinesAsync`, the patches of the
     rest in `PatchService.CommitPatchesAsync`, through an index of HEAD, the real index reset for
     the committed paths after. A left-out line is found again where only its number moved (its
     text unique among the file's changed lines of its version); one not found stops the commit,
     the header saying how many. An older commit's amend refuses a file with lines left out. Phase
     1's patch of a file in a repository with no commit yet was not read: with a path git says
     'bad revision', not 'unknown revision', so HEAD is asked about first now.
     *Phase 4's way back done (2026-10-16):* every discard, the lines, a file, all changes, all with
     the ignored files, and a stash dropped, keeps what it throws away (`Server.DiscardedChanges`):
     the working tree before and after it as commits on `refs/gmd/discarded`, whose reflog keeps
     them for git's 30 days, a dropped stash as a commit whose parent is the stash. Restore
     Discarded Changes, in the Undo menu, lists them, diffs them, and puts one back, the difference
     applied to the working tree, which git refuses whole if its files changed since. `git stash
     create`, which this plan named, leaves out the new files, so the snapshot is a copy of the index
     with everything added and written as a tree (`SnapshotService`). The questions say the way back
     now, and the status line after each discard. The ignored files are not kept.
     *Phase 4's files done (2026-10-16):* a deleted file in part needed nothing, phase 1's patches
     and the rows already had it, now tested end to end (a line discarded is the file with that line
     again). A renamed file in part: its patch is of both paths, the rename found as the uncommitted
     diff finds it (`PatchService` stages everything into the copy of the index now, and asks
     `--name-status` for the old path); its lines go in with the rename, the old path then not
     committed whole too, and with none of them the rename alone does, a patch with no hunks not
     being empty then; taken back, they are a change of the file where it is now, so the rename
     stays. Git writes a tab after a path with a space on the '---' and '+++' lines, not in the
     'diff --git' one, which the header made for that keeps.
   - Open, in the order they would be met: a cherry-pick of the selected commits, whose range the
     copy menu offers for the command line; opening a file in the editor at a line of the diff or the
     blame; a rebase of a published branch without its force push; and a reset of a branch to any
     commit, which takes Uncommit and then Discard today.

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
  *Homebrew since 1.7 (2026-10-14), see proposal 24.*
- **What happens when gmd crashes?** It logs the exception and exits without a word
  (`ExceptionHandling.cs:60-110`, where the dialog for it is commented out). The About box has no
  link to the project, and nothing says how to report a problem or where the log is.
- **What does gmd write, and what does it send?** `.gmdconfig` in each repository's git folder;
  `refs/gmd-metadata-key-value/data` in every repository with a reflog, pushed only when the shared
  branch structure is on; `~/.gmdconfig` and `~/gmd.log`; a request to GitHub's releases API every
  hour unless that is turned off; and the tags of Add Tag, above. There is no telemetry. People ask
  this before they run a tool on their work, and the README should answer it.
- **Can others contribute?** Building needs a preview .NET 11 SDK, for the C# 15 unions, until .NET
  11 is released (`docs/UPGRADING.md`).
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
   fails, and `LoginAdvice` says what to do: the key to `ssh-add`, the host to trust, a credential
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
   *Since (2026-10-15):* Ctrl-C in the log asks what to copy (`CommitCopy`), the copy menu, which
   is also the commit menu's Copy: of a commit the id, message, subject, id and subject, short id
   (as the log shows it) or the link; of the rows
   selected the ids, the messages, the ids and subjects, or the range `<oldest>^..<newest>`, all
   oldest first, as `git cherry-pick` takes them. `i` and `Shift-I` act on the selection too. Ctrl-C
   used to copy one selected row as drawn, the graph with it, and several as sid and subject, of
   the top row's branch where the log highlighted the starting row's.
   *Since (2026-10-15):* the short id is eight characters wherever gmd shows one or names a branch
   after a commit (`StringExtensions.SidLength`), rather than six: what `git log --oneline` writes in
   a repository of 16,000 objects or more and GitLab shows, and GitHub links, which it did not with
   six. Of 50,000 commits, one in 300 shared its six with another, one in 86,000 its eight.
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
   worse the longer they get. *Replaced (2026-10-08) by the help on GitHub:* `?` and F1 open
   `help.md` in the browser, at the commit gmd was built from and at the section of the view, and
   copy the link where there is no browser, e.g. over ssh. The help had grown to some 700 lines, and
   a browser gives it what the box could not: the width of the window, search, and links between its
   sections. It stays in the repository rather than the wiki, so it is still written, reviewed and
   tested with the code it describes.
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
   Sends; its development half is `docs/CONTRIBUTING.md` now.
2. Homebrew, Scoop or winget, and AUR packages, and a build for Intel Macs.
3. Issue templates, linked from About and the README. *Done (2026-10-08):* a Problem form, which
   asks for the version, the system and the log, and a Suggestion form, which asks why and how the
   user works with git (`.github/ISSUE_TEMPLATE`); the README links both, and a crash links the
   Problem form. About keeps the issues page, whose New issue offers them.
4. Captions in the animation, saying which key is pressed. *Done (2026-10-08):* a caption bar
   under gmd's screen, as a subtitle, saying what each part of the story shows and with which key
   (`DemoRecording.Caption`); it is in the GIF once `scripts/demo` re-records it.
5. The inference's agreement with the reflog, measured on public repositories and published.
   *Postponed (2026-10-08)* to the task that works on the inference, to be measured before and
   after it; the plan is in `MODERNIZATION.md` (deferred).

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

- [x] 20. **Issue templates** (D.3), linked from the README. S
- [x] 21. **Captions in the animation** (D.4). S
- [ ] 22. *Postponed (2026-10-08), to go with the work on the inference, measured before and after
  it; see `MODERNIZATION.md` (deferred).* **The inference measured** (D.5) on public repositories of
  each workflow, and the numbers
  in the README. M
- [x] 23. **The terminals people have** (part 5): 80 columns, Windows Terminal, fonts with no `✦`,
  `Ϙ` or `ß`. M *Done (2026-10-08), as far as it can be from Linux:* 80 by 24 is usable, end to end
  tested (`SmallTerminalTest`), after the commit dialog's file list and the Recover Lost Commits
  list were made to fit. What only Windows and macOS show is a checklist in `docs/CONTRIBUTING.md`, for
  the symbols new since the summer (`⇧`, `✦`, `⌂`, `·`) and the keys and features that depend on the
  system; the rest waits for reports.
- [ ] 24. **Packages** (D.2): Homebrew, Scoop or winget, AUR, and an Intel Mac build. Needs the
  author's accounts. M *Postponed (2026-10-08).* *Homebrew done (2026-10-14):* a tap the release
  workflow writes, and a gmd installed from it updated by Homebrew (MODERNIZATION.md). Planned
  next, in this order: winget and an `install.ps1` (`irm … | iex`) for Windows, which should
  meet no SmartScreen warning since no browser downloads anything (to be tried on Windows), with
  `gmdSetup.exe` out of the README; a dev container feature; then maybe Scoop, a dotnet tool, an
  Intel Mac build. *`install.ps1` on dev (2026-10-14):* `irm … | iex` installs gmd.exe to
  `%LOCALAPPDATA%\Programs\gmd` and the user's PATH, and the README leads with it on Windows; tried
  on Linux PowerShell as far as it goes there (the download, the checksum, the replace), and on
  Windows (2026-10-15): installed and on the PATH at once, installed again over a gmd running, and
  the user's PATH kept as written, `REG_EXPAND_SZ` with its `%USERPROFILE%`. *Dev container feature on dev (2026-10-14):* `ghcr.io/michael-reichenauer/gmd/gmd:1`,
  tested by CI in containers and published from main, in 1.8, public with no step by hand.
  *winget left out (2026-10-15):* the first version, 1.8, submitted with Komac
  (microsoft/winget-pkgs#452829), was blocked by Defender in winget's validation sandbox
  (`Validation-Defender-Error`), while Defender on a Windows PC found nothing in the same file: the
  sandbox blocks unsigned programs few have run, and each release would be a new one. Getting past
  it takes a false positive report per release, or signing, which still leaves SmartScreen's
  warning, so it was judged not worth it: the PowerShell install is the way on Windows. gmd still
  knows a winget install (`PackageManager.Winget`); the release workflow's step that proposed each
  release with Komac was removed, and is in cab1e09b to take up again. *Scoop and a dotnet tool
  postponed (2026-10-15)*: Scoop's bucket takes two commands to install, where Homebrew's tap takes
  one, and the PowerShell install already serves Windows; a dotnet tool needs the .NET 10 SDK, so it
  reaches .NET developers only. Either is about the work of the tap, should someone ask for it.

Waiting for the Terminal.Gui 2.x port:

- [ ] 25. **Light themes, `NO_COLOR` and colors that do not lean on red against green** (B.4). L
  *Postponed (2026-10-08)*, with the Terminal.Gui 2.x port it waits for.

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
  With no recent repositories it starts with a bare separator. *Superseded (2026-10-13):* a start
  page replaced the menu; it numbers the repositories to open, and explains each action to someone
  who has opened none.

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
     which says why it is there. (Since replaced by the start page, `Cui/Start/`.)

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
   *Since (2026-10-08):* with no branch highlighted it starts with the row's commit and its branch
   (`commit on dev:`, `changes on main:` on the uncommitted row), the name in the branch's color, and
   the top bar's `(dev)` is gone. That one named whichever had moved last of the row and the
   highlight, in the parentheses that mark a tip in the log, so it read as neither. With the key
   hints off the bar says `commit on dev`, the row's only, and leaves it out when it is too narrow.
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
   record (see MODERNIZATION.md). *The way back done (2026-10-16):* Restore Discarded Changes, see
   the product review's part 2, item 8.

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
