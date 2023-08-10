namespace gmd.Server.Private;

interface IConverter
{
    IReadOnlyList<Commit> ToCommits(IEnumerable<Augmented.Commit> commits);
    IReadOnlyList<Branch> ToBranches(IEnumerable<Augmented.Branch> branches);
    IReadOnlyList<Stash> ToStashes(IEnumerable<Augmented.Stash> stashes);
    CommitDiff ToCommitDiff(Git.CommitDiff gitCommitDiff);
    CommitDiff[] ToCommitDiffs(Git.CommitDiff[] gitCommitDiffs);
    Branch ToBranch(Augmented.Branch branch);
    Commit ToCommit(Augmented.Commit commit, int index = -1);
}


class Converter : IConverter
{
    public IReadOnlyList<Commit> ToCommits(IEnumerable<Augmented.Commit> commits) =>
       commits.Select(ToCommit).ToList();

    public IReadOnlyList<Branch> ToBranches(IEnumerable<Augmented.Branch> branches) =>
           branches.Select(ToBranch).ToList();

    public IReadOnlyList<Stash> ToStashes(IEnumerable<Augmented.Stash> stashes) =>
        stashes.Select(ToStash).ToList();

    public CommitDiff[] ToCommitDiffs(Git.CommitDiff[] gitCommitDiffs) =>
        gitCommitDiffs.Select(ToCommitDiff).ToArray();

    public CommitDiff ToCommitDiff(Git.CommitDiff gitCommitDiff)
    {
        var d = gitCommitDiff;
        return new CommitDiff(d.Id, d.Author, d.Time, d.Message, ToFileDiffs(d.FileDiffs));
    }

    public Commit ToCommit(Augmented.Commit c, int index = -1) => new Commit(
        Id: c.Id,
        Sid: c.Sid,
        Subject: c.Subject,
        Message: c.Message,
        Author: c.Author,
        AuthorTime: c.AuthorTime,

        Index: index != -1 ? index : c.GitIndex,
        GitIndex: c.GitIndex,
        BranchName: c.BranchName,
        BranchPrimaryName: c.BranchPrimaryName,
        BranchNiceUniqueName: c.BranchNiceUniqueName,
        ParentIds: c.ParentIds,
        AllChildIds: c.AllChildIds,
        FirstChildIds: c.FirstChildIds,
        MergeChildIds: c.MergeChildIds,
        Tags: ToTags(c.Tags),
        BranchTips: c.BranchTips,
        IsCurrent: c.IsCurrent,
        IsDetached: c.IsDetached,
        IsUncommitted: c.IsUncommitted,
        IsConflicted: c.IsConflicted,
        IsAhead: c.IsAhead,
        IsBehind: c.IsBehind,
        IsTruncatedLogCommit: c.IsTruncatedLogCommit,
        IsAmbiguous: c.IsAmbiguous,
        IsAmbiguousTip: c.IsAmbiguousTip,
        IsBranchSetByUser: c.IsBranchSetByUser,
        HasStash: c.HasStash,

        More: More.None);

    public Branch ToBranch(Augmented.Branch b) => new Branch(
        Name: b.Name,
        PrimaryName: b.PrimaryName,
        PrimaryBaseName: b.PrimaryBaseName,
        NiceName: b.NiceName,
        NiceNameUnique: b.NiceNameUnique,
        TipId: b.TipId,
        BottomId: b.BottomId,
        IsCurrent: b.IsCurrent,
        IsLocalCurrent: b.IsLocalCurrent,
        IsRemote: b.IsRemote,
        RemoteName: b.RemoteName,
        LocalName: b.LocalName,

        ParentBranchName: b.ParentBranchName,
        PullMergeParentBranchName: b.PullMergeParentBranchName,
        IsGitBranch: b.IsGitBranch,
        IsDetached: b.IsDetached,
        IsPrimary: b.IsPrimary,
        IsMainBranch: b.IsMainBranch,

        HasLocalOnly: b.HasAheadCommits,
        HasRemoteOnly: b.HasBehindCommits,

        AmbiguousTipId: b.AmbiguousTipId,
        AmbiguousBranchNames: b.AmbiguousBranchNames,
        PullMergeBranchNames: b.PullMergeBranchNames,
        AncestorNames: b.AncestorNames,

        X: 0,
        IsIn: false,
        IsOut: false);


    Stash ToStash(Augmented.Stash s) =>
        new Stash(s.Id, s.Name, s.Branch, s.ParentId, s.IndexId, s.Message);

    static IReadOnlyList<Tag> ToTags(IReadOnlyList<Augmented.Tag> tags) =>
        tags.Select(t => new Tag(t.Name, t.CommitId)).ToList();

    IReadOnlyList<FileDiff> ToFileDiffs(IReadOnlyList<Git.FileDiff> fileDiffs) =>
        fileDiffs
            .Select(d => new FileDiff(d.PathBefore, d.PathAfter, d.IsRenamed, d.IsBinary,
                ToDiffMode(d.DiffMode), ToSectionDiffs(d.SectionDiffs)))
            .ToList();


    private IReadOnlyList<SectionDiff> ToSectionDiffs(IReadOnlyList<Git.SectionDiff> sectionDiffs) =>
        sectionDiffs
            .Select(d => new SectionDiff(d.ChangedIndexes, d.LeftLine, d.LeftCount,
                d.RightLine, d.RightCount, ToLineDiffs(d.LineDiffs)))
            .ToList();

    private IReadOnlyList<LineDiff> ToLineDiffs(IReadOnlyList<Git.LineDiff> lineDiffs) =>
        lineDiffs.Select(d => new LineDiff(ToDiffMode(d.DiffMode), d.Line)).ToList();


    private DiffMode ToDiffMode(Git.DiffMode diffMode)
    {
        switch (diffMode)
        {
            case Git.DiffMode.DiffAdded:
                return DiffMode.DiffAdded;
            case Git.DiffMode.DiffConflictEnd:
                return DiffMode.DiffConflictEnd;
            case Git.DiffMode.DiffConflicts:
                return DiffMode.DiffConflicts;
            case Git.DiffMode.DiffConflictSplit:
                return DiffMode.DiffConflictSplit;
            case Git.DiffMode.DiffConflictStart:
                return DiffMode.DiffConflictStart;
            case Git.DiffMode.DiffModified:
                return DiffMode.DiffModified;
            case Git.DiffMode.DiffRemoved:
                return DiffMode.DiffRemoved;
            case Git.DiffMode.DiffSame:
                return DiffMode.DiffSame;
        }

        Asserter.FailFast($"Unknown diff mode: {diffMode}");
        return DiffMode.DiffModified;
    }
}