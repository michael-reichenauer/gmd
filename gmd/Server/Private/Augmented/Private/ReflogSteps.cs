using System.Text.RegularExpressions;
using gmd.Git;

namespace gmd.Server.Private.Augmented.Private;

// What the moves of a branch were, as its own reflog says, which is what Undo takes back. A branch's
// reflog is the right one rather than HEAD's: a rebase or a pull is one entry there, however many
// commits it made, and git writes no entry for an update that leaves the branch where it was (a
// 'reset --hard' with no target, which Discard All Changes runs, writes one to HEAD's only).
static class ReflogSteps
{
    const string BranchRefPrefix = "refs/heads/";

    // What Uncommit resets to, 'HEAD~1', and the ways of saying the same of one or more commits
    static readonly Regex UncommitTarget = new Regex(@"^HEAD(~\d*|\^+)$", RegexOptions.Compiled);

    static readonly Regex MergeSubject = new Regex(
        @"^Merge (?:remote-tracking )?branch '([^']+)'",
        RegexOptions.Compiled
    );

    // The kind of change a branch reflog message records, and what it was of: the subject of the
    // commit, or the branch merged in. Messages are git's own, in English (Cmd.InEnglish), e.g.
    // 'commit (amend): Fix', 'merge feature: Fast-forward', 'pull: Merge made by the 'ort' strategy.',
    // 'fetch origin dev:dev: fast-forward' (gmd's pull of a branch that is not checked out), or
    // 'rebase (finish): refs/heads/dev onto <id>'. Options given to the command are part of it, as
    // in 'pull --rebase (finish): …', so only the leading word is relied on.
    public static (StepKind Kind, string Name) KindOf(string message)
    {
        var subject = Subject(message);

        if (message.StartsWith("commit (amend)"))
            return (StepKind.Amend, subject);
        if (message.StartsWith("commit (merge)"))
            return (StepKind.Merge, MergedBranch(subject));
        if (message.StartsWith("commit (cherry-pick)") || message.StartsWith("cherry-pick"))
            return (StepKind.CherryPick, subject);
        if (message.StartsWith("revert"))
            return (StepKind.Revert, Reverted(subject));
        if (message.StartsWith("commit"))
        { // gmd reverts with '--no-commit' and then commits, so its revert is a commit of git's message
            return IsRevertSubject(subject) ? (StepKind.Revert, Reverted(subject)) : (StepKind.Commit, subject);
        }
        if (message.StartsWith("merge "))
            return (StepKind.Merge, TrimRemote(message["merge ".Length..IndexOrEnd(message, ": ")]));
        if (message.StartsWith("pull") || message.StartsWith("fetch"))
            return (StepKind.Pull, "");
        if (message.StartsWith("rebase"))
            return (StepKind.Rebase, "");
        if (message.StartsWith("reset: moving to "))
        {
            var target = message["reset: moving to ".Length..];
            return (UncommitTarget.IsMatch(target) ? StepKind.Uncommit : StepKind.Reset, "");
        }
        if (message.StartsWith("undo: moving to ") || message.StartsWith("branch: Reset to "))
            return (StepKind.Reset, "");
        if (message.StartsWith("branch: Created from") || message.StartsWith("clone:"))
            return (StepKind.Created, "");
        if (message.StartsWith("Branch: renamed") || message.StartsWith("Branch: copied"))
            return (StepKind.Renamed, "");

        return (StepKind.Other, "");
    }

    // The entries of one ref's reflog, latest first, with each run of entries at the same commit
    // collapsed to the oldest of them: that one is the move, and the newer ones moved nothing, e.g. a
    // rename. A new list, since the entries are shared with the branch inference.
    public static IReadOnlyList<ReflogEntry> Collapse(IEnumerable<ReflogEntry> entries)
    {
        List<ReflogEntry> collapsed = [];
        foreach (var e in entries.OrderBy(e => e.Index))
        {
            if (collapsed.Count > 0 && collapsed[^1].Id == e.Id)
                collapsed[^1] = e;
            else
                collapsed.Add(e);
        }
        return collapsed;
    }

    // The reflog of each local branch, by branch name, latest first and collapsed
    public static IReadOnlyDictionary<string, IReadOnlyList<ReflogEntry>> BranchReflogs(
        IReadOnlyList<ReflogEntry> entries
    ) =>
        entries
            .Where(e => e.Ref.StartsWith(BranchRefPrefix))
            .GroupBy(e => e.Ref[BranchRefPrefix.Length..])
            .ToDictionary(g => g.Key, g => Collapse(g));

    static string Subject(string message)
    {
        var i = message.IndexOf(": ");
        return i < 0 ? "" : message[(i + 2)..];
    }

    // The branch a merge commit's subject names, e.g. 'Merge branch 'feature' into dev', or nothing
    // for any other subject, e.g. a pull request's, which names no branch git has
    static string MergedBranch(string subject) =>
        MergeSubject.Match(subject) is { Success: true } m ? TrimRemote(m.Groups[1].Value) : "";

    static bool IsRevertSubject(string subject) => subject.StartsWith("Revert \"") && subject.EndsWith('"');

    // 'Revert "Fix"' names the commit it reverts, 'Fix'
    static string Reverted(string subject) => IsRevertSubject(subject) ? subject[8..^1] : subject;

    static string TrimRemote(string name) => name.StartsWith("origin/") ? name["origin/".Length..] : name;

    static int IndexOrEnd(string text, string value) => text.IndexOf(value) is var i && i >= 0 ? i : text.Length;
}
