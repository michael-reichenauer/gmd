

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
            Id: c.C.Id,
            Sid: c.C.Sid,
            Subject: c.C.Subject,
            Message: c.C.Message,
            Author: c.C.Author,
            AuthorTime: c.C.AuthorTime,
            ParentIds: c.C.ParentIds,

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
            Name: b.B.Name,
            DisplayName: b.B.DisplayName,
            TipID: b.B.TipID,
            IsCurrent: b.B.IsCurrent,
            IsRemote: b.B.IsRemote,
            RemoteName: b.B.RemoteName,

            IsGitBranch: b.IsGitBranch,
            IsDetached: b.B.IsDetached,
            IsAmbiguousBranch: b.IsAmbiguousBranch,
            IsSetAsParent: b.IsSetAsParent,
            IsMainBranch: b.IsMainBranch,

            AheadCount: b.B.AheadCount,
            BehindCount: b.B.BehindCount,
            HasLocalOnly: b.HasLocalOnly,
            HasRemoteOnly: b.HasRemoteOnly,

            AmbiguousTipId: b.AmbiguousTipId,
            AmbiguousBranchNames: b.AmbiguousBranchNames);
    }
}

