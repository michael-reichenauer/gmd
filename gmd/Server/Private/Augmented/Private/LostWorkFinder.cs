using gmd.Git;
using GitCommit = gmd.Git.Commit;

namespace gmd.Server.Private.Augmented.Private;

// The lost commits made into the lines of work the user lost, and told apart: which branch each was
// made on and what took it out of the history, from the reflogs, and whether it is only an older
// version of work that is still there. No git and no view, so it is testable as it is.
static class LostWorkFinder
{
    const string BranchRefPrefix = "refs/heads/";

    // The lines of work, newest first, the older versions of work still there last. 'lost' is what
    // no ref reaches (ILogService.GetUnreachableCommitsAsync), newest first, and 'reachable' the
    // commits that are still there, by author and author time, to tell the older versions by.
    public static IReadOnlyList<LostWork> Find(
        IReadOnlyList<ReflogEntry> reflog,
        IReadOnlyList<GitCommit> lost,
        IEnumerable<(string Author, DateTime AuthorTime)> reachable,
        IReadOnlySet<string> localBranchNames
    )
    {
        Dictionary<string, GitCommit> byId = [];
        foreach (var c in lost)
        {
            byId.TryAdd(c.Id, c);
        }
        var orderById = lost.Select((c, i) => (c.Id, i)).DistinctBy(p => p.Id).ToDictionary(p => p.Id, p => p.i);
        var parentsOfLost = lost.SelectMany(c => c.ParentIds).Where(byId.ContainsKey).ToHashSet();
        var keptVersions = reachable.ToHashSet();
        var madeOn = ReflogWitness.MadeOn(reflog);
        var logs = reflog.GroupBy(e => e.Ref).ToDictionary(g => g.Key, g => g.OrderBy(e => e.Index).ToList());

        return byId
            .Values.Where(c => !parentsOfLost.Contains(c.Id))
            .Select(tip =>
            {
                var ids = LineOf(tip, byId, orderById);
                var branchName =
                    ids.Select(id => madeOn.GetValueOrDefault(id)).FirstOrDefault(n => n != null)
                    ?? BranchMentioning(tip.Id, logs)
                    ?? "";
                var lostBy =
                    branchName != "" && !localBranchNames.Contains(branchName)
                        ? LostBy.Deleted
                        : LostByOf(tip.Id, logs);
                var isRewritten = ids.All(id => keptVersions.Contains((byId[id].Author, byId[id].AuthorTime)));

                return new LostWork(
                    tip.Id,
                    ids,
                    OldestOf(tip, byId),
                    tip.CommitTime,
                    tip.Subject,
                    tip.Author,
                    branchName,
                    lostBy,
                    isRewritten
                );
            })
            .OrderBy(w => w.IsRewritten)
            .ThenByDescending(w => w.Time)
            .ToList();
    }

    // The tip and every lost commit under it, through all parents, in the order git listed them
    static IReadOnlyList<string> LineOf(
        GitCommit tip,
        IReadOnlyDictionary<string, GitCommit> byId,
        IReadOnlyDictionary<string, int> orderById
    )
    {
        HashSet<string> line = [tip.Id];
        Stack<GitCommit> toVisit = new([tip]);
        while (toVisit.TryPop(out var c))
        {
            foreach (var parentId in c.ParentIds)
            {
                if (byId.TryGetValue(parentId, out var parent) && line.Add(parentId))
                    toVisit.Push(parent);
            }
        }

        return line.OrderBy(id => id == tip.Id ? -1 : orderById[id]).ToList();
    }

    // The oldest lost commit on the tip's first parents, i.e. where the line left the history that
    // is still there
    static string OldestOf(GitCommit tip, IReadOnlyDictionary<string, GitCommit> byId)
    {
        var c = tip;
        HashSet<string> seen = [tip.Id];
        while (c.ParentIds.Length > 0 && byId.TryGetValue(c.ParentIds[0], out var parent) && seen.Add(parent.Id))
        {
            c = parent;
        }
        return c.Id;
    }

    // A local branch whose reflog has the commit, i.e. one that was once at it, even if the commit
    // was not made on it, e.g. a reset or a pull took the branch there
    static string? BranchMentioning(string id, IReadOnlyDictionary<string, List<ReflogEntry>> logs) =>
        logs.FirstOrDefault(l => l.Key.StartsWith(BranchRefPrefix) && l.Value.Any(e => e.Id == id)).Key?[
            BranchRefPrefix.Length..
        ];

    // What moved away from the tip for the last time: in the reflog of a branch that was at it, if any
    // still is, else in HEAD's, where a checkout away from it means it was made on a detached HEAD
    static LostBy LostByOf(string tipId, IReadOnlyDictionary<string, List<ReflogEntry>> logs)
    {
        var branchLogs = logs.Where(l => l.Key.StartsWith(BranchRefPrefix)).Select(l => l.Value);
        var headLogs = logs.Where(l => IsHeadRef(l.Key)).Select(l => l.Value);

        foreach (var log in branchLogs.Concat(headLogs))
        {
            if (MoveAwayFrom(tipId, log) is not ReflogEntry move)
                continue;
            if (move.Message.StartsWith("checkout:"))
                return LostBy.Detached;

            return ReflogSteps.KindOf(move.Message).Kind switch
            {
                StepKind.Reset or StepKind.Uncommit => LostBy.Reset,
                StepKind.Rebase => LostBy.Rebase,
                StepKind.Amend => LostBy.Amend,
                StepKind.Pull => LostBy.Pull,
                _ => LostBy.Other,
            };
        }
        return LostBy.Other;
    }

    // The entry that moved a ref away from the commit the last time it was there, i.e. the next
    // newer one at another commit, in a log latest first
    static ReflogEntry? MoveAwayFrom(string id, List<ReflogEntry> log)
    {
        var at = log.FindIndex(e => e.Id == id);
        return at > 0 ? log[at - 1] : null;
    }

    // HEAD's reflog and every other worktree's HEAD's ('worktrees/x/HEAD'), as ReflogWitness reads them
    static bool IsHeadRef(string reference) =>
        reference == "HEAD" || (reference.EndsWith("/HEAD") && !reference.StartsWith("refs/"));
}
