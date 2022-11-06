namespace gmd.ViewRepos.Private;

interface IConverter
{
    IReadOnlyList<Commit> ToCommits(IReadOnlyList<Augmented.Commit> commits);
    public IReadOnlyList<Branch> ToBranches(IReadOnlyList<Augmented.Branch> branches);
}


class Converter : IConverter
{
    public IReadOnlyList<Commit> ToCommits(IReadOnlyList<Augmented.Commit> commits) =>
       commits.Select(ToCommit).ToList();

    public IReadOnlyList<Branch> ToBranches(IReadOnlyList<Augmented.Branch> branches) =>
           branches.Select(ToBranch).ToList();

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

}