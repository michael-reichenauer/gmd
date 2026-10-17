using gmd.Common;
using gmd.Git;
using GitCommit = gmd.Git.Commit;

namespace gmd.Server.Private.Augmented.Private;

// The local branches rewritten here since they were pushed, while origin still has the old version:
// a squash, an amend, a rebase or a reset of commits that were on origin, in gmd or not. Told by the
// branch's own reflog: the branch was once where its remote branch is now, and is not now, so
// something moved it off that commit rather than on from it. A branch that is only behind has not
// been rewritten, or its rewrite only dropped commits, which a pull brings back as before, so only a
// branch that has diverged from its remote branch is looked at, which is rare: for nearly every repo
// nothing is walked. One whose remote branch a force push rewrote (RemoteRewrites) is that instead,
// e.g. gmd's Rebase pushed and then undone here. No git and no view here.
//
// Not told: origin having commits pushed since the branch was last there, e.g. someone else's, since
// origin's tip is then a commit the branch never was at, and a force push would drop them. The branch
// is then shown as diverged, as before.
static class LocalRewrites
{
    public static IReadOnlyDictionary<string, LocalRewrite> Find(
        GitRepo gitRepo,
        IReadOnlyDictionary<string, RemoteRewrite> remoteRewrites
    )
    {
        Dictionary<string, LocalRewrite> rewrites = [];
        var diverged = gitRepo
            .Branches.Where(b =>
                !b.IsRemote
                && !b.IsDetached
                && b.RemoteName != ""
                && b.AheadCount > 0
                && b.BehindCount > 0
                && !remoteRewrites.ContainsKey(b.Name)
            )
            .ToList();
        if (diverged.Count == 0)
            return rewrites;

        var remoteTips = gitRepo.Branches.Where(b => b.IsRemote).ToDictionary(b => b.Name, b => b.TipID);
        var reflogs = ReflogSteps.BranchReflogs(gitRepo.Reflog);
        Dictionary<string, (GitCommit Commit, int Index)>? byId = null;

        foreach (var b in diverged)
        {
            // The reflog has to say where the branch is, as Undo needs it to, or it is not its history
            if (
                !remoteTips.TryGetValue(b.RemoteName, out var remoteTip)
                || !reflogs.TryGetValue(b.Name, out var log)
                || log.Count < 2
                || log[0].Id != b.TipID
            )
                continue;

            // Where the branch was at origin's tip, the newest time it was. The current entry is not,
            // since the branch is behind, i.e. does not have that commit.
            var at = IndexOf(log, remoteTip);
            if (at < 1)
                continue;

            if (byId == null)
            {
                byId = [];
                for (var i = 0; i < gitRepo.Commits.Count; i++)
                {
                    byId.TryAdd(gitRepo.Commits[i].Id, (gitRepo.Commits[i], i));
                }
            }
            if (!byId.ContainsKey(remoteTip) || !byId.ContainsKey(b.TipID))
                continue;

            var moveOff = MoveOff(log, at, remoteTip, byId);
            var (kind, name) = KindOf(gitRepo, b.Name, log, moveOff);
            rewrites[b.Name] = new LocalRewrite(
                b.Name,
                b.RemoteName,
                b.TipID,
                remoteTip,
                kind,
                name,
                b.AheadCount,
                OldIds(remoteTip, b.TipID, byId)
            );
        }
        return rewrites;
    }

    static int IndexOf(IReadOnlyList<ReflogEntry> log, string id)
    {
        for (var i = 0; i < log.Count; i++)
        {
            if (log[i].Id == id)
                return i;
        }
        return -1;
    }

    // The entry of the move that took the branch off origin's tip: the first after it to a commit that
    // does not have that tip, since the commits made on it since are on it still. A commit the log no
    // longer has, e.g. one that an amend replaced in turn, is passed over: it is not where the branch
    // went in the end. The latest entry, where the branch is now, never has the tip, so there is one.
    static int MoveOff(
        IReadOnlyList<ReflogEntry> log,
        int at,
        string remoteTip,
        IReadOnlyDictionary<string, (GitCommit Commit, int Index)> byId
    )
    {
        for (var j = at - 1; j > 0; j--)
        {
            if (byId.ContainsKey(log[j].Id) && !Reaches(log[j].Id, remoteTip, byId))
                return j;
        }
        return 0;
    }

    // What the move was, as Undo names it: gmd's own record of a squash, an amend or a drop, whose
    // moves end with this one and started at the commit before them, or else the reflog's message
    static (StepKind, string) KindOf(GitRepo gitRepo, string branchName, IReadOnlyList<ReflogEntry> log, int moveOff)
    {
        if (
            gitRepo.RecordedSteps.TryGetValue(branchName, out var recorded)
            && !recorded.IsRedo
            && recorded.Moves >= 1
            && moveOff + recorded.Moves < log.Count
            && log[moveOff + recorded.Moves].Id == recorded.BeforeId
            && Enum.TryParse<StepKind>(recorded.Kind, out var recordedKind)
        )
            return (recordedKind, recorded.Name);
        return ReflogSteps.KindOf(log[moveOff].Message);
    }

    // Whether a commit is the other, or has it among its ancestors. The log lists every commit before
    // its parents, so a commit listed after the other cannot have it, and the walk goes no further
    // there: it only goes through the commits between the two.
    static bool Reaches(string fromId, string toId, IReadOnlyDictionary<string, (GitCommit Commit, int Index)> byId)
    {
        var toIndex = byId[toId].Index;
        HashSet<string> visited = [];
        Stack<string> toVisit = new([fromId]);
        while (toVisit.TryPop(out var id))
        {
            if (id == toId)
                return true;
            if (!byId.TryGetValue(id, out var c) || c.Index > toIndex || !visited.Add(id))
                continue;
            foreach (var parentId in c.Commit.ParentIds)
            {
                toVisit.Push(parentId);
            }
        }
        return false;
    }

    // Origin's commits that the branch does not have, newest first, as the log lists them
    static IReadOnlyList<string> OldIds(
        string remoteTip,
        string localTip,
        IReadOnlyDictionary<string, (GitCommit Commit, int Index)> byId
    )
    {
        var inLocal = Ancestors(localTip, byId, new HashSet<string>());
        return Ancestors(remoteTip, byId, inLocal).OrderBy(id => byId[id].Index).ToList();
    }

    // A commit and every commit it was made on, as far as the log goes, but not into the ones given
    static HashSet<string> Ancestors(
        string id,
        IReadOnlyDictionary<string, (GitCommit Commit, int Index)> byId,
        IReadOnlySet<string> stopAt
    )
    {
        HashSet<string> ancestors = [];
        Stack<string> toVisit = new([id]);
        while (toVisit.TryPop(out var current))
        {
            if (stopAt.Contains(current) || !byId.TryGetValue(current, out var c) || !ancestors.Add(current))
                continue;
            foreach (var parentId in c.Commit.ParentIds)
            {
                toVisit.Push(parentId);
            }
        }
        return ancestors;
    }
}
