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

    public static IReadOnlyList<string> GetUncommittedFiles(this Repo repo) =>
        repo.Status.ModifiedFiles
        .Concat(repo.Status.AddedFiles)
        .Concat(repo.Status.DeletedFiles)
        .Concat(repo.Status.ConflictsFiles)
        .Concat(repo.Status.RenamedTargetFiles)
        .OrderBy(f => f)
        .ToList();
}