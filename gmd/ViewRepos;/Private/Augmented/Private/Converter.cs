

namespace gmd.ViewRepos.Private.Augmented.Private;

interface IConverter
{
    Repo ToRepo(AugRepo augRepo);
}

class Converter : IConverter
{
    public Repo ToRepo(AugRepo augRepo)
    {
        return new Repo(
            augRepo.Commits.Select(ToCommit).ToList(),
            augRepo.Branches.Select(ToBranch).ToList()
        );
    }


    Commit ToCommit(AugCommit c)
    {
        return new Commit(
            Id: c.Id,
            Sid: c.Sid,
            Subject: c.Subject,
            Message: c.Message,
            Author: c.Author,
            AuthorTime: c.AuthorTime,
            ParentIds: c.ParentIds,

            BranchName: c.BranchName,
            ChildIds: c.ChildIds,
            Tags: c.Tags,
            BranchTips: c.BranchTips,
            IsCurrent: c.IsCurrent,
            IsUncommitted: c.IsUncommitted,
            IsLocalOnly: c.IsLocalOnly,
            IsRemoteOnly: c.IsRemoteOnly,
            IsPartialLogCommit: c.IsPartialLogCommit,
            IsAmbiguous: c.IsAmbiguous,
            IsAmbiguousTip: c.IsAmbiguousTip);
    }

    Branch ToBranch(AugBranch b)
    {
        return new Branch(
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
            AmbiguousBranchNames: b.AmbiguousBranchNames);
    }
}

