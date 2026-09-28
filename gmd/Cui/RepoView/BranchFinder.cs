using gmd.Server;

namespace gmd.Cui.RepoView;

// Which branches the Find Branch dialog lists for what has been typed, best match first. A branch
// matches when its name contains every word typed, case ignored, in any order. The order is what
// makes a few letters enough: first the names in which the first word starts the name, or a part
// of it after a '/', '-', '_' or '.', since that is how names are remembered ('log' for
// 'feature/login'); then the branches that still exist before the deleted ones gmd recovered from
// merge messages; and then the most recent first. With nothing typed, that is every branch, most
// recent first.
//
// The same branches as the Open Branch menu's 'Active and Deleted', i.e. one per local and remote
// pair. Plain and with no view, so it is tested without a terminal.
static class BranchFinder
{
    static readonly char[] PartSeparators = ['/', '-', '_', '.'];

    public static IReadOnlyList<Branch> Find(Repo repo, string text)
    {
        var words = text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return repo
            .AllBranches.Where(b => b.IsPrimary)
            .DistinctBy(b => b.PrimaryName)
            .Select(b => (Branch: b, Name: b.NiceNameUnique.ToLowerInvariant()))
            .Where(b => words.All(b.Name.Contains))
            .OrderBy(b => words.Length == 0 || IsPartStart(b.Name, words[0]) ? 0 : 1)
            .ThenBy(b => b.Branch.IsGitBranch ? 0 : 1)
            .ThenBy(b => repo.CommitById[b.Branch.TipId].GitIndex) // Newest commit first, as git log
            .Select(b => b.Branch)
            .ToList();
    }

    static bool IsPartStart(string name, string word) =>
        name.StartsWith(word) || PartSeparators.Any(s => name.Contains(s + word));
}
