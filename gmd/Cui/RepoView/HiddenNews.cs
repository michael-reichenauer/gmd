using gmd.Server;

namespace gmd.Cui.RepoView;

// A hidden remote branch with commits the user has not seen, and how many
record HiddenBranchNews(Branch Branch, int Count);

// The commits pushed to branches the user does not show. ▲ and ▼ count only the shown branches, so
// without this a hidden branch can get new work with nothing on screen saying so, which is the cost
// of a tidy log. What is new is what the user has not seen: for each remote branch the tip it had
// when it was last shown is remembered (RepoConfig.SeenTips, so it holds across sessions), and the
// branch's own commits above that tip are new. A branch that was not there then is new with all of
// its own commits. The first time, when nothing is remembered, every tip is taken as seen, rather
// than every branch the repository ever had being news.
//
// Counted in memory, on the commits the log already holds, walking a branch down its first parents
// for as long as the commits are the branch's own: a merge into it counts as one, and the commits
// it brought are the other branch's. This is the counting and the remembering with no view, so it
// is tested without a terminal; RepoView keeps it up to date and the application bar shows it.
static class HiddenNews
{
    // The hidden remote branches with new commits, the most recently changed first
    public static IReadOnlyList<HiddenBranchNews> Of(Repo repo, IReadOnlyDictionary<string, string> seenTips)
    {
        var shown = repo.ViewBranches.Select(b => b.Name).ToHashSet();

        return RemoteBranches(repo)
            .Where(b => !shown.Contains(b.Name))
            .Select(b => new HiddenBranchNews(b, NewCount(repo, b, seenTips.GetValueOrDefault(b.Name))))
            .Where(n => n.Count > 0)
            .OrderBy(n => repo.CommitById.TryGetValue(n.Branch.TipId, out var tip) ? tip.GitIndex : int.MaxValue)
            .ToList();
    }

    // What is remembered as seen once this repo is shown: the tips of the shown branches, and the
    // first time every tip. A branch that is gone is forgotten, so that one made again later with
    // the same name is new rather than compared with a tip it never had.
    public static Dictionary<string, string> Seen(Repo repo, IReadOnlyDictionary<string, string> seenTips)
    {
        var remote = RemoteBranches(repo).ToList();
        if (seenTips.Count == 0)
            return AllSeen(repo);

        var names = remote.Select(b => b.Name).ToHashSet();
        var seen = seenTips.Where(t => names.Contains(t.Key)).ToDictionary(t => t.Key, t => t.Value);
        var shown = repo.ViewBranches.Select(b => b.Name).ToHashSet();
        remote.Where(b => shown.Contains(b.Name)).ForEach(b => seen[b.Name] = b.TipId);
        return seen;
    }

    // Every tip taken as seen, for the first time and for 'Mark All as Seen'
    public static Dictionary<string, string> AllSeen(Repo repo) =>
        RemoteBranches(repo).ToDictionary(b => b.Name, b => b.TipId);

    public static bool IsSame(IReadOnlyDictionary<string, string> a, IReadOnlyDictionary<string, string> b) =>
        a.Count == b.Count && a.All(t => b.TryGetValue(t.Key, out var v) && v == t.Value);

    static int NewCount(Repo repo, Branch branch, string? seenTip)
    {
        var count = 0;
        var id = branch.TipId;
        while (
            id != seenTip
            && repo.CommitById.TryGetValue(id, out var commit)
            && commit.BranchPrimaryName == branch.PrimaryName
        )
        {
            count++;
            if (commit.ParentIds.Count == 0)
                break;
            id = commit.ParentIds[0];
        }

        return count;
    }

    static IEnumerable<Branch> RemoteBranches(Repo repo) => repo.AllBranches.Where(b => b.IsRemote && b.IsGitBranch);
}
