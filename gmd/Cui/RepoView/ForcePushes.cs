using gmd.Server;

namespace gmd.Cui.RepoView;

// What is said of a remote branch a force push rewrote (Repo.RemoteRewrites): on the status line when
// it is found, and by the commands that act on it. Plain functions, so they are testable without a
// view.
static class ForcePushes
{
    // The rewrite of a local branch, by a branch of either side: a remote branch is its local one's
    public static RemoteRewrite? RewriteOf(Repo repo, Branch branch)
    {
        var name = branch.IsRemote ? branch.LocalName : branch.Name;
        return name != "" && repo.RemoteRewrites.TryGetValue(name, out var rewrite) ? rewrite : null;
    }

    // Said once, when a refresh first finds it, since the graph shows it only as a branch that has
    // diverged, i.e. as new commits on both sides
    public static string Found(RemoteRewrite r)
    {
        var dropped = r.DroppedIds.Count > 0 ? $", dropping {Commits(r.DroppedIds.Count)}" : "";
        var pull = r.OwnCount > 0 ? $"Pull moves your {Commits(r.OwnCount)} onto it" : "Pull takes the new version";
        return $"'{r.RemoteName}' was rewritten by a force push{dropped}: {pull}";
    }

    // The same for several found at once, which do not fit the status line one by one
    public static string Found(IReadOnlyList<RemoteRewrite> rewrites) =>
        rewrites.Count == 1
            ? Found(rewrites[0])
            : $"{BranchPushPullCommands.Names(rewrites.Select(r => r.RemoteName))} were rewritten by force pushes: "
                + "pull each to take the new version";

    // Asked before a pull, which then does not do what pull.rebase says: it moves the branch's own
    // commits onto the new version, as 'git pull --rebase' would, and leaves the old version out
    public static string PullQuestion(Repo repo, RemoteRewrite r)
    {
        var by = r.IsByYou ? " from here" : "";
        var pull =
            r.OwnCount > 0
                ? $"Pull moves your {Commits(r.OwnCount)} onto the new version"
                : "Pull takes the new version";
        var question =
            $"'{r.RemoteName}' was rewritten by a force push{by},\n"
            + $"so '{r.BranchName}' has the old version of it.\n\n"
            + $"{pull}, and leaves out\n"
            + $"the {Commits(r.OldCopyCount)} of the old one, which Recover Lost Commits finds.\n"
            + "It does not merge the two, which would put every commit in twice.";
        if (r.DroppedIds.Count == 0)
            return question;

        var dropped = r.DroppedIds.Take(MaxListed).Select(id => $"  {id.Sid()} {SubjectOf(repo, id)}");
        var more = r.DroppedIds.Count > MaxListed ? $"\n  and {r.DroppedIds.Count - MaxListed} more" : "";
        return question
            + $"\n\nThe force push dropped {Commits(r.DroppedIds.Count)}, which the new version does not have:\n"
            + string.Join("\n", dropped)
            + more
            + $"\nIf that was a mistake, Restore {r.RemoteName} in the branch menu puts it back.";
    }

    // What the status line says once it is pulled
    public static string Pulled(RemoteRewrite r) =>
        r.OwnCount > 0
            ? $"Moved your {Commits(r.OwnCount)} onto the rewritten '{r.RemoteName}'"
            : $"Updated '{r.BranchName}' to the rewritten '{r.RemoteName}'";

    // Why a branch that is not checked out is not pulled: its own commits have to be moved, which
    // only a branch that is checked out can have done to it
    public static string WhyNoPull(RemoteRewrite r) =>
        $"'{r.RemoteName}' was rewritten by a force push: switch to '{r.BranchName}', and pull moves your "
        + $"{Commits(r.OwnCount)} onto it";

    // The push question when origin was rewritten: a force push from here would undo that for everyone
    public static string PushWarning(RemoteRewrite r) =>
        $"'{r.RemoteName}' was rewritten by a force push,\n"
        + $"and '{r.BranchName}' has the old version of it.\n\n"
        + "Force Push puts the old version back on origin, dropping\n"
        + "the new one for everyone. Pull first, which moves your\n"
        + "commits onto the new version, and then push.";

    // The menu item that puts origin back
    public static string RestoreLabel(RemoteRewrite r) => $"Restore {r.RemoteName} from before the Force Push ...";

    // Why origin is not put back: what was pushed on top of the rewrite would be lost with it
    public static string WhyNoRestore(RemoteRewrite r) =>
        $"Commits were pushed to '{r.RemoteName}' after the force push: putting it back would drop them";

    // Asked before origin is put back, which is a force push of its own, for everyone
    public static string RestoreQuestion(Repo repo, RemoteRewrite r) =>
        $"Put '{r.RemoteName}' back as it was before the force push?\n\n"
        + $"It gets {r.OldTipId.Sid()} {SubjectOf(repo, r.OldTipId)} again, and the {Commits(r.NewCount)}\n"
        + "of the new version leave it. This is a force push too: everyone\n"
        + "who pulled the new version has to deal with it in turn.";

    // What the status line says once origin is put back
    public static string Restored(RemoteRewrite r) =>
        $"Put '{r.RemoteName}' back at {r.OldTipId.Sid()}, as it was before the force push";

    const int MaxListed = 5;

    static string SubjectOf(Repo repo, string id) => repo.CommitById.TryGetValue(id, out var c) ? c.Subject : "";

    static string Commits(int count) => count == 1 ? "1 commit" : $"{count} commits";
}

// Which force pushes on origin have been told on the status line, so that each is told once, when a
// refresh first finds it. One made by a push from here is not told: the undo or the push that made it
// says what it did.
class ForcePushNotes
{
    readonly HashSet<(string Branch, string Tip)> told = [];

    // What to say of the rewrites of a shown repo not told yet, or null. All of them at once, since
    // each is marked as told: one fetch can bring force pushes on several branches.
    public string? Untold(Repo repo)
    {
        List<RemoteRewrite> untold = [];
        foreach (var r in repo.RemoteRewrites.Values.OrderBy(r => r.BranchName))
        {
            if (told.Add((r.BranchName, r.NewTipId)) && !r.IsByYou)
                untold.Add(r);
        }
        return untold.Count > 0 ? ForcePushes.Found(untold) : null;
    }
}
