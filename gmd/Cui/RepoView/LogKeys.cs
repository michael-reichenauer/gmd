using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.RepoView;

// What a key does, for a key of the log view whose answer depends on where the cursor is: the hint
// the key-hint line shows for it, or why it does nothing, which the status line says when it is
// pressed. A key that acts with no hint leaves it to the command it runs, e.g. a push of the current
// branch says 'Nothing to push' itself.
record KeyAction(string Hint, string WhyNot = "")
{
    public bool CanAct => WhyNot == "";
    public bool IsHinted => CanAct && Hint != "";

    public static KeyAction No(string whyNot) => new("", whyNot);
}

// The decision for each of those keys. The keys (RepoViewInput) and the hints (KeyHints) both ask
// here, so a key cannot do what its hint does not say, or the other way round, which they did while
// each decided on its own: 'b' on the uncommitted row, 'd' and 'm' with rows selected, 'E' on a
// branch held by another worktree. What a key then does, a command or a menu, is RepoViewInput's.
//
// Most of these act on the highlighted branch, the hoover, and on the current branch or the row's
// commit when none is highlighted. Plain functions of the shown repo, so they are tested without a
// terminal.
static class LogKeys
{
    // 'e' merges the highlighted branch into the current one, or for the current branch opens the
    // menu of branches to merge into it. Worded as the merge items are, naming the current branch.
    public static KeyAction Merge(IViewRepo repo, Hoover hoover)
    {
        if (!hoover.IsBranch)
            return KeyAction.No("Highlight a branch with ← → first, and 'e' merges it into the current branch");
        if (!repo.Status.IsOk)
            return KeyAction.No("Commit the changes first, then merge");

        return new($"merge into {CurrentName(repo)}");
    }

    // 'E', the mirror of 'e': merges the current branch into the highlighted one, which git can only
    // do by checking that one out on the way, or for the current branch opens the menu of branches
    // to merge it into. Hinted for the current branch only, the merge into another being rarer.
    public static KeyAction MergeInto(IViewRepo repo, Hoover hoover)
    {
        if (!hoover.IsBranch)
            return KeyAction.No("Highlight a branch with ← → first, and 'E' merges the current branch into it");
        if (!repo.Status.IsOk)
            return KeyAction.No("Commit the changes first, then merge");

        var branch = HooveredLocal(repo, hoover);
        if (branch.IsCurrent)
            return new($"merge {CurrentName(repo)} into");

        // A branch git no longer has would be recreated by the checkout, so it is not offered,
        // exactly as in the branch menu, nor one checked out in another worktree, which git refuses
        // to check out here
        if (!branch.IsGitBranch)
            return KeyAction.No($"'{branch.NiceNameUnique}' no longer exists, so nothing can be merged into it");
        if (repo.Repo.WorktreePathOf(branch) != "")
            return KeyAction.No(Why.InWorktree(branch));

        return new("");
    }

    // 's' switches to the highlighted branch
    public static KeyAction Switch(IViewRepo repo, Hoover hoover)
    {
        if (!hoover.IsBranch)
            return KeyAction.No("Highlight a branch with ← → first, and 's' switches to it");

        var branch = repo.Repo.BranchByName[hoover.BranchPrimaryName];
        if (branch.PrimaryName == repo.Repo.CurrentBranch().PrimaryName)
            return KeyAction.No($"Already on '{branch.NiceNameUnique}'");

        return new("switch");
    }

    // 'p' pushes the highlighted branch, as the branch menu's Push does, and the current branch when
    // none is highlighted, which goes through the command that asks before a force push
    public static KeyAction Push(IViewRepo repo, Hoover hoover)
    {
        if (Hoovered(repo, hoover) is not Branch branch || IsCurrent(branch))
            return new(BranchPushPullCommands.CanPushCurrentBranch(repo.Repo) ? "push" : "");

        return BranchPushPullCommands.CanPushBranch(repo.Repo, branch)
            ? new("push")
            : KeyAction.No(BranchPushPullCommands.WhyNoPushBranch(repo.Repo, branch));
    }

    // 'u' pulls the highlighted branch, as the branch menu's Pull does, and the current branch when
    // none is highlighted, which merges, so it can be pulled when diverged
    public static KeyAction Pull(IViewRepo repo, Hoover hoover)
    {
        if (Hoovered(repo, hoover) is not Branch branch || IsCurrent(branch))
            return new(BranchPushPullCommands.CanPullCurrentBranch(repo.Repo) ? "pull" : "");

        return BranchPushPullCommands.CanPullBranch(repo.Repo, branch)
            ? new("pull")
            : KeyAction.No(BranchPushPullCommands.WhyNoPullBranch(repo.Repo, branch));
    }

    // Ctrl-C asks what to copy of the row's commit or of the commits selected, the copy menu, and 'i'
    // and '⇧i' copy their ids and messages straight away, see CommitCopy. Hinted for selected rows
    // only, where the line has room for it.
    public static KeyAction Copy(IViewRepo repo, Selection selection) =>
        CommitCopy.WhyNot(repo.Repo, selection, repo.CurrentIndex) is var whyNot && whyNot != ""
            ? KeyAction.No(whyNot)
            : new("copy");

    // 'd' diffs the rows selected, else opens the menu of diffs of the highlighted branch, else
    // diffs the row's commit; on the uncommitted row with conflicts the diff is where they are
    // resolved, with Enter
    public static KeyAction Diff(IViewRepo repo, Hoover hoover, Selection selection)
    {
        if (selection.IsRows)
            return new("diff");
        if (hoover.IsBranch)
            return repo.Status.IsOk ? new("diff") : KeyAction.No(Why.Changes);

        var isConflicts = repo.RowCommit.IsUncommitted && repo.Status.Conflicted > 0;
        return new(isConflicts ? "resolve" : "diff");
    }

    // 'h' hides the highlighted branch, else the row's branch, which is not hinted. The main branch
    // is always shown: 'h' on it, and its menu item, did nothing and said nothing.
    public static KeyAction Hide(IViewRepo repo, Hoover hoover)
    {
        var branch = Hoovered(repo, hoover) ?? repo.RowBranch;
        if (branch.IsMainBranch)
            return KeyAction.No(Why.AlwaysShown(branch));

        return new(hoover.IsBranch ? "hide" : "");
    }

    // 'g' gives the highlighted branch another color, else the row's branch; not hinted, being rare.
    // The main branch and a deleted one keep theirs: 'g' on them did nothing and said nothing.
    public static KeyAction Color(IViewRepo repo, Hoover hoover)
    {
        var branch = Hoovered(repo, hoover) ?? repo.RowBranch;
        if (branch.IsMainBranch)
            return KeyAction.No(Why.AlwaysMagenta);
        if (!branch.IsGitBranch)
            return KeyAction.No(Why.AlwaysGray);

        return new("");
    }

    // 'b' makes a branch from the highlighted branch, else at the row's commit
    public static KeyAction NewBranch(IViewRepo repo, Hoover hoover) =>
        !hoover.IsBranch && repo.RowCommit.IsUncommitted
            ? KeyAction.No(BranchCreateCommands.NoCommitToStartAt)
            : new("new branch");

    // The highlighted branch as the branch menu is for it, i.e. the primary one of a pair
    public static Branch? Hoovered(IViewRepo repo, Hoover hoover) =>
        hoover.IsBranch ? repo.Repo.BranchByName[hoover.BranchPrimaryName] : null;

    // The highlighted branch, the local one of a pair, which is the one checked out
    public static Branch HooveredLocal(IViewRepo repo, Hoover hoover)
    {
        var branch = repo.Repo.BranchByName[hoover.BranchPrimaryName];
        return branch.LocalName != "" ? repo.Repo.BranchByName[branch.LocalName] : branch;
    }

    static bool IsCurrent(Branch b) => b.IsCurrent || b.IsLocalCurrent;

    static string CurrentName(IViewRepo repo) => repo.Repo.CurrentBranch().ShortNiceUniqueName();
}
