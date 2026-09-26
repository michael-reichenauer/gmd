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
        foreach (var c in Creations(entries))
        {
            startedFrom.TryAdd(c.Id, c.Source);
        }
        return startedFrom;
    }

    // The branch each branch was started from, by the branch's name, from the same entries: its own
    // reflog's oldest, which goes first when the reflog expires
    public static IReadOnlyDictionary<string, string> SourceByBranch(IReadOnlyList<ReflogEntry> entries)
    {
        Dictionary<string, string> sources = [];
        foreach (var c in Creations(entries))
        {
            sources.TryAdd(c.Branch, c.Source);
        }
        return sources;
    }

    // A branch started at a commit from another branch, by name
    record Creation(string Id, string Branch, string Source);

    // The branches whose reflog says which branch they were started from
    static IEnumerable<Creation> Creations(IReadOnlyList<ReflogEntry> entries)
    {
        var creatingCheckouts = CreatingCheckouts(entries);

        foreach (
            var e in entries.Where(e => e.Ref.StartsWith(BranchRefPrefix) && e.Message.StartsWith(CreatedFromPrefix))
        )
        {
            var branch = e.Ref[BranchRefPrefix.Length..];
            var source = e.Message[CreatedFromPrefix.Length..];

            var name = source == "HEAD" ? creatingCheckouts.GetValueOrDefault((e.Id, branch)) : BranchOrNull(source);
            if (name != null && name != branch)
            {
                yield return new Creation(e.Id, branch, name);
            }
        }
    }

    // The branch HEAD was on when a branch was created from it, by the commit and the created
    // branch: the first checkout to the branch at that commit, if it left HEAD's commit where it
    // was, as 'git checkout -b' and 'git switch -c' do. Any other checkout to it is no creation, e.g.
    // from hotfix to a feature made before with 'git branch feature HEAD', which checks nothing out,
    // and HEAD was not on hotfix then. Found in one pass over the HEAD reflogs, rather than a search
    // of them for each branch created.
    static Dictionary<(string Id, string Branch), string?> CreatingCheckouts(IReadOnlyList<ReflogEntry> entries)
    {
        Dictionary<(string, string), string?> sources = [];
        foreach (var head in HeadReflogs(entries))
        {
            for (var i = 0; i < head.Count; i++)
            {
                var e = head[i];
                var to = e.Message.LastIndexOf(" to ");
                if (!e.Message.StartsWith(CheckoutPrefix) || to < CheckoutPrefix.Length)
                    continue;

                var key = (e.Id, e.Message[(to + 4)..]);
                if (sources.ContainsKey(key))
                    continue; // A later checkout to the branch, the first is the one that created it

                var isCommitKept = i == 0 || head[i - 1].Id == e.Id;
                sources[key] = isCommitKept ? BranchOrNull(e.Message[CheckoutPrefix.Length..to]) : null;
            }
        }
        return sources;
    }

    // Whether a reflog entry is a commit being made, rather than the ref moving to one that exists
    static bool IsCommitMade(string message) =>
        message.StartsWith("commit")
        || message.StartsWith("cherry-pick")
        || message.StartsWith("revert")
        || message.Contains(": Merge made by");

    // The reflogs of HEAD and of every other worktree's HEAD ('worktrees/x/HEAD', 'main-worktree/HEAD'),
    // each oldest first. Not a ref under 'refs/' that ends in HEAD, e.g. a remote's 'origin/HEAD'.
    static IEnumerable<List<ReflogEntry>> HeadReflogs(IReadOnlyList<ReflogEntry> entries) =>
        entries
            .Where(e => e.Ref == "HEAD" || (e.Ref.EndsWith("/HEAD") && !e.Ref.StartsWith("refs/")))
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
