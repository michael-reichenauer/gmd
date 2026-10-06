namespace gmd.Server;

static class RepoExtensions
{
    public static Branch CurrentBranch(this Repo repo) => repo.AllBranches.First(b => b.IsCurrent);

    public static Commit CurrentCommit(this Repo repo)
    {
        var c = repo.CommitById[repo.CurrentBranch().TipId];
        if (c.IsUncommitted && c.ParentIds.Count > 0)
        {
            c = repo.CommitById[c.ParentIds[0]];
        }
        return c;
    }

    // The folder a branch is checked out in, when that is another worktree than the one this repo
    // was read from; empty otherwise. A remote branch answers for its local branch, since that is
    // the one a worktree holds.
    public static string WorktreePathOf(this Repo repo, Branch branch)
    {
        if (!branch.IsRemote)
            return branch.WorktreePath;
        return branch.LocalName != "" && repo.BranchByName.TryGetValue(branch.LocalName, out var local)
            ? local.WorktreePath
            : "";
    }

    // The branches that start at a commit with no commits of their own: their tip is the commit, but
    // the commit is another branch's, e.g. a branch just made with 'git branch' or by another tool,
    // before anything is committed on it. Such a branch has no child commit to show where it branches
    // out, so this is how it is found there. A twin of the commit's own branch is that branch, and the
    // detached HEAD is always shown, so neither is among them.
    public static IEnumerable<Branch> BranchesStartingAt(this Repo repo, string commitId) =>
        repo.BranchesWithNoCommits().Where(b => b.TipId == commitId);

    // The same for every commit, for drawing them all at once
    public static IEnumerable<Branch> BranchesWithNoCommits(this Repo repo) =>
        repo.AllBranches.Where(b =>
            !b.IsDetached && repo.CommitById.TryGetValue(b.TipId, out var tip) && tip.BranchPrimaryName != b.PrimaryName
        );

    // The worktrees other than the one this repo was read from
    public static IReadOnlyList<Worktree> OtherWorktrees(this Repo repo) =>
        repo.Worktrees.Where(w => !w.IsCurrent).ToList();

    // Whether deleting the branch would lose commits: its tip is on it and nothing else, i.e. no
    // other branch has merged it or continues from it. A tip that is not in the log at all (an
    // old branch in a truncated log) is taken as unmerged, since nothing says otherwise.
    public static bool HasUnmergedCommits(this Repo repo, Branch branch)
    {
        if (!repo.CommitById.TryGetValue(branch.TipId, out var tip))
            return true;
        return !tip.AllChildIds.Any() && tip.BranchName == branch.Name;
    }

    public static IReadOnlyList<string> GetUncommittedFiles(this Repo repo) =>
        repo
            .Status.ModifiedFiles.Concat(repo.Status.AddedFiles)
            .Concat(repo.Status.DeletedFiles)
            .Concat(repo.Status.ConflictsFiles)
            .Concat(repo.Status.RenamedTargetFiles)
            .OrderBy(f => f)
            .ToList();
}
