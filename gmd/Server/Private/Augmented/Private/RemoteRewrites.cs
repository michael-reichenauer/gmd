using gmd.Git;
using GitCommit = gmd.Git.Commit;

namespace gmd.Server.Private.Augmented.Private;

// The remote branches a force push rewrote since their local branches were built on them. Told by
// the remote branch's reflog, which says where origin was each time it was fetched or pushed: the
// local branch was built on one of those, its fork point, as 'git pull --rebase' finds it, and when
// that is no longer in the remote branch's history, the remote branch was rewritten. Only the reflogs
// of the branches with commits not on origin are read (AugmentedService), since a branch with none
// has nothing of the old version to put back, and with none, or an expired one, there is no fork
// point and nothing is said. No git and no view here.
static class RemoteRewrites
{
    const string RemoteRefPrefix = "refs/remotes/";

    public static IReadOnlyDictionary<string, RemoteRewrite> Find(GitRepo gitRepo)
    {
        Dictionary<string, RemoteRewrite> rewrites = [];
        if (gitRepo.RemoteReflog.Count == 0)
            return rewrites;

        Dictionary<string, GitCommit> commitById = [];
        foreach (var c in gitRepo.Commits)
        {
            commitById.TryAdd(c.Id, c);
        }
        var remoteTips = gitRepo.Branches.Where(b => b.IsRemote).ToDictionary(b => b.Name, b => b.TipID);
        var logs = gitRepo
            .RemoteReflog.GroupBy(e => e.Ref)
            .ToDictionary(g => g.Key, g => g.OrderBy(e => e.Index).ToList());

        foreach (var b in gitRepo.Branches.Where(b => !b.IsRemote && !b.IsDetached && b.RemoteName != ""))
        {
            if (
                !remoteTips.TryGetValue(b.RemoteName, out var remoteTip)
                || !logs.TryGetValue(RemoteRefPrefix + b.RemoteName, out var log)
            )
                continue;
            if (Find(b, remoteTip, log, gitRepo.Commits, commitById) is RemoteRewrite rewrite)
                rewrites[b.Name] = rewrite;
        }
        return rewrites;
    }

    static RemoteRewrite? Find(
        Git.Branch local,
        string remoteTip,
        List<ReflogEntry> log,
        IReadOnlyList<GitCommit> commits,
        IReadOnlyDictionary<string, GitCommit> commitById
    )
    {
        // Where origin was before the rewrite: the newest place it was that it no longer has, and the
        // entry after it is the rewrite itself, a fetch of someone's force push or a push from here.
        // Found first, since there is nearly never one, and then nothing more is walked.
        var inRemote = Ancestors(remoteTip, commitById);
        var oldIndex = log.FindIndex(e => !inRemote.Contains(e.Id));
        if (oldIndex < 1)
            return null;
        var oldTip = log[oldIndex];
        var rewriteEntry = log[oldIndex - 1];

        // The newest place origin was that the local branch was built on; still in origin's history
        // means new commits on both sides, which is no rewrite
        var inLocal = Ancestors(local.TipID, commitById);
        var fork = log.FirstOrDefault(e => inLocal.Contains(e.Id));
        if (fork == null)
            return null;
        HashSet<string>? inFork = null;
        if (inRemote.Contains(fork.Id))
        {
            // Unless the force push only dropped commits, taking origin back to a commit the old
            // version was built on: the local branch then seems built on that, since it has every
            // commit of it, but it was built on the old version, which it has too. That is a rewrite
            // no pull shows, with nothing new to pull, while a push puts the dropped commits back.
            if (!inLocal.Contains(oldTip.Id))
                return null;
            inFork = Ancestors(oldTip.Id, commitById);
            if (!inFork.Contains(fork.Id))
                return null;
            fork = oldTip;
        }

        inFork ??= Ancestors(fork.Id, commitById);
        var oldCopies = inFork.Where(id => !inRemote.Contains(id)).ToList();
        var newIds = inRemote.Where(id => !inFork.Contains(id)).ToList();
        var newVersion = newIds.Select(id => KeyOf(commitById[id])).ToHashSet();
        var dropped = oldCopies.Where(id => !newVersion.Contains(KeyOf(commitById[id]))).ToList();

        return new RemoteRewrite(
            local.Name,
            local.RemoteName,
            local.TipID,
            fork.Id,
            oldTip.Id,
            remoteTip,
            OwnCount(commits, inLocal, inFork, fork.Id),
            oldCopies.Count,
            newIds.Count,
            dropped,
            rewriteEntry.Message.StartsWith("update by push"),
            oldIndex == 1
        );
    }

    // A commit and every commit it was made on, as far as the log goes
    static HashSet<string> Ancestors(string id, IReadOnlyDictionary<string, GitCommit> commitById)
    {
        HashSet<string> ancestors = [];
        Stack<string> toVisit = new([id]);
        while (toVisit.TryPop(out var current))
        {
            if (!commitById.TryGetValue(current, out var c) || !ancestors.Add(current))
                continue;
            foreach (var parentId in c.ParentIds)
            {
                toVisit.Push(parentId);
            }
        }
        return ancestors;
    }

    // The local branch's own commits, which a pull moves onto the new version: those made on the fork
    // point, or on one of them. Not every commit it has that the fork point has not: a merge of
    // another branch, e.g. main, brings that branch's commits too, which the rebase leaves as they are,
    // since --rebase-merges keeps a merged branch on its own base.
    static int OwnCount(
        IReadOnlyList<GitCommit> commits,
        IReadOnlySet<string> inLocal,
        IReadOnlySet<string> inFork,
        string forkId
    )
    {
        HashSet<string> own = [];
        // Oldest first, so a commit's parents come before it: the log lists none before its children
        for (var i = commits.Count - 1; i >= 0; i--)
        {
            var c = commits[i];
            if (
                inLocal.Contains(c.Id)
                && !inFork.Contains(c.Id)
                && c.ParentIds.Any(p => p == forkId || own.Contains(p))
            )
                own.Add(c.Id);
        }
        return own.Count;
    }

    // What a commit keeps when it is rewritten, which tells a copy of it
    static (string, DateTime) KeyOf(GitCommit c) => (c.Author, c.AuthorTime);
}
