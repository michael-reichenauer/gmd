
using AugmentedRepo = gmd.ViewRepos.Private.Augmented.Repo;
using AugmentedBranch = gmd.ViewRepos.Private.Augmented.Branch;
using AugmentedCommit = gmd.ViewRepos.Private.Augmented.Commit;

namespace gmd.ViewRepos.Private;

interface IConverter
{
    Repo ToRepo(AugmentedRepo augRepo);
}


class Converter : IConverter
{
    public Repo ToRepo(AugmentedRepo ar)
    {
        return new Repo(
            ar,
            ToCommits(ar),
            ToBranches(ar));
    }

    IReadOnlyList<Commit> ToCommits(AugmentedRepo augRepo) =>
       augRepo.Commits.Select(ToCommit).ToList();

    IReadOnlyList<Branch> ToBranches(AugmentedRepo augRepo) =>
           augRepo.Branches.Select(ToBranch).ToList();

    Commit ToCommit(AugmentedCommit c) => new Commit(
        Id: c.Id,
        Sid: c.Sid,
        Subject: c.Subject,
        Message: c.Message,
        Author: c.Author,
        AuthorTime: c.AuthorTime,

        BranchName: c.BranchName,
        ParentIds: c.ParentIds,
        ChildIds: c.ChildIds,
        Tags: c.Tags,
        BranchTips: c.BranchTips,
        IsCurrent: false,
        IsUncommitted: false,
        IsLocalOnly: false,
        IsRemoteOnly: false,
        IsPartialLogCommit: false,
        IsAmbiguous: false,
        IsAmbiguousTip: false,

        More: More.None);

    Branch ToBranch(AugmentedBranch b) => new Branch(
        Name: b.Name,
        DisplayName: b.DisplayName,
        TipID: b.TipID,
        IsCurrent: b.IsCurrent,
        IsRemote: b.IsRemote,
        RemoteName: b.RemoteName,

        IsGitBranch: b.IsGitBranch,
        IsDetached: b.IsDetached,
        IsAmbiguousBranch: b.IsAmbiguousBranch,
        IsSetAsParent: b.IsSetAsParent,
        IsMainBranch: b.IsMainBranch,

        AheadCount: b.AheadCount,
        BehindCount: b.BehindCount,
        HasLocalOnly: b.HasLocalOnly,
        HasRemoteOnly: b.HasRemoteOnly,

        AmbiguousTipId: b.AmbiguousTipId,
        AmbiguousBranchNames: b.AmbiguousBranchNames,

        X: 0,
        IsIn: false,
        IsOut: false);

}