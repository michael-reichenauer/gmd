using gmd.Server;

namespace gmd.Cui.RepoView;

// A hidden branch with something the user has not seen: commits of its own, how many, or the branch
// itself, which is new
record HiddenBranchNews(Branch Branch, int Count, bool IsNew = false);

// What is new on the branches the user does not show. ▲ and ▼ count only the shown branches, and a
// branch made by someone or something else, a teammate, Dependabot, Claude Code, is not shown unless
// the user shows it, so without this a branch can come, or get new work, with nothing on screen
// saying so, which is the cost of a tidy log. gmd tells rather than shows, since showing every new
// branch would fill the log of a busy repository after every fetch.
//
// What is new is what the user has not seen: for each branch the tip it had when it was last shown is
// remembered (RepoConfig.SeenTips, so it holds across sessions), and the branch's own commits above
// that tip are new. A branch that was not there then is new, with or without commits of its own. The
// first time, when nothing is remembered, every tip is taken as seen, rather than every branch the
// repository ever had being news.
//
// The branches are the remote ones and the local ones with no remote branch, i.e. never pushed or
// with the remote branch deleted; a local branch with one is told of by it. The branch the user is on
// is their own work, and never news.
//
// Counted in memory, on the commits the log already holds, walking a branch down its first parents
// for as long as the commits are the branch's own: a merge into it counts as one, and the commits
// it brought are the other branch's. This is the counting and the remembering with no view, so it
// is tested without a terminal; RepoView keeps it up to date and the application bar shows it.
static class HiddenNews
{
    // The hidden branches with something new, the most recently changed first
    public static IReadOnlyList<HiddenBranchNews> Of(Repo repo, IReadOnlyDictionary<string, string> seenTips)
    {
        var shown = repo.ViewBranches.Select(b => b.Name).ToHashSet();

        return NewsBranches(repo)
            .Where(b => !shown.Contains(b.Name) && !b.IsCurrent)
            .Select(b =>
                seenTips.TryGetValue(b.Name, out var seenTip)
                    ? new HiddenBranchNews(b, NewCount(repo, b, seenTip))
                    : new HiddenBranchNews(b, NewCount(repo, b, null), IsNew: true)
            )
            .Where(n => n.IsNew || n.Count > 0)
            .OrderBy(n => repo.CommitById.TryGetValue(TipOf(repo, n.Branch), out var tip) ? tip.GitIndex : int.MaxValue)
            .ToList();
    }

    // What is remembered as seen once this repo is shown: the tips of the shown branches and the
    // current one, and the first time every tip. A branch that is gone is forgotten, so that one made
    // again later with the same name is new rather than compared with a tip it never had.
    //
    // 'isForAll' is false for what an older gmd remembered, which held only remote branches, and
    // those of them with no commits of their own were not news; such branches with nothing remembered
    // are taken as seen once, rather than all becoming news at once.
    public static Dictionary<string, string> Seen(
        Repo repo,
        IReadOnlyDictionary<string, string> seenTips,
        bool isForAll = true
    )
    {
        var branches = NewsBranches(repo).ToList();
        if (seenTips.Count == 0)
            return AllSeen(repo);

        var names = branches.Select(b => b.Name).ToHashSet();
        var seen = seenTips.Where(t => names.Contains(t.Key)).ToDictionary(t => t.Key, t => t.Value);

        foreach (var b in branches.Where(b => !seen.ContainsKey(b.Name)))
        {
            if (CarriedOverTip(repo, b, seenTips) is string carried)
                seen[b.Name] = carried;
            else if (!isForAll && (!b.IsRemote || NewCount(repo, b, null) == 0))
                seen[b.Name] = TipOf(repo, b);
        }

        var shown = repo.ViewBranches.Select(b => b.Name).ToHashSet();
        branches.Where(b => shown.Contains(b.Name) || b.IsCurrent).ForEach(b => seen[b.Name] = TipOf(repo, b));
        return seen;
    }

    public static bool IsSame(IReadOnlyDictionary<string, string> a, IReadOnlyDictionary<string, string> b) =>
        a.Count == b.Count && a.All(t => b.TryGetValue(t.Key, out var v) && v == t.Value);

    // Every tip taken as seen, for the first time and for 'Mark All as Seen'
    public static Dictionary<string, string> AllSeen(Repo repo) =>
        NewsBranches(repo).ToDictionary(b => b.Name, b => TipOf(repo, b));

    // A branch that is new by name, but is one the user has seen under its other name: a local branch
    // pushed from a terminal is the remote branch it now has, and seen as far as the local one was; a
    // local branch whose remote branch was deleted, e.g. once its pull request was merged, is what is
    // left of that, and its own commits are the user's.
    static string? CarriedOverTip(Repo repo, Branch b, IReadOnlyDictionary<string, string> seenTips)
    {
        if (b.IsRemote)
            return b.LocalName != "" && seenTips.TryGetValue(b.LocalName, out var localTip) ? localTip : null;

        return seenTips.ContainsKey($"origin/{b.Name}") ? TipOf(repo, b) : null;
    }

    static int NewCount(Repo repo, Branch branch, string? seenTip)
    {
        var count = 0;
        var id = TipOf(repo, branch);
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

    // The tip of a branch in git, i.e. for the current branch with uncommitted changes the commit they
    // are on rather than the uncommitted row, which would make every edit a change of tip
    static string TipOf(Repo repo, Branch b) =>
        b.TipId == Repo.UncommittedId && repo.CommitById.TryGetValue(b.TipId, out var uncommitted)
            ? uncommitted.ParentIds[0]
            : b.TipId;

    // The remote branches, and the local ones with no remote branch. The DETACHED head is a git
    // branch too, but none to tell of.
    static IEnumerable<Branch> NewsBranches(Repo repo) =>
        repo.AllBranches.Where(b => b.IsGitBranch && (b.IsRemote || (b.RemoteName == "" && !b.IsDetached)));
}
