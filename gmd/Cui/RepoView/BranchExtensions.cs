using gmd.Server;

namespace gmd.Cui.RepoView;

static class BranchExtensions
{
    static readonly int maxTipNameLength = 16;

    public static string ShortNiceUniqueName(this Branch branch)
    {
        var name = branch.NiceNameUnique;
        if (name.Length > maxTipNameLength)
        { // Branch name to long, shorten it
            name = $"┅{name[^maxTipNameLength..]}";
        }
        return name;
    }
}
