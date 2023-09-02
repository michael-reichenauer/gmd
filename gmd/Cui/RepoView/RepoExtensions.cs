using gmd.Server;

namespace gmd.Cui.RepoView;

static class RepoExtensions
{
    static readonly int maxTipNameLength = 16;

    public static string ShortNiceUniqueName(this Branch branch)
    {
        var name = branch.NiceNameUnique;
        if (name.Length > maxTipNameLength)
        {   // Branch name to long, shorten it
            name = $"┅{name[^maxTipNameLength..]}";
        }
        return name;
    }

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
        repo.Status.ModifiedFiles
        .Concat(repo.Status.AddedFiles)
        .Concat(repo.Status.DeletedFiles)
        .Concat(repo.Status.ConflictsFiles)
        .Concat(repo.Status.RenamedTargetFiles)
        .OrderBy(f => f)
        .ToList();
}