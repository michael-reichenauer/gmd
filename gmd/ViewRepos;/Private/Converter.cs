namespace gmd.ViewRepos.Private;

interface IConverter
{
    IReadOnlyList<Commit> ToCommits(IReadOnlyList<Augmented.Commit> commits);
    public IReadOnlyList<Branch> ToBranches(IReadOnlyList<Augmented.Branch> branches);
    CommitDiff ToCommitDiff(Utils.Git.CommitDiff gitCommitDiff);
}


class Converter : IConverter
{
    public IReadOnlyList<Commit> ToCommits(IReadOnlyList<Augmented.Commit> commits) =>
       commits.Select(ToCommit).ToList();

    public IReadOnlyList<Branch> ToBranches(IReadOnlyList<Augmented.Branch> branches) =>
           branches.Select(ToBranch).ToList();

    public CommitDiff ToCommitDiff(Utils.Git.CommitDiff gitCommitDiff)
    {
        var d = gitCommitDiff;
        return new CommitDiff(d.Id, d.Author, d.Date, d.Message, ToFileDiffs(d.FileDiffs));
    }

    Commit ToCommit(Augmented.Commit c, int index) => new Commit(
        Id: c.Id,
        Sid: c.Sid,
        Subject: c.Subject,
        Message: c.Message,
        Author: c.Author,
        AuthorTime: c.AuthorTime,

        Index: index,
        BranchName: c.BranchName,
        ParentIds: c.ParentIds,
        ChildIds: c.ChildIds,
        Tags: c.Tags,
        BranchTips: c.BranchTips,
        IsCurrent: c.IsCurrent,
        IsUncommitted: c.IsUncommitted,
        IsConflicted: c.IsConflicted,
        IsAhead: c.IsAhead,
        IsBehind: c.IsBehind,
        IsPartialLogCommit: c.IsPartialLogCommit,
        IsAmbiguous: c.IsAmbiguous,
        IsAmbiguousTip: c.IsAmbiguousTip,

        More: More.None);

    Branch ToBranch(Augmented.Branch b) => new Branch(
        Name: b.Name,
        CommonName: b.CommonName,
        DisplayName: b.DisplayName,
        TipId: b.TipId,
        BottomId: b.BottomId,
        IsCurrent: b.IsCurrent,
        IsRemote: b.IsRemote,
        RemoteName: b.RemoteName,
        LocalName: b.LocalName,

        ParentBranchName: b.ParentBranchName,
        IsGitBranch: b.IsGitBranch,
        IsDetached: b.IsDetached,

        IsSetAsParent: b.IsSetAsParent,
        IsMainBranch: b.IsMainBranch,

        AheadCount: b.AheadCount,
        BehindCount: b.BehindCount,
        HasLocalOnly: b.HasAheadCommits,
        HasRemoteOnly: b.HasBehindCommits,

        AmbiguousTipId: b.AmbiguousTipId,
        AmbiguousBranchNames: b.AmbiguousBranchNames,

        X: 0,
        IsIn: false,
        IsOut: false);


    IReadOnlyList<FileDiff> ToFileDiffs(IReadOnlyList<Utils.Git.FileDiff> fileDiffs) =>
        fileDiffs
            .Select(d => new FileDiff(d.PathBefore, d.PathAfter, d.IsRenamed,
                ToDiffMode(d.DiffMode), ToSectionDiffs(d.SectionDiffs)))
            .ToList();


    private IReadOnlyList<SectionDiff> ToSectionDiffs(IReadOnlyList<Utils.Git.SectionDiff> sectionDiffs) =>
        sectionDiffs
            .Select(d => new SectionDiff(d.ChangedIndexes, d.LeftLine, d.LeftCount,
                d.RightLine, d.RightCount, ToLineDiffs(d.LineDiffs)))
            .ToList();

    private IReadOnlyList<LineDiff> ToLineDiffs(IReadOnlyList<Utils.Git.LineDiff> lineDiffs) =>
        lineDiffs.Select(d => new LineDiff(ToDiffMode(d.DiffMode), d.Line)).ToList();


    private DiffMode ToDiffMode(Utils.Git.DiffMode diffMode)
    {
        switch (diffMode)
        {
            case Utils.Git.DiffMode.DiffAdded:
                return DiffMode.DiffAdded;
            case Utils.Git.DiffMode.DiffConflictEnd:
                return DiffMode.DiffConflictEnd;
            case Utils.Git.DiffMode.DiffConflicts:
                return DiffMode.DiffConflicts;
            case Utils.Git.DiffMode.DiffConflictSplit:
                return DiffMode.DiffConflictSplit;
            case Utils.Git.DiffMode.DiffConflictStart:
                return DiffMode.DiffConflictStart;
            case Utils.Git.DiffMode.DiffModified:
                return DiffMode.DiffModified;
            case Utils.Git.DiffMode.DiffRemoved:
                return DiffMode.DiffRemoved;
            case Utils.Git.DiffMode.DiffSame:
                return DiffMode.DiffSame;
        }

        Asserter.FailFast($"Unknown diff mode: {diffMode}");
        return DiffMode.DiffModified;
    }
}