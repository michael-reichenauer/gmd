using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.RepoView;

// What a copy of commits puts on the clipboard, which the copy menu (Ctrl-C) offers
enum CopyKind
{
    Ids,
    ShortIds,
    Subjects,
    Messages,
    IdsAndSubjects,
    Range,
}

// What the log copies, with Ctrl-C, 'i' and '⇧i': which commits, and the text of each kind of copy
// of them. Never the rows as drawn, which carried the graph with them. Plain functions of the shown
// repo and the selection, so they are tested without a terminal.
//
// Several commits are copied oldest first, the order they were made in: 'git cherry-pick' applies
// ids in the order given, a squash joins messages so, and a pull request lists its commits so. The
// log shows them the other way round.
static class CommitCopy
{
    const string WhyNoUncommitted = "The uncommitted changes are no commit yet: move to one first";

    // The commits a copy acts on, oldest first. With rows selected, the commits of the branch the
    // selection started on, which are the ones the log highlights (RepoWriter), since the rows in
    // between belong to whichever branches the graph happens to draw there. Otherwise the commit on
    // the cursor's row. A row that is no commit, such as the uncommitted changes, is left out.
    public static IReadOnlyList<Commit> Commits(Repo repo, Selection selection, int rowIndex)
    {
        var commits = repo.ViewCommits;
        if (commits.Count == 0)
            return [];
        var last = commits.Count - 1;

        if (selection.IsEmpty)
        {
            var row = commits[Math.Clamp(rowIndex, 0, last)];
            return IsCommit(row) ? [row] : [];
        }

        var branch = commits[Math.Clamp(selection.InitialIndex, 0, last)].BranchPrimaryName;
        var (i1, i2) = (Math.Clamp(selection.I1, 0, last), Math.Clamp(selection.I2, 0, last));
        return commits
            .Skip(i1)
            .Take(i2 - i1 + 1)
            .Where(c => c.BranchPrimaryName == branch && IsCommit(c))
            .Reverse()
            .ToList();
    }

    // Why there is nothing to copy, or "" when there is
    public static string WhyNot(Repo repo, Selection selection, int rowIndex)
    {
        if (Commits(repo, selection, rowIndex).Any())
            return "";

        // The row the selection started on, whose branch it is
        var commits = repo.ViewCommits;
        var index = selection.IsEmpty ? rowIndex : selection.InitialIndex;
        var isUncommitted = commits.Count > 0 && commits[Math.Clamp(index, 0, commits.Count - 1)].IsUncommitted;
        return isUncommitted ? WhyNoUncommitted : "There is no commit to copy";
    }

    // The text a copy puts on the clipboard, or null when there is none, which only a range can lack
    public static string? Text(CopyKind kind, IReadOnlyList<Commit> commits) =>
        kind switch
        {
            CopyKind.Ids => commits.Select(c => c.Id).Join(" "),
            CopyKind.ShortIds => commits.Select(c => c.Sid).Join(" "),
            CopyKind.Subjects => commits.Select(c => c.Subject).Join("\n"),
            CopyKind.Messages => commits.Select(c => c.Message.TrimEnd()).Join("\n\n"),
            CopyKind.IdsAndSubjects => commits.Select(c => $"{c.Sid} {c.Subject}").Join("\n"),
            CopyKind.Range => Range(commits),
            _ => throw Asserter.Fail($"Unknown copy kind {kind}"),
        };

    // What the status line says once it is copied: the commit by its id as the log shows it, or how
    // many commits there were
    public static string Said(CopyKind kind, IReadOnlyList<Commit> commits)
    {
        if (kind == CopyKind.Range)
            return $"Copied the range {RangeName(commits)}";

        var (one, many) = kind switch
        {
            CopyKind.Ids => ("id", "ids"),
            CopyKind.ShortIds => ("short id", "short ids"),
            CopyKind.Subjects => ("subject", "subjects"),
            CopyKind.Messages => ("message", "messages"),
            CopyKind.IdsAndSubjects => ("id and subject", "ids and subjects"),
            _ => throw Asserter.Fail($"Unknown copy kind {kind}"),
        };
        return commits.Count == 1
            ? $"Copied the {one} of {commits[0].Sid}"
            : $"Copied the {many} of {commits.Count} commits";
    }

    // The range as the menu names it, with the ids as the log shows them
    public static string RangeName(IReadOnlyList<Commit> commits) =>
        commits.Any() ? $"{commits[0].Sid}^..{commits[^1].Sid}" : "";

    // Why there is no range, or "" when there is one
    public static string WhyNoRange(IReadOnlyList<Commit> commits) =>
        commits.Any() && commits[0].ParentIds.Count == 0
            ? "The oldest commit is the first one, with no parent to start a range at: copy the ids instead"
            : "";

    // The range git takes for the commits from the oldest to the newest, both of them included, as
    // 'git log', 'diff', 'cherry-pick' and 'format-patch' read 'A^..B'. Full ids, which no other
    // commit can share, as a short one can in a large repository.
    static string? Range(IReadOnlyList<Commit> commits) =>
        !commits.Any() || WhyNoRange(commits) != "" ? null : $"{commits[0].Id}^..{commits[^1].Id}";

    // The rows that are no commit: the uncommitted changes, and the one row of an empty repository
    // or of a search that found nothing
    static bool IsCommit(Commit c) =>
        !c.IsUncommitted && c.Id != Repo.EmptyRepoCommitId && c.Id != Repo.TruncatedLogCommitId;
}
