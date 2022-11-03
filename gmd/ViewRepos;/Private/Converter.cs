
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
        var commits = ToCommits(ar);
        var branches = ToBranches(ar);

        return new Repo(
            ar,
            commits,
            branches);
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
        IsCurrent: c.IsCurrent,
        IsUncommitted: c.IsUncommitted,
        IsLocalOnly: c.IsLocalOnly,
        IsRemoteOnly: c.IsRemoteOnly,
        IsPartialLogCommit: c.IsPartialLogCommit,
        IsAmbiguous: c.IsAmbiguous,
        IsAmbiguousTip: c.IsAmbiguousTip,

        More: More.None);

    Branch ToBranch(AugmentedBranch b) => new Branch(
        Name: b.Name,
        DisplayName: b.DisplayName,
        TipID: b.TipID,
        IsCurrent: b.IsCurrent,
        IsRemote: b.IsRemote,
        RemoteName: b.RemoteName,
        LocalName: b.LocalName,

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