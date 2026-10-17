using gmd.Server;

namespace gmd.Cui.RepoView;

// What is said of a branch rewritten here after it was pushed (Repo.LocalRewrites), e.g. its commits
// squashed: origin still has the old version, and the branch looks diverged from it, but there is
// nothing to pull, which would bring the old version back. It needs a force push, which 'p' asks
// about. The mirror of ForcePushes, and plain functions too, so they are testable without a view.
static class RewrittenHere
{
    const int MaxListed = 5;

    // The rewrite of a local branch, by a branch of either side: a remote branch is its local one's
    public static LocalRewrite? Of(Repo repo, Branch branch)
    {
        var name = branch.IsRemote ? branch.LocalName : branch.Name;
        return name != "" && repo.LocalRewrites.TryGetValue(name, out var rewrite) ? rewrite : null;
    }

    // Said once, when a refresh first finds it, e.g. right after a squash of commits on origin
    public static string Found(LocalRewrite r) =>
        $"'{r.BranchName}' was rewritten here{By(r, isNamed: false)}: p pushes it with force, over origin's old version";

    // The same for several found at once
    public static string Found(IReadOnlyList<LocalRewrite> rewrites) =>
        rewrites.Count == 1
            ? Found(rewrites[0])
            : $"{BranchPushPullCommands.Names(rewrites.Select(r => r.BranchName))} were rewritten here: p pushes "
                + "each with force, over origin's old version";

    // Asked before the force push, naming what it replaces on origin, for everyone
    public static string PushQuestion(Repo repo, LocalRewrite r)
    {
        var old = r.OldIds.Take(MaxListed).Select(id => $"  {id.Sid()} {SubjectOf(repo, id)}");
        var more = r.OldIds.Count > MaxListed ? $"\n  and {r.OldIds.Count - MaxListed} more" : "";
        var them = r.OldIds.Count == 1 ? "it" : "them";
        return $"'{r.BranchName}' was rewritten here{By(r, isNamed: true)},\n"
            + $"and origin still has the {Commits(r.OldIds.Count)} it replaced:\n\n"
            + string.Join("\n", old)
            + more
            + $"\n\nForce Push replaces {them} on origin with the {Commits(r.NewCount)} here,\n"
            + "for everyone who has the branch. Recover Lost Commits\n"
            + $"finds {them} afterwards.";
    }

    // What the status line says once it is pushed
    public static string Pushed(LocalRewrite r) =>
        $"Pushed '{r.BranchName}' with force, replacing the old version on origin";

    // Why it is not pulled: origin has only the old version, and a pull would merge it back in, or
    // rebase the rewrite onto it, which undoes it
    public static string WhyNoPull(LocalRewrite r) =>
        $"Nothing to pull on '{r.BranchName}', rewritten here{By(r, isNamed: false)}: p pushes it with force";

    // Why it is not rebased: the rebase's push is leased on origin's tip, which then has commits the
    // branch has not, and Rebase onto asks for those to be pulled first, which is wrong here
    public static string WhyNoRebase(LocalRewrite r) =>
        $"'{r.BranchName}' was rewritten here{By(r, isNamed: false)}: push it with force first (p), then rebase it";

    // The branches Push All leaves to be pushed one by one, since each is a force push, asked first
    public static string LeftOut(IReadOnlyList<LocalRewrite> rewrites) =>
        rewrites.Count == 1
            ? $"'{rewrites[0].BranchName}' was rewritten here: push it on its own (p), which asks to force it"
            : $"{BranchPushPullCommands.Names(rewrites.Select(r => r.BranchName))} were rewritten here: "
                + "push each on its own (p), which asks to force it";

    // What rewrote it, when the reflog or gmd's own record says: a move that took the branch off a
    // commit is one of these, and the others, e.g. a commit, move a branch on. What it was of, e.g. an
    // amend's subject, only where there is room, which the status line has not: the row shows it.
    static string By(LocalRewrite r, bool isNamed) =>
        r.Kind
            is StepKind.Squash
                or StepKind.Amend
                or StepKind.Rebase
                or StepKind.Reset
                or StepKind.Uncommit
                or StepKind.Drop
                or StepKind.Move
            ? $" by {BranchUndo.Phrase(r.Kind, isNamed ? r.Name : "")}"
            : "";

    static string SubjectOf(Repo repo, string id) => repo.CommitById.TryGetValue(id, out var c) ? c.Subject : "";

    static string Commits(int count) => count == 1 ? "1 commit" : $"{count} commits";
}
