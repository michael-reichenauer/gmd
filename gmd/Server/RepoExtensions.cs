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

    public static IReadOnlyList<string> GetUncommittedFiles(this Repo repo) =>
        repo
            .Status.ModifiedFiles.Concat(repo.Status.AddedFiles)
            .Concat(repo.Status.DeletedFiles)
            .Concat(repo.Status.ConflictsFiles)
            .Concat(repo.Status.RenamedTargetFiles)
            .OrderBy(f => f)
            .ToList();
}
