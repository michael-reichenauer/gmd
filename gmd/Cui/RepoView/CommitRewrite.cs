using gmd.Server;

namespace gmd.Cui.RepoView;

// Whether a commit can be rewritten, by Amend or Drop, and why not when it cannot. Only a commit not
// pushed yet can, since one that others may have would come back with their next push, and only one
// on the current branch with nothing in the way of the rebase that rewrites it: a merge after it,
// which the rebase would flatten into plain commits, or another branch or a tag on it or after it,
// which would be left on the old commits.
//
// Plain functions of the repo, so they are testable without a view. The augmented service checks the
// same against git before it rewrites anything (AugmentedService.CheckRewritableAsync).
static class CommitRewrite
{
    // Not pushed yet: ahead of its remote branch, or a commit of a local branch never pushed. The one
    // rule for Amend, Uncommit and Drop, which before it was only 'ahead' for Amend, so the last
    // commit of a branch never pushed could be uncommitted but not amended.
    public static bool IsNotPushed(Repo repo, Commit commit) =>
        !commit.IsUncommitted
        && (
            commit.IsAhead
            || (repo.BranchByName.TryGetValue(commit.BranchName, out var b) && !b.IsRemote && b.RemoteName == "")
        );

    // Why a commit older than the last one cannot be amended, or "" when it can. Changes in the
    // working tree are no reason: the ones not added to it are put aside while it runs.
    public static string WhyNotAmend(Repo repo, Commit commit) =>
        commit.IsUncommitted ? "A commit is amended: move to one first" : WhyNot(repo, commit, "amended");

    // Why a commit cannot be dropped, or "" when it can. The files go back to before it, so changes
    // that git would have to overwrite are committed or stashed first.
    public static string WhyNotDrop(Repo repo, Commit commit)
    {
        if (commit.IsUncommitted)
            return "A commit is dropped: move to one first";
        if (WhyNot(repo, commit, "dropped") is var whyNot && whyNot != "")
            return whyNot;
        if (commit.ParentIds.Count == 0)
            return "The first commit cannot be dropped";
        if (!repo.Status.IsOk)
            return Why.Changes;
        return "";
    }

    static string WhyNot(Repo repo, Commit commit, string done)
    {
        if (repo.Status.IsMerging)
            return Why.InProgress;
        if (!IsNotPushed(repo, commit))
            return $"Only a commit not yet pushed can be {done}";
        if (CommitsDownTo(repo, commit) is not { } commits)
            return $"Only a commit on the current branch can be {done}";

        if (commit.ParentIds.Count > 1)
            return $"A merge cannot be {done}: the rebase would flatten it";
        if (commits.Any(c => c.ParentIds.Count > 1))
            return "There is a merge after this commit, which the rebase would flatten";

        var ids = commits.Select(c => c.Id).ToHashSet();
        var current = repo.AllBranches.First(b => b.IsCurrent);
        if (repo.AllBranches.FirstOrDefault(b => b.Name != current.Name && ids.Contains(b.TipId)) is { } other)
            return OnIt($"'{other.NiceNameUnique}'");
        if (
            commits
                .SelectMany(c => c.AllChildIds)
                .FirstOrDefault(id => id != Repo.UncommittedId && !ids.Contains(id)) is
            { } childId
        )
            return OnIt(
                repo.CommitById.TryGetValue(childId, out var child) ? $"'{child.BranchNiceUniqueName}'" : "A branch"
            );
        if (commits.SelectMany(c => c.Tags).FirstOrDefault() is { } tag)
            return OnIt($"The tag '{tag.Name}'");
        return "";
    }

    static string OnIt(string what) => $"{what} is on this commit or one after it, and would keep the old ones";

    // The commits of the current branch from its last one down to the commit, newest first, or null
    // when the commit is not among the ones not pushed yet. Down git's first parents, which is the
    // line the rebase replays, whereas gmd swaps the parents of a pull merge to draw it.
    static IReadOnlyList<Commit>? CommitsDownTo(Repo repo, Commit commit)
    {
        var current = repo.AllBranches.FirstOrDefault(b => b.IsCurrent);
        if (current == null || current.IsRemote || current.IsDetached)
            return null;

        List<Commit> commits = [];
        var c = repo.CurrentCommit();
        while (IsNotPushed(repo, c))
        {
            commits.Add(c);
            if (c.Id == commit.Id)
                return commits;
            if (c.ParentIds.Count == 0 || !repo.CommitById.TryGetValue(c.GitParentIds[0], out var parent))
                return null;
            c = parent;
        }
        return null;
    }
}
