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

    static string Commits(int count) => count == 1 ? "1 commit" : $"{count} commits";
}

// Which force pushes on origin have been told on the status line, so that each is told once, when a
// refresh first finds it. One made by a push from here is not told: the undo or the push that made it
// says what it did.
class ForcePushNotes
{
    readonly HashSet<(string Branch, string Tip)> told = [];

    // What to say of the rewrites of a shown repo not told yet, or null
    public string? Untold(Repo repo)
    {
        string? note = null;
        foreach (var r in repo.RemoteRewrites.Values.OrderBy(r => r.BranchName))
        {
            if (told.Add((r.BranchName, r.NewTipId)) && !r.IsByYou)
                note ??= ForcePushes.Found(r);
        }
        return note;
    }
}
