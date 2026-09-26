using gmd.Git;

namespace gmd.Server.Private.Augmented.Private;

// What the reflog witnessed about the branches of commits, which git records nowhere else: the
// branch a commit was made on, and the branch a branch was started from. Only what the reflog says
// for certain, as far back as it goes: it is local and expires, so most commits have no fact at all.
//
// A branch's own reflog says it for the commits made on it ('commit: ...', 'merge x: Merge made by
// ...'); HEAD's says it for the branches since deleted, whose reflogs went with them, by following
// which branch was checked out ('checkout: moving from a to b') when each commit was made. Moves
// that made no commit (a fast-forward, a reset, a rebase) say nothing, and neither do the commits a
// rebase makes, on a detached HEAD. Branch names are the local names, which are the nice names of
// both the local and the remote branch.
static class ReflogWitness
{
    const string BranchRefPrefix = "refs/heads/";
    const string CreatedFromPrefix = "branch: Created from ";
    const string CheckoutPrefix = "checkout: moving from ";

    // The branch each commit is witnessed on, by commit id: the branch it was made on, and failing
    // that the branch whose commit it was when another branch was started from it, i.e. where
    // 'git checkout -b feature' on dev left feature, which is dev's commit
    public static IReadOnlyDictionary<string, string> BranchByCommit(IReadOnlyList<ReflogEntry> entries)
    {
        var branches = MadeOn(entries).ToDictionary();
        foreach (var (id, name) in StartedFrom(entries))
        {
            branches.TryAdd(id, name);
        }
        return branches;
    }

    // The branch each commit was made on, by commit id
    public static IReadOnlyDictionary<string, string> MadeOn(IReadOnlyList<ReflogEntry> entries)
    {
        Dictionary<string, string> madeOn = [];

        foreach (var e in entries.Where(e => e.Ref.StartsWith(BranchRefPrefix) && IsCommitMade(e.Message)))
        {
            madeOn.TryAdd(e.Id, e.Ref[BranchRefPrefix.Length..]);
        }

        foreach (var head in HeadReflogs(entries))
        {
            string? current = InitialBranch(head);
            foreach (var e in head)
            {
                if (e.Message.StartsWith(CheckoutPrefix))
                {
                    current = BranchOrNull(e.Message[(e.Message.LastIndexOf(" to ") + 4)..]);
                }
                else if (e.Message.StartsWith("rebase") && e.Message.Contains("returning to " + BranchRefPrefix))
                {
                    current = e.Message[(e.Message.IndexOf("returning to ") + 13 + BranchRefPrefix.Length)..];
                }
                else if (e.Message.StartsWith("rebase"))
                {
                    current = null; // A rebase runs on a detached HEAD
                }
                else if (current != null && IsCommitMade(e.Message))
                {
                    madeOn.TryAdd(e.Id, current);
                }
            }
        }

        return madeOn;
    }

    // The branch a branch was started from, keyed by the commit it was started at, which is that
    // branch's commit. 'Created from HEAD' is what 'git checkout -b' and 'git switch -c' write; HEAD's
    // reflog then has the checkout to the new branch at the same commit, and says from which branch.
    // A branch started from a commit id or an expression ('main~2') says nothing about a branch.
    public static IReadOnlyDictionary<string, string> StartedFrom(IReadOnlyList<ReflogEntry> entries)
    {
        Dictionary<string, string> startedFrom = [];
        var heads = HeadReflogs(entries).ToList();

        foreach (
            var e in entries.Where(e => e.Ref.StartsWith(BranchRefPrefix) && e.Message.StartsWith(CreatedFromPrefix))
        )
        {
            var branch = e.Ref[BranchRefPrefix.Length..];
            var source = e.Message[CreatedFromPrefix.Length..];

            var name =
                source == "HEAD"
                    ? heads
                        .SelectMany(h => h)
                        .Where(h =>
                            h.Id == e.Id && h.Message.StartsWith(CheckoutPrefix) && h.Message.EndsWith(" to " + branch)
                        )
                        .Select(h => BranchOrNull(h.Message[CheckoutPrefix.Length..h.Message.LastIndexOf(" to ")]))
                        .FirstOrDefault()
                    : BranchOrNull(source);
            if (name != null && name != branch)
            {
                startedFrom.TryAdd(e.Id, name);
            }
        }

        return startedFrom;
    }

    // Whether a reflog entry is a commit being made, rather than the ref moving to one that exists
    static bool IsCommitMade(string message) =>
        message.StartsWith("commit")
        || message.StartsWith("cherry-pick")
        || message.StartsWith("revert")
        || message.Contains(": Merge made by");

    // The reflogs of HEAD and of every other worktree's HEAD, each oldest first
    static IEnumerable<List<ReflogEntry>> HeadReflogs(IReadOnlyList<ReflogEntry> entries) =>
        entries
            .Where(e => e.Ref == "HEAD" || e.Ref.EndsWith("/HEAD"))
            .GroupBy(e => e.Ref)
            .Select(g => g.OrderByDescending(e => e.Index).ToList());

    // The branch checked out before the first checkout in a HEAD reflog, which is where the reflog
    // starts, e.g. with the first commit or a clone
    static string? InitialBranch(List<ReflogEntry> head)
    {
        var first = head.FirstOrDefault(e => e.Message.StartsWith(CheckoutPrefix));
        return first == null
            ? null
            : BranchOrNull(first.Message[CheckoutPrefix.Length..first.Message.LastIndexOf(" to ")]);
    }

    // The local name of a branch named in a reflog message, or null for anything that is no branch
    // name: a commit id (a detached HEAD) or an expression such as 'main~2'
    static string? BranchOrNull(string name)
    {
        if (name == "" || name == "HEAD" || name.Any(ch => ch is '~' or '^' or ':' or '@' or '{' or ' '))
            return null;
        if (name.Length >= 7 && name.All(Uri.IsHexDigit))
            return null;

        foreach (var prefix in new[] { BranchRefPrefix, "refs/remotes/origin/", "origin/" })
        {
            if (name.StartsWith(prefix))
                return name[prefix.Length..];
        }
        return name;
    }
}
